using BepInEx;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using static StupidTemplate.Settings;

// =====================================================================================
//  This single file holds BOTH the notification library and the ImGui toast renderer.
//  Library.cs holds NotifiLib + the ImGui toast renderer (ImGuiNotifications).
//  ImGuiHost.cs is separate (replace your old ImGuiHost.cs with the new one).
// =====================================================================================

namespace StupidTemplate.Notifications
{
    [BepInPlugin("org.gorillatag.lars.notifications2", "NotificationLibrary", "1.0.7")]
    public class NotifiLib : BaseUnityPlugin
    {
        const string DefaultTitle = "UNN4MED MENU";

        // ---- sound (embedded wav, played through AssetLoader) ----
        const string SoundFile = "NotifiSound.wav";

        // AssetLoader tags its errors with this, they go to the log only, never to a toast
        const string SoundErrorTag = "<color=red>SOUND</color>";

        // ---- which notifications are allowed (case-insensitive, matched anywhere in the message) ----
        // mod on/off + player join/leave
        static readonly string[] AllowedTags = { "ENABLE", "DISABLE", "JOIN", "LEAVE", "LEFT" };

        // dropped even if they contain an allowed word (category buttons say "Opens the ... tab")
        static readonly string[] BlockedPhrases = { "OPENS THE" };

        // ---- position (camera space, in meters at Dist) ----
        const float Dist = 1.2f;
        const float ShownX = -0.50f;   // more negative = further left
        const float ShownY = -0.36f;   // more negative = further down
        const float HiddenX = -1.10f;  // where it slides in from / out to

        // ---- size ----
        const float CardW = 0.52f;
        const float CardH = 0.14f;
        const float BarW = 0.46f;

        // ---- timing (shorter = faster) ----
        const float SlideIn = 0.25f;
        const float Hold = 1.5f;
        const float SlideOut = 0.2f;

        // ---- speed up when several are waiting ----
        const float SpeedPerQueued = 1.2f;  // extra speed for each notification waiting in line
        const float MaxSpeed = 5f;          // never faster than this

        static readonly Queue<string[]> queue = new Queue<string[]>();
        public static string PreviousNotifi;
        public static bool IsEnabled = true;

        // ---- ImGui connection ----
        // The library checks by itself whether ImGuiHost has finished creating its fonts
        // (that means ImGui is up). You can still force it with ImGuiRunning = true/false.
        static bool imGuiForced;
        static bool imGuiBroken;
        public static bool ImGuiRunning
        {
            get { return imGuiForced || (!imGuiBroken && ImGuiReady()); }
            set { imGuiForced = value; if (!value) imGuiBroken = false; }
        }
        static float lastImGuiTime;

        // toasts handed to ImGui that Draw() has not picked up yet. If ImGuiHost never draws them,
        // they are moved to the 3D toast so notifications never just vanish.
        static readonly List<string[]> unconfirmed = new List<string[]>();
        static float unconfirmedSince;
        const float ImGuiPickupTimeout = 0.75f;

        GameObject root, bar;
        Text titleText, bodyText;
        Material textMat;
        Material edgeMat, panelMat, stripeMat, trackMat, barMat;
        bool active;
        float t;

        void Awake()
        {
            Logger.LogInfo("Plugin NotificationLibrary is loaded!");
        }

        // ---------- ImGui bridge ----------
        // Kept in separate NoInlining methods so NotifiLib can still load without ImGui.NET.dll.
        // The ImGui types are only touched when one of these actually runs.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ImGuiHostReady()
        {
            object font = StupidTemplate.ImGuiUI.ImGuiHost.Bold;
            bool hasFont = font is IntPtr ? (IntPtr)font != IntPtr.Zero : font != null;
            // PC: always. VR: only while the hand menu is drawn, otherwise the 3D toast is used.
            return hasFont && StupidTemplate.ImGuiUI.ImGuiHost.CanShowToasts;
        }

        static bool ImGuiReady()
        {
            try { return ImGuiHostReady(); }
            catch (Exception) { return false; } // ImGuiHost / ImGui.NET.dll not there
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ImGuiPush(string title, string message)
        {
            StupidTemplate.ImGuiUI.ImGuiNotifications.Push(title, message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ImGuiPending()
        {
            return StupidTemplate.ImGuiUI.ImGuiNotifications.PendingCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ImGuiClear()
        {
            StupidTemplate.ImGuiUI.ImGuiNotifications.Clear();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ImGuiClearPast(int amount)
        {
            StupidTemplate.ImGuiUI.ImGuiNotifications.ClearPast(amount);
        }

        static bool TryImGui(string title, string message)
        {
            if (!ImGuiRunning) return false;
            try
            {
                ImGuiPush(title, message);
                return true;
            }
            catch (Exception) // ImGui.NET.dll missing / failed to load
            {
                imGuiForced = false;
                imGuiBroken = true;
                return false;
            }
        }

        // ---------- 3D toast (fallback when ImGui is not running) ----------
        bool Build()
        {
            GameObject cam = GameObject.Find("Main Camera");
            if (cam == null) return false;

            if (textMat == null)
            {
                textMat = new Material(Shader.Find("GUI/Text Shader"));
                textMat.renderQueue = 3020;
            }

            root = new GameObject("NOTIFICATIONLIB_TOAST");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(HiddenX, ShownY, Dist);

            // outline, then the panel color on top
            edgeMat = Box(root.transform, CardW, CardH, 0.032f, 3000, Vector2.zero, out _);
            panelMat = Box(root.transform, CardW - 0.004f, CardH - 0.004f, 0.030f, 3001, Vector2.zero, out _);

            // accent stripe on the left side
            stripeMat = Box(root.transform, 0.007f, CardH - 0.032f, 0.0035f, 3002, new Vector2(-(CardW / 2f) + 0.018f, 0.002f), out _);

            // progress bar along the bottom (track + fill)
            trackMat = Box(root.transform, BarW, 0.006f, 0.003f, 3002, new Vector2(0.01f, -(CardH / 2f) + 0.014f), out _);
            barMat = Box(root.transform, BarW, 0.006f, 0.003f, 3003, new Vector2(0.01f, -(CardH / 2f) + 0.014f), out bar);

            // text (canvas units: 1 unit = 0.001 m)
            GameObject canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(root.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            canvasObj.transform.localScale = Vector3.one * 0.001f;
            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 160f);

            titleText = MakeText(canvasObj.transform, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(15f, 38f), new Vector2(440f, 26f));
            bodyText = MakeText(canvasObj.transform, 26, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(15f, -6f), new Vector2(440f, 62f));

            ApplyColors();
            root.SetActive(false);
            return true;
        }

        // Re-reads the menu theme every frame, so rainbow / custom gradients animate on the toast too
        void ApplyColors()
        {
            Color panel = backgroundColor.GetCurrentColor();
            panel.a = 1f;

            Color accent = buttonColors[1].GetCurrentColor();
            accent.a = 1f;

            edgeMat.color = ExtGradient.OutlineBase;
            panelMat.color = panel;
            stripeMat.color = accent;
            trackMat.color = ExtGradient.DividerBase;
            barMat.color = accent;

            titleText.color = accent;
            bodyText.color = textColors[0];
        }

        Material Box(Transform parent, float width, float height, float radius, int renderQueue, Vector2 offset, out GameObject obj)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            g.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            g.transform.localScale = new Vector3(0.001f, height, width);

            Material m = new Material(Shader.Find("GUI/Text Shader"));
            m.renderQueue = renderQueue;
            g.GetComponent<Renderer>().material = m;

            RoundedMesh.Apply(g, radius, 12);
            obj = g;
            return m;
        }

        Text MakeText(Transform parent, int size, FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 box)
        {
            GameObject g = new GameObject("Text");
            g.transform.SetParent(parent, false);
            Text tx = g.AddComponent<Text>();
            tx.font = currentFont;
            tx.fontSize = size;
            tx.fontStyle = style;
            tx.alignment = anchor;
            tx.supportRichText = true;
            tx.horizontalOverflow = HorizontalWrapMode.Wrap;
            tx.verticalOverflow = VerticalWrapMode.Truncate;
            tx.material = textMat;
            tx.rectTransform.sizeDelta = box;
            tx.rectTransform.localPosition = pos;
            return tx;
        }

        static float EaseOutBack(float k)
        {
            const float s = 1.2f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        void SetBar(float p)
        {
            p = Mathf.Clamp01(p);
            Vector3 s = bar.transform.localScale;
            s.z = Mathf.Max(0.0001f, BarW * p);
            bar.transform.localScale = s;

            // keeps the bar's left edge fixed while it shrinks
            Vector3 pos = bar.transform.localPosition;
            pos.x = 0.01f - BarW * 0.5f + s.z * 0.5f;
            bar.transform.localPosition = pos;
        }

        void Update()
        {
            // watchdog: did ImGuiHost actually pick the toasts up?
            if (unconfirmed.Count > 0)
            {
                int pendingNow = 0;
                try { pendingNow = ImGuiPending(); } catch { }

                if (pendingNow == 0)
                {
                    unconfirmed.Clear(); // Draw() took them, all good
                }
                else if (Time.unscaledTime - unconfirmedSince > ImGuiPickupTimeout)
                {
                    Debug.Log("[NotifiLib] ImGuiHost did not draw the toasts, using the 3D toast instead (is ImGuiNotifications.Draw being called?)");
                    try { ImGuiClear(); } catch { }
                    foreach (string[] n in unconfirmed) queue.Enqueue(n);
                    unconfirmed.Clear();
                    PreviousNotifi = null;
                }
            }

            // ImGui draws the toasts, nothing to do here unless the 3D toast is still playing
            if (ImGuiRunning && !active && queue.Count == 0)
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                return;
            }

            if (root == null)
            {
                active = false;
                if (!Build()) return;
            }

            if (!active)
            {
                if (queue.Count == 0) return;
                string[] n = queue.Dequeue();
                titleText.text = n[0].ToUpper();
                bodyText.text = n[1];
                t = 0f;
                active = true;
                SetBar(1f);
                root.SetActive(true);
                AssetLoader.PlayClick(SoundFile);
            }

            ApplyColors();

            // the more notifications waiting in line, the faster this one plays
            float speed = Mathf.Min(MaxSpeed, 1f + queue.Count * SpeedPerQueued);
            t += Time.unscaledDeltaTime * speed;
            float x;

            if (t < SlideIn)
            {
                float k = t / SlideIn;
                x = Mathf.LerpUnclamped(HiddenX, ShownX, EaseOutBack(k));
            }
            else if (t < SlideIn + Hold)
            {
                x = ShownX;
                SetBar(1f - (t - SlideIn) / Hold);
            }
            else if (t < SlideIn + Hold + SlideOut)
            {
                float k = (t - SlideIn - Hold) / SlideOut;
                x = Mathf.Lerp(ShownX, HiddenX, k * k * k);
                SetBar(0f);
            }
            else
            {
                active = false;
                root.SetActive(false);
                PreviousNotifi = null; // lets the same message show again later
                return;
            }

            root.transform.localPosition = new Vector3(x, ShownY, Dist);
        }

        static bool IsAllowed(string message)
        {
            string upper = message.ToUpperInvariant();

            foreach (string phrase in BlockedPhrases)
            {
                if (upper.Contains(phrase)) return false;
            }

            foreach (string tag in AllowedTags)
            {
                if (upper.Contains(tag)) return true;
            }
            return false;
        }

        public static void SendNotification(string NotificationText)
        {
            SendNotification(DefaultTitle, NotificationText);
        }

        public static void SendNotification(string title, string message)
        {
            if (message == null) return;

            // sound errors from AssetLoader stay in the log, no toast
            if (message.Contains(SoundErrorTag)) return;

            // only mod on/off and join/leave get through, categories etc. are dropped
            if (!IsAllowed(message)) return;

            if (disableNotifications || !IsEnabled) return;
            message = message.Trim();

            // ---- ImGui path ----
            // Update() never resets PreviousNotifi while ImGui handles the toasts, so a plain
            // "same as last" check would block repeats forever. Use a short time window instead.
            if (ImGuiRunning)
            {
                if (PreviousNotifi == message && Time.unscaledTime - lastImGuiTime < 1f) return;
                PreviousNotifi = message;
                lastImGuiTime = Time.unscaledTime;
                if (TryImGui(title, message))
                {
                    if (unconfirmed.Count == 0) unconfirmedSince = Time.unscaledTime;
                    unconfirmed.Add(new[] { title, message });
                    Debug.Log("[NotifiLib] sent to ImGui: " + message);
                    return;
                }
                PreviousNotifi = null; // ImGui failed, fall through to the 3D toast
                Debug.Log("[NotifiLib] ImGui push failed, using the 3D toast");
            }

            // ---- fallback: world-space toast ----
            if (PreviousNotifi == message) return;
            PreviousNotifi = message;
            queue.Enqueue(new[] { title, message });
        }

        public static void ClearAllNotifications()
        {
            queue.Clear();
            if (ImGuiRunning) { try { ImGuiClear(); } catch { } }
        }

        public static void ClearPastNotifications(int amount)
        {
            for (int i = 0; i < amount && queue.Count > 0; i++) queue.Dequeue();
            if (ImGuiRunning) { try { ImGuiClearPast(amount); } catch { } }
        }
    }
}

// =====================================================================================
//  ImGui toast renderer (was ImGuiNotifications.cs)
//  ImGuiHost calls Draw() every frame there is something to show (even when the menu is closed).
//  Keep ImGui.NET types out of fields / signatures that NotifiLib touches directly.
// =====================================================================================
namespace StupidTemplate.ImGuiUI
{
    using ImGuiNET;
    using System.Text.RegularExpressions;
    using NVec2 = System.Numerics.Vector2;
    using NVec4 = System.Numerics.Vector4;

    public static class ImGuiNotifications
    {
        private enum Kind { Info, On, Off, Join, Leave }

        private class Toast
        {
            public Kind kind;
            public string title;
            public string body;
            public string[] lines;
            public float height;
            public float t;          // animation time
            public float y;          // current distance from the bottom of the stack
            public bool placed;
        }

        // ---- size ----
        private const float CardW = 400f;
        private const float Gap = 8f;
        private const float Radius = 14f;
        private const float TextX = 56f;          // where the text starts inside the card
        private const float TextRight = 16f;      // right padding of the text
        private const int MaxVisible = 3;

        // ---- timing (seconds) ----
        private const float SlideIn = 0.25f;
        private const float Hold = 3.5f;   // how long a toast stays (seconds), menu open or closed
        private const float SlideOut = 0.2f;

        // ---- speed up when several are waiting ----
        private const float SpeedPerQueued = 1.2f;
        private const float MaxSpeed = 5f;

        private const string SoundFile = "NotifiSound.wav";

        private static readonly List<Toast> active = new List<Toast>();
        private static readonly Queue<Toast> pending = new Queue<Toast>();

        // set by Draw. ImGuiHost uses it to know the last draw list belongs to the toasts (they must not get the menu's open/close scaling)
        public static bool DrawnThisFrame;

        // toasts waiting to be picked up by Draw (NotifiLib uses this to detect that nothing is drawing)
        public static int PendingCount
        {
            get { return pending.Count; }
        }

        public static bool HasAny
        {
            get { return active.Count > 0 || pending.Count > 0; }
        }

        // ---------- colors ----------
        private static NVec4 PanelC, OutlineC, SubC, Text0, OnC, DividerC;
        private static readonly NVec4 Green = new NVec4(0.33f, 0.86f, 0.52f, 1f);
        private static readonly NVec4 Red = new NVec4(0.96f, 0.36f, 0.38f, 1f);
        private static readonly NVec4 Amber = new NVec4(1f, 0.78f, 0.25f, 1f);

        private static NVec4 Cl(Color c)
        {
            return new NVec4(c.r, c.g, c.b, c.a);
        }

        private static uint Col(NVec4 c, float alpha)
        {
            return ImGui.ColorConvertFloat4ToU32(new NVec4(c.X, c.Y, c.Z, Mathf.Clamp01(c.W * alpha)));
        }

        private static NVec2 P(float x, float y)
        {
            return new NVec2(x, y);
        }

        private static void RefreshColors()
        {
            Color panel = backgroundColor.GetCurrentColor();
            panel.a = 1f;
            Color sub = ExtGradient.SubtextBase;
            Color divider = ExtGradient.DividerBase;

            PanelC = Cl(panel);
            OutlineC = Cl((Color)ExtGradient.OutlineBase);
            SubC = Cl(sub);
            DividerC = Cl(divider);
            Text0 = Cl(textColors[0]);
            Color on = buttonColors[1].GetCurrentColor();
            on.a = 1f;
            OnC = Cl(on);
        }

        private static NVec4 AccentFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.On: return Green;
                case Kind.Off: return Red;
                case Kind.Leave: return Amber;
                default: return OnC;
            }
        }

        // ---------- public API ----------
        public static void Push(string title, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // ImGui can't draw <color> tags, so strip them and read the [ENABLE] / [JOIN] style prefix instead
            string stripped = Regex.Replace(message, "<.*?>", "").Trim();
            Match m = Regex.Match(stripped, @"^\s*\[([^\]]*)\]");
            string tag = (m.Success ? m.Groups[1].Value : stripped).ToUpperInvariant();

            Kind kind = Kind.Info;
            string head = string.IsNullOrEmpty(title) ? "Notice" : title;

            if (tag.Contains("DISABLE")) { kind = Kind.Off; head = "Disabled"; }
            else if (tag.Contains("ENABLE")) { kind = Kind.On; head = "Enabled"; }
            else if (tag.Contains("JOIN")) { kind = Kind.Join; head = "Player joined"; }
            else if (tag.Contains("LEAVE") || tag.Contains("LEFT")) { kind = Kind.Leave; head = "Player left"; }

            string body = Regex.Replace(stripped, @"^(\s*\[[^\]]*\]\s*)+", "").Trim();

            pending.Enqueue(new Toast { kind = kind, title = head, body = body });
        }

        public static void Clear()
        {
            active.Clear();
            pending.Clear();
        }

        public static void ClearPast(int amount)
        {
            for (int i = 0; i < amount && pending.Count > 0; i++)
                pending.Dequeue();
        }

        // ---------- text helpers ----------
        private static float Measure(ImFontPtr font, float size, string text)
        {
            ImGui.PushFont(font);
            float w = ImGui.CalcTextSize(text).X;
            ImGui.PopFont();
            float native = font.FontSize > 1f ? font.FontSize : size;
            return w * size / native;
        }

        private static string Ellipsize(ImFontPtr font, float size, string text, float maxW)
        {
            if (Measure(font, size, text) <= maxW)
                return text;

            while (text.Length > 1 && Measure(font, size, text + "...") > maxW)
                text = text.Substring(0, text.Length - 1);

            return text.TrimEnd() + "...";
        }

        // Splits the body into at most maxLines lines, the last one gets "..." if it doesn't fit
        private static string[] Wrap(ImFontPtr font, float size, string text, float maxW, int maxLines)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines.ToArray();

            string[] words = text.Split(' ');
            string current = "";

            foreach (string word in words)
            {
                if (word.Length == 0)
                    continue;

                string test = current.Length == 0 ? word : current + " " + word;
                if (current.Length == 0 || Measure(font, size, test) <= maxW)
                    current = test;
                else
                {
                    lines.Add(current);
                    current = word;
                }
            }

            if (current.Length > 0)
                lines.Add(current);

            if (lines.Count > maxLines)
            {
                string last = string.Join(" ", lines.GetRange(maxLines - 1, lines.Count - maxLines + 1).ToArray());
                lines = lines.GetRange(0, maxLines);
                lines[maxLines - 1] = last;
            }

            for (int i = 0; i < lines.Count; i++)
                lines[i] = Ellipsize(font, size, lines[i], maxW);

            return lines.ToArray();
        }

        private static void Layout(Toast n, ImFontPtr regular)
        {
            n.lines = Wrap(regular, 15f, n.body, CardW - TextX - TextRight, 2);

            float h = 12f + 20f + (n.lines.Length > 0 ? 4f + n.lines.Length * 17f : 0f) + 18f;
            n.height = Mathf.Max(56f, h);
        }

        private static float EaseOutBack(float k)
        {
            const float s = 0.9f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        // ---------- drawing ----------
        // origin = bottom-left corner of the stack, in the pixels of the target (screen or the VR texture)
        public static void Draw(NVec2 origin, bool shadow, float dt)
        {
            dt = Mathf.Clamp(dt, 0.0001f, 0.1f);

            ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;
            ImFontPtr regular = (ImFontPtr)ImGuiHost.Regular;

            // Move waiting ones into the visible stack
            while (active.Count < MaxVisible && pending.Count > 0)
            {
                Toast next = pending.Dequeue();
                Layout(next, regular);
                active.Add(next);

                try { AssetLoader.PlayClick(SoundFile); } catch { }
            }

            // The more are waiting, the faster they play
            float speed = Mathf.Min(MaxSpeed, 1f + pending.Count * SpeedPerQueued);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].t += dt * speed;
                if (active[i].t >= SlideIn + Hold + SlideOut)
                    active.RemoveAt(i);
            }

            if (active.Count == 0)
                return;

            RefreshColors();

            // Newest at the bottom, older ones slide up
            float offset = 0f;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Toast n = active[i];
                if (!n.placed)
                {
                    n.y = offset;
                    n.placed = true;
                }
                else
                    n.y = Mathf.Lerp(n.y, offset, 1f - Mathf.Exp(-dt * 16f));

                offset += n.height + Gap;
            }

            ImDrawListPtr dl = ImGui.GetForegroundDrawList();
            for (int i = 0; i < active.Count; i++)
                DrawToast(dl, active[i], origin, shadow, bold, regular);

            DrawnThisFrame = true;
        }

        private static void DrawToast(ImDrawListPtr dl, Toast n, NVec2 origin, bool shadow, ImFontPtr bold, ImFontPtr regular)
        {
            float t = n.t;
            float slide;
            float alpha = Mathf.Clamp01(t / 0.12f);
            float remaining = 1f;
            float off = -(CardW + 24f);

            if (t < SlideIn)
                slide = Mathf.LerpUnclamped(off, 0f, EaseOutBack(t / SlideIn));
            else if (t < SlideIn + Hold)
            {
                slide = 0f;
                remaining = 1f - (t - SlideIn) / Hold;
            }
            else
            {
                float k = (t - SlideIn - Hold) / SlideOut;
                slide = Mathf.Lerp(0f, off, k * k * k);
                alpha *= 1f - k;
                remaining = 0f;
            }

            alpha = Mathf.Max(0.02f, alpha);

            float x = origin.X + slide;
            float y = origin.Y - n.y - n.height;
            NVec2 min = P(x, y);
            NVec2 max = P(x + CardW, y + n.height);
            NVec4 accent = AccentFor(n.kind);

            // Soft shadow (PC only, the VR texture is a clean cut-out)
            if (shadow)
            {
                for (int i = 1; i <= 5; i++)
                {
                    float g = i * 2.5f;
                    dl.AddRectFilled(P(min.X - g, min.Y - g + 5f), P(max.X + g, max.Y + g + 5f),
                        Col(new NVec4(0f, 0f, 0f, 0.035f), alpha), Radius + g);
                }
            }

            // Card + soft accent glow along the top + outline
            dl.AddRectFilled(min, max, Col(PanelC, shadow ? 0.97f * alpha : alpha), Radius);

            // thin accent glow, only the top ~16px, fading out downward (raise GlowHeight / GlowAlpha for more)
            const int layers = 8;
            const float GlowHeight = 16f;
            const float GlowAlpha = 0.010f;
            for (int i = 0; i < layers; i++)
            {
                float h = GlowHeight * (1f - i / (float)layers);
                dl.AddRectFilled(P(min.X + 1f, min.Y + 1f), P(max.X - 1f, min.Y + 1f + h),
                    Col(accent, GlowAlpha * alpha), Radius - 1f, ImDrawFlags.RoundCornersTop);
            }

            dl.AddRect(min, max, Col(OutlineC, 0.9f * alpha), Radius);
            dl.AddRectFilled(P(min.X, min.Y + 12f), P(min.X + 3f, max.Y - 12f), Col(accent, alpha), 2f);

            // Icon in a circle
            NVec2 c = P(x + 32f, y + (n.height - 8f) / 2f);
            dl.AddCircleFilled(c, 15f, Col(accent, 0.16f * alpha), 28);
            dl.AddCircle(c, 15f, Col(accent, 0.85f * alpha), 28, 1.5f);
            DrawGlyph(dl, n.kind, c, Col(accent, alpha));

            // Text
            dl.PushClipRect(P(x + TextX - 4f, y), P(max.X - 8f, max.Y), true);

            float titleY = n.lines.Length == 0 ? y + (n.height - 8f) / 2f - 11f : y + 12f;
            dl.AddText(bold, 17f, P(x + TextX, titleY), Col(accent, alpha), n.title);

            for (int i = 0; i < n.lines.Length; i++)
                dl.AddText(regular, 15f, P(x + TextX, y + 34f + i * 17f), Col(Text0, 0.82f * alpha), n.lines[i]);

            dl.PopClipRect();

            // Progress bar along the bottom, shrinks while it is shown
            float bx0 = x + 16f;
            float bx1 = x + CardW - 16f;
            float by = max.Y - 9f;
            dl.AddRectFilled(P(bx0, by), P(bx1, by + 3f), Col(DividerC, alpha), 1.5f);
            if (remaining > 0.001f)
                dl.AddRectFilled(P(bx0, by), P(bx0 + (bx1 - bx0) * remaining, by + 3f), Col(accent, 0.9f * alpha), 1.5f);
        }

        private static void DrawGlyph(ImDrawListPtr dl, Kind kind, NVec2 c, uint col)
        {
            const float t = 2.2f;

            switch (kind)
            {
                case Kind.On: // check mark
                    dl.AddLine(P(c.X - 6f, c.Y + 0.5f), P(c.X - 2f, c.Y + 4.5f), col, t);
                    dl.AddLine(P(c.X - 2f, c.Y + 4.5f), P(c.X + 6.5f, c.Y - 4.5f), col, t);
                    break;

                case Kind.Off: // cross
                    dl.AddLine(P(c.X - 5f, c.Y - 5f), P(c.X + 5f, c.Y + 5f), col, t);
                    dl.AddLine(P(c.X - 5f, c.Y + 5f), P(c.X + 5f, c.Y - 5f), col, t);
                    break;

                case Kind.Join: // plus
                    dl.AddLine(P(c.X - 6f, c.Y), P(c.X + 6f, c.Y), col, t);
                    dl.AddLine(P(c.X, c.Y - 6f), P(c.X, c.Y + 6f), col, t);
                    break;

                case Kind.Leave: // minus
                    dl.AddLine(P(c.X - 6f, c.Y), P(c.X + 6f, c.Y), col, t);
                    break;

                default: // "i"
                    dl.AddCircleFilled(P(c.X, c.Y - 5.5f), 1.7f, col, 10);
                    dl.AddLine(P(c.X, c.Y - 1.5f), P(c.X, c.Y + 6f), col, t);
                    break;
            }
        }
    }
}