using ImGuiNET;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;
using NVec4 = System.Numerics.Vector4;
using static StupidTemplate.Settings;

namespace StupidTemplate.ImGuiUI
{
    public static class ImGuiMenu
    {
        // ---------- VR ----------
        public static bool VRMode;                 // true while drawing into the VR render texture (set by ImGuiHost)
        public static bool CenterRequest;          // set by ImGuiHost when the PC menu opens: put the window in the middle of the screen
        public const float VRMargin = 24f;         // room for shadow/glow inside the render texture

        public static bool SearchOpen
        {
            get { return searchOpen; }
        }

        public static void CloseSearch()
        {
            searchOpen = false;
            search = "";
            Main.pageNumber = 0;
        }

        // ---------- Open / close animation (set by ImGuiHost every frame) ----------
        public static float AnimAlpha = 1f;
        public static float AnimScale = 1f;
        public static NVec2 AnimPivot = new NVec2(0f, 0f);

        // ---------- Layout ----------
        private const float PanelWidth = 380f;
        private const float PanelHeight = 520f;
        private const float PanelRadius = 16f;
        private const float Pad = 16f;
        private const float Gap = 6f;
        private const float HeaderHeight = 84f;
        private const float IconSize = 38f;
        private const float FooterSize = 40f;
        private const int RowsPerPage = 5;
        private const float RowRounding = 0.2f;
        private const float IconRoundness = 0.28f;
        private const bool ShowArrayList = true;
        private const string ReturnButtonText = "Return to Main";

        // ---------- State ----------
        private static bool searchOpen;
        private static bool focusSearch;
        private static string search = "";
        private static NVec2 lastWindowPos = new NVec2(80f, 80f);

        private static readonly Dictionary<int, string> categoryOwner = new Dictionary<int, string>();
        private static readonly Dictionary<string, float> anim = new Dictionary<string, float>();

        // ---------- Colors ----------
        private static NVec4 PanelC, OutlineC, SubC, Text0, Text1, OffC, OnC, AccentC, DividerC;
        private static readonly NVec4 White = new NVec4(1f, 1f, 1f, 1f);
        private static readonly NVec4 Gold = new NVec4(1f, 0.82f, 0.2f, 1f);

        private static NVec4 Cl(Color c, float a = -1f)
        {
            return new NVec4(c.r, c.g, c.b, a < 0f ? c.a : a);
        }

        private static NVec4 Mix(NVec4 a, NVec4 b, float t)
        {
            return NVec4.Lerp(a, b, Mathf.Clamp01(t));
        }

        private static NVec4 A(NVec4 c, float a)
        {
            return new NVec4(c.X, c.Y, c.Z, a);
        }

        private static uint U32(NVec4 c)
        {
            return ImGui.ColorConvertFloat4ToU32(new NVec4(c.X, c.Y, c.Z, c.W * AnimAlpha));
        }

        private static NVec2 P(float x, float y)
        {
            return new NVec2(x, y);
        }

        private static float Anim(string id, float target, float speed = 14f)
        {
            float v;
            if (!anim.TryGetValue(id, out v))
                v = target;

            v = Mathf.MoveTowards(v, target, Time.unscaledDeltaTime * speed);
            anim[id] = v;
            return v;
        }

        private static void RefreshColors()
        {
            Color sub = ExtGradient.SubtextBase;
            Color divider = ExtGradient.DividerBase;

            PanelC = Cl(backgroundColor.GetCurrentColor());
            OutlineC = Cl((Color)ExtGradient.OutlineBase);
            SubC = Cl(sub);
            DividerC = Cl(divider);
            Text0 = Cl(textColors[0]);
            Text1 = Cl(textColors[1]);
            OffC = Cl(buttonColors[0].GetCurrentColor());
            OnC = Cl(buttonColors[1].GetCurrentColor());
            AccentC = Cl((Color)ExtGradient.AccentBase);
        }

        public static void ApplyStyle()
        {
            ImGuiStylePtr s = ImGui.GetStyle();
            s.WindowRounding = PanelRadius;
            s.FrameRounding = 8f;
            s.WindowBorderSize = 1f;
            s.FrameBorderSize = 0f;
            s.FramePadding = new NVec2(0f, 6f);
            s.ItemSpacing = new NVec2(8f, 6f);

            var c = s.Colors;
            c[(int)ImGuiCol.TextSelectedBg] = new NVec4(0.2f, 0.4f, 1f, 0.35f);
            c[(int)ImGuiCol.FrameBg] = new NVec4(0f, 0f, 0f, 0f);
            c[(int)ImGuiCol.FrameBgHovered] = new NVec4(0f, 0f, 0f, 0f);
            c[(int)ImGuiCol.FrameBgActive] = new NVec4(0f, 0f, 0f, 0f);
        }

        // ---------- Which buttons are listed ----------
        private static bool Searching()
        {
            return searchOpen && !string.IsNullOrWhiteSpace(search);
        }

        private static bool InMain(ButtonInfo b)
        {
            return Buttons.buttons.Length > 0 && Array.IndexOf(Buttons.buttons[0], b) >= 0;
        }

        private static bool IsCategoryButton(ButtonInfo b)
        {
            if (b.isTogglable)
                return false;

            if (Searching())
                return InMain(b);

            return Main.currentCategory == 0;
        }

        private static ButtonInfo[] GetVisible()
        {
            ButtonInfo[][] all = Buttons.buttons;

            if (Searching())
            {
                string q = search.Trim().ToLower();
                return all.SelectMany(l => l)
                    .Where(b => b != null && b.buttonText != null && b.buttonText != ReturnButtonText && b.buttonText.ToLower().Contains(q))
                    .ToArray();
            }

            int cat = Main.currentCategory;

            if (cat == Main.FavoritesCategory)
                return all.SelectMany(l => l)
                    .Where(b => b != null && Main.CanFavorite(b) && Main.IsFavorite(b.buttonText))
                    .Distinct()
                    .ToArray();

            if (cat < 0 || cat >= all.Length)
                return new ButtonInfo[0];

            return all[cat].Where(b => b != null && b.buttonText != ReturnButtonText).ToArray();
        }

        // ---------- Window ----------
        public static void Draw()
        {
            RefreshColors();

            ImGui.SetNextWindowSize(new NVec2(PanelWidth, PanelHeight), ImGuiCond.Always);

            if (VRMode)
                ImGui.SetNextWindowPos(new NVec2(VRMargin, VRMargin), ImGuiCond.Always);
            else if (CenterRequest)
            {
                NVec2 ds = ImGui.GetIO().DisplaySize;
                ImGui.SetNextWindowPos(new NVec2((ds.X - PanelWidth) / 2f, (ds.Y - PanelHeight) / 2f), ImGuiCond.Always);
            }
            else
                ImGui.SetNextWindowPos(new NVec2(80f, 80f), ImGuiCond.FirstUseEver);

            CenterRequest = false;

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, Mathf.Max(AnimAlpha, 0.01f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, PanelRadius);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, PanelC);
            ImGui.PushStyleColor(ImGuiCol.Border, OutlineC);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse |
                                     ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                     ImGuiWindowFlags.NoResize;
            if (VRMode)
                flags |= ImGuiWindowFlags.NoMove;

            bool open = ImGui.Begin("##menu", flags);

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(3);

            if (open)
                DrawContent();

            ImGui.End();

            if (VRMode && searchOpen)
                DrawKeyboard();

            ImGui.PopStyleVar();

            if (ShowArrayList && !VRMode)
                DrawArrayList();
        }

        private static void FadeLine(ImDrawListPtr dl, float x1, float x2, float y, NVec4 color)
        {
            float mid = (x1 + x2) / 2f;
            uint full = U32(color);
            uint none = U32(A(color, 0f));

            dl.AddRectFilledMultiColor(P(x1, y), P(mid, y + 1f), none, full, full, none);
            dl.AddRectFilledMultiColor(P(mid, y), P(x2, y + 1f), full, none, none, full);
        }

        private static void DrawContent()
        {
            NVec2 wp = ImGui.GetWindowPos();
            NVec2 ws = ImGui.GetWindowSize();
            lastWindowPos = wp;
            AnimPivot = P(wp.X + ws.X / 2f, wp.Y + ws.Y / 2f);

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;
            ImFontPtr regular = (ImFontPtr)ImGuiHost.Regular;

            // Soft shadow behind the panel
            ImDrawListPtr back = ImGui.GetBackgroundDrawList();
            for (int i = 1; i <= 6; i++)
            {
                float g = i * 3f;
                back.AddRectFilled(P(wp.X - g, wp.Y - g + 6f), P(wp.X + ws.X + g, wp.Y + ws.Y + g + 6f),
                    U32(new NVec4(0f, 0f, 0f, 0.035f)), PanelRadius + g);
            }

            // Soft glow along the top, following the rounded corners
            const int layers = 26;
            for (int i = 0; i < layers; i++)
            {
                float h = 96f * (1f - i / (float)layers);
                dl.AddRectFilled(P(wp.X + 1f, wp.Y + 1f), P(wp.X + ws.X - 1f, wp.Y + 1f + h),
                    U32(A(OnC, 0.0075f)), PanelRadius - 1f, ImDrawFlags.RoundCornersTop);
            }

            // ----- Header icon buttons (right side) -----
            float right = wp.X + ws.X - Pad;
            float iconY = wp.Y + Pad;
            int slots = 0;

            if (disconnectButton)
            {
                if (IconButton("leave", "leave", P(right - IconSize, iconY), IconSize, Cl(ExtGradient.Disconnect().GetCurrentColor())))
                {
                    try { NetworkSystem.Instance.ReturnToSinglePlayer(); }
                    catch (Exception exc) { Debug.LogError("Disconnect failed: " + exc.Message); }
                }
                slots++;
            }

            float searchX = right - IconSize * (slots + 1) - Gap * slots;
            if (IconButton("search", "search", P(searchX, iconY), IconSize, Cl(ExtGradient.Search().GetCurrentColor())))
            {
                searchOpen = !searchOpen;
                focusSearch = searchOpen;
                if (!searchOpen)
                    search = "";
                Main.pageNumber = 0;
            }

            if (!VRMode && searchOpen && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                searchOpen = false;
                search = "";
                Main.pageNumber = 0;
            }

            // ----- Title (turns into the search box while searching) -----
            float titleX = wp.X + Pad;
            float titleW = searchX - Gap - titleX;

            if (searchOpen && VRMode)
            {
                // VR: the on-screen keyboard types into this
                string shown = search.Length == 0 ? "type to search..." : search + "_";
                dl.AddText(bold, 22f, P(titleX, wp.Y + Pad + 2f), U32(search.Length == 0 ? SubC : Text0), shown);
                dl.AddRectFilled(P(titleX, wp.Y + Pad + 34f), P(titleX + titleW, wp.Y + Pad + 36f), U32(A(AccentC, 0.9f)), 1f);
            }
            else if (searchOpen)
            {
                ImGui.SetCursorScreenPos(P(titleX, wp.Y + Pad));
                ImGui.SetNextItemWidth(titleW);
                if (focusSearch)
                {
                    ImGui.SetKeyboardFocusHere();
                    focusSearch = false;
                }

                ImGui.PushFont(bold);
                ImGui.PushStyleColor(ImGuiCol.Text, Text0);
                if (ImGui.InputTextWithHint("##search", "type to search...", ref search, 32))
                    Main.pageNumber = 0;
                ImGui.PopStyleColor();
                ImGui.PopFont();

                dl.AddRectFilled(P(titleX, wp.Y + Pad + 34f), P(titleX + titleW, wp.Y + Pad + 36f), U32(A(AccentC, 0.9f)), 1f);
            }
            else
            {
                string title = Main.currentCategory == 0 ? PluginInfo.Name : TitleFor(Main.currentCategory);
                dl.AddText(bold, 25f, P(titleX, wp.Y + Pad + 1f), U32(Text0), title);
            }

            // ----- Rows / pages -----
            ButtonInfo[] visible = GetVisible();
            int pages = Mathf.Max(1, Mathf.CeilToInt(visible.Length / (float)RowsPerPage));
            if (Main.pageNumber > pages - 1) Main.pageNumber = pages - 1;
            if (Main.pageNumber < 0) Main.pageNumber = 0;

            string info = (fpsCounter ? "FPS " + Mathf.Ceil(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)) + "     " : "")
                          + "page " + (Main.pageNumber + 1) + "/" + pages;
            dl.AddText(regular, 14.5f, P(titleX, wp.Y + Pad + 42f), U32(SubC), info);

            FadeLine(dl, wp.X + Pad, wp.X + ws.X - Pad, wp.Y + HeaderHeight, DividerC);

            float footerTop = wp.Y + ws.Y - Pad - FooterSize;
            float listTop = wp.Y + HeaderHeight + 10f;
            float listBottom = footerTop - 12f;
            float rowH = ((listBottom - listTop) - Gap * (RowsPerPage - 1)) / RowsPerPage;
            float rowW = ws.X - Pad * 2f;

            ButtonInfo[] page = visible.Skip(Main.pageNumber * RowsPerPage).Take(RowsPerPage).ToArray();

            if (page.Length == 0)
            {
                string msg = Searching() ? "no results" : Main.currentCategory == Main.FavoritesCategory ? "star a mod to see it here" : "nothing here";
                NVec2 ts = ImGui.CalcTextSize(msg);
                dl.AddText(P(wp.X + (ws.X - ts.X) / 2f, listTop + 40f), U32(SubC), msg);
            }

            for (int i = 0; i < page.Length; i++)
                DrawRow(page[i], i, P(wp.X + Pad, listTop + i * (rowH + Gap)), rowW, rowH);

            // ----- Footer -----
            FadeLine(dl, wp.X + Pad, wp.X + ws.X - Pad, footerTop - 8f, DividerC);

            if (IconButton("prev", "prev", P(wp.X + Pad, footerTop), FooterSize, OffC))
            {
                Main.pageNumber--;
                if (Main.pageNumber < 0) Main.pageNumber = pages - 1;
            }

            if (IconButton("next", "next", P(wp.X + ws.X - Pad - FooterSize, footerTop), FooterSize, OffC))
            {
                Main.pageNumber++;
                if (Main.pageNumber > pages - 1) Main.pageNumber = 0;
            }

            if (Main.currentCategory != 0 || searchOpen)
            {
                if (IconButton("home", "home", P(wp.X + (ws.X - FooterSize) / 2f, footerTop), FooterSize, OffC))
                {
                    searchOpen = false;
                    search = "";
                    Main.currentCategory = 0;
                }
            }
        }

        // ---------- VR search keyboard (right of the panel, like the old menu's) ----------
        private const int MaxSearchLength = 16;

        private static void VRKey(string key)
        {
            if (key == "BACK")
            {
                if (search.Length > 0)
                    search = search.Substring(0, search.Length - 1);
            }
            else if (key == "CLEAR")
                search = "";
            else if (key == "SPACE")
            {
                if (search.Length < MaxSearchLength)
                    search += " ";
            }
            else if (search.Length < MaxSearchLength)
                search += key.ToLower();

            Main.pageNumber = 0;
        }

        private static bool KeyButton(string id, string label, string icon, NVec2 pos, float w, float h, NVec4 baseColor)
        {
            ImGui.SetCursorScreenPos(pos);
            bool pressed = ImGui.InvisibleButton("kb_" + id, P(w, h));
            bool hovered = ImGui.IsItemHovered();
            bool held = ImGui.IsItemActive();

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float hv = Anim("kb" + id, hovered ? 1f : 0f);
            NVec2 max = pos + P(w, h);
            NVec4 fill = Mix(baseColor, White, 0.10f * hv + (held ? 0.08f : 0f));
            float r = h * 0.22f;

            dl.AddRectFilled(pos, max, U32(A(fill, 0.96f)), r);
            dl.AddRect(pos, max, U32(A(Mix(OutlineC, OnC, hv), 0.9f)), r);

            if (icon != null)
                DrawIcon(dl, icon, pos + P(w / 2f, h / 2f), 8f, U32(Text0));
            else
            {
                ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;
                ImGui.PushFont(bold);
                NVec2 ts = ImGui.CalcTextSize(label);
                ImGui.PopFont();

                float k = 19f / 22f;   // bold font is loaded at 22, drawn at 19
                dl.AddText(bold, 19f, P(pos.X + (w - ts.X * k) / 2f, pos.Y + (h - ts.Y * k) / 2f), U32(Text0), label);
            }

            if (pressed)
                Main.PlayClickSound();

            return pressed;
        }

        private static void DrawKeyboard()
        {
            const float pad = 12f, gap = 4f, headerH = 18f, inputH = 30f, keyH = 40f;
            float kbH = pad * 2f + headerH + inputH + 8f + 5f * keyH + 4f * gap;

            ImGui.SetNextWindowSize(new NVec2(PanelWidth, kbH), ImGuiCond.Always);
            ImGui.SetNextWindowPos(new NVec2(VRMargin + PanelWidth + Gap, VRMargin), ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, PanelRadius);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, PanelC);
            ImGui.PushStyleColor(ImGuiCol.Border, OutlineC);

            bool open = ImGui.Begin("##menukb",
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(3);

            if (open)
            {
                NVec2 wp = ImGui.GetWindowPos();
                NVec2 ws = ImGui.GetWindowSize();
                ImDrawListPtr dl = ImGui.GetWindowDrawList();
                ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;
                ImFontPtr regular = (ImFontPtr)ImGuiHost.Regular;

                // Soft shadow
                ImDrawListPtr back = ImGui.GetBackgroundDrawList();
                for (int i = 1; i <= 6; i++)
                {
                    float g = i * 3f;
                    back.AddRectFilled(P(wp.X - g, wp.Y - g + 6f), P(wp.X + ws.X + g, wp.Y + ws.Y + g + 6f),
                        U32(new NVec4(0f, 0f, 0f, 0.035f)), PanelRadius + g);
                }

                // Header + input line with the accent underline
                dl.AddText(bold, 16f, P(wp.X + pad, wp.Y + pad), U32(Text0), "Unn4meD / search");

                float inputY = wp.Y + pad + headerH + 2f;
                if (search.Length == 0)
                    dl.AddText(regular, 16f, P(wp.X + pad, inputY + 5f), U32(SubC), "search mods, settings...");
                else
                    dl.AddText(bold, 19f, P(wp.X + pad, inputY + 3f), U32(Text0), search + "_");

                dl.AddRectFilled(P(wp.X + pad, inputY + inputH - 2f), P(wp.X + ws.X - pad, inputY + inputH), U32(A(AccentC, 0.9f)), 1f);

                // Keys
                float keysTop = inputY + inputH + 8f;
                float keyW = (ws.X - pad * 2f - gap * 9f) / 10f;
                NVec4 keyColor = Cl(ExtGradient.Key().GetCurrentColor());
                NVec4 accentKey = Cl(ExtGradient.Accent().GetCurrentColor());
                string[] rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };

                for (int r = 0; r < rows.Length; r++)
                {
                    string row = rows[r];
                    float rowTotal = row.Length * keyW + (row.Length - 1) * gap;
                    float rowLeft = wp.X + (ws.X - rowTotal) / 2f;

                    for (int i = 0; i < row.Length; i++)
                    {
                        string ch = row[i].ToString();
                        if (KeyButton(ch, ch, null, P(rowLeft + i * (keyW + gap), keysTop + r * (keyH + gap)), keyW, keyH, keyColor))
                            VRKey(ch);
                    }
                }

                // Bottom row: clear (2 keys), space (6), backspace (2, accent colored)
                float bottomY = keysTop + 4f * (keyH + gap);
                float x = wp.X + pad;
                string[] ids = { "CLEAR", "SPACE", "BACK" };
                string[] labels = { "clear", "space", null };
                string[] icons = { null, null, "prev" };
                int[] units = { 2, 6, 2 };

                for (int i = 0; i < 3; i++)
                {
                    float w = units[i] * keyW + (units[i] - 1) * gap;
                    if (KeyButton(ids[i], labels[i], icons[i], P(x, bottomY), w, keyH, i == 2 ? accentKey : keyColor))
                        VRKey(ids[i]);
                    x += w + gap;
                }
            }

            ImGui.End();
        }

        private static string TitleFor(int category)
        {
            string owner;
            return categoryOwner.TryGetValue(category, out owner) ? owner : Main.GetCategoryName();
        }

        // ---------- One mod row ----------
        private static void DrawRow(ButtonInfo b, int index, NVec2 pos, float w, float h)
        {
            ImGui.PushID(index);
            ImGui.SetCursorScreenPos(pos);
            bool pressed = ImGui.InvisibleButton("row", P(w, h));
            bool hovered = ImGui.IsItemHovered();

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;
            ImFontPtr regular = (ImFontPtr)ImGuiHost.Regular;
            NVec2 max = pos + P(w, h);
            float cy = pos.Y + h / 2f;

            bool toggle = b.isTogglable;
            bool on = toggle && b.enabled;
            bool cycle = Main.IsCycleButton(b);
            bool canFav = Main.CanFavorite(b);
            bool fav = canFav && Main.IsFavorite(b.buttonText);
            bool isCat = IsCategoryButton(b);

            float hv = Anim("h" + b.buttonText, hovered ? 1f : 0f);
            float sw = Anim("s" + b.buttonText, on ? 1f : 0f, 10f);

            NVec4 fill = Mix(Mix(OffC, OnC, 0.5f * sw), White, 0.06f * hv);
            float rounding = h * RowRounding;
            dl.AddRectFilled(pos, max, U32(A(fill, 0.96f)), rounding);
            dl.AddRect(pos, max, U32(A(Mix(OutlineC, OnC, sw), 0.7f + 0.3f * sw)), rounding);

            if (sw > 0.01f)
                dl.AddRectFilled(P(pos.X, pos.Y + 11f), P(pos.X + 3f, max.Y - 11f), U32(A(OnC, sw)), 2f);

            float cx = max.X - 12f;
            bool overStar = false, overFwd = false, overBack = false;

            if (canFav)
            {
                NVec2 sc = P(cx - 10f, cy);
                overStar = hovered && ImGui.IsMouseHoveringRect(P(sc.X - 13f, cy - 15f), P(sc.X + 13f, cy + 15f));
                DrawStar(dl, sc, 9f, fav, fav ? Gold : (overStar ? Text0 : A(Text0, 0.55f)));
                cx -= 32f;
            }

            if (toggle)
            {
                NVec2 tMin = P(cx - 44f, cy - 12f);
                NVec2 tMax = P(cx, cy + 12f);
                dl.AddRectFilled(tMin, tMax, U32(Mix(A(SubC, 0.35f), Mix(OnC, White, 0.25f), sw)), 12f);
                float knobX = Mathf.Lerp(tMin.X + 12f, tMax.X - 12f, sw);
                dl.AddCircleFilled(P(knobX, cy), 8.5f, U32(Mix(A(Text0, 0.75f), White, sw)), 20);
                cx -= 54f;
            }
            else if (cycle)
            {
                NVec2 fMin = P(cx - 28f, cy - 14f), fMax = P(cx, cy + 14f);
                NVec2 bMin = P(fMin.X - 32f, cy - 14f), bMax = P(fMin.X - 4f, cy + 14f);

                overFwd = hovered && ImGui.IsMouseHoveringRect(fMin, fMax);
                overBack = hovered && ImGui.IsMouseHoveringRect(bMin, bMax);

                dl.AddRectFilled(fMin, fMax, U32(overFwd ? OnC : A(White, 0.10f)), 8f);
                dl.AddRectFilled(bMin, bMax, U32(overBack ? OnC : A(White, 0.10f)), 8f);
                DrawIcon(dl, "next", P((fMin.X + fMax.X) / 2f, cy), 8f, U32(Text0));
                DrawIcon(dl, "prev", P((bMin.X + bMax.X) / 2f, cy), 8f, U32(Text0));
                cx = bMin.X - 8f;
            }
            else if (isCat)
            {
                float ax = cx - 10f + hv * 3f;
                dl.AddLine(P(ax - 4f, cy - 7f), P(ax + 3f, cy), U32(Text0), 2f);
                dl.AddLine(P(ax + 3f, cy), P(ax - 4f, cy + 7f), U32(Text0), 2f);
                cx -= 26f;
            }

            string name = b.overlapText != null ? b.overlapText : b.buttonText;
            bool hasDesc = !string.IsNullOrEmpty(b.toolTip);
            NVec4 nameColor = Mix(Text0, Text1, sw);

            dl.PushClipRect(P(pos.X + 12f, pos.Y), P(cx - 6f, max.Y), true);

            if (hasDesc)
            {
                dl.AddText(bold, 19f, P(pos.X + 16f, pos.Y + h * 0.16f), U32(nameColor), name);
                dl.AddText(regular, 14.5f, P(pos.X + 16f, pos.Y + h * 0.56f), U32(SubC), b.toolTip);
            }
            else
            {
                dl.AddText(bold, 19f, P(pos.X + 16f, cy - 10f), U32(nameColor), name);
            }

            dl.PopClipRect();

            // Clicks
            if (pressed)
            {
                if (overStar)
                {
                    Main.PlayClickSound();
                    Main.ToggleFavorite(b.buttonText);
                }
                else if (overFwd)
                {
                    Main.PlayClickSound();
                    Main.RunCycle(b, true);
                }
                else if (overBack)
                {
                    Main.PlayClickSound();
                    Main.RunCycle(b, false);
                }
                else if (!cycle)
                {
                    Main.Toggle(b.buttonText);   // plays the click sound itself

                    if (isCat)
                    {
                        if (Main.currentCategory != 0)
                            categoryOwner[Main.currentCategory] = b.buttonText;

                        searchOpen = false;
                        search = "";
                    }
                }
            }

            ImGui.PopID();
        }

        // ---------- Icon button ----------
        private static bool IconButton(string id, string icon, NVec2 pos, float size, NVec4 baseColor)
        {
            ImGui.SetCursorScreenPos(pos);
            bool pressed = ImGui.InvisibleButton("ib_" + id, P(size, size));
            bool hovered = ImGui.IsItemHovered();
            bool held = ImGui.IsItemActive();

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float hv = Anim("ib" + id, hovered ? 1f : 0f);
            NVec2 max = pos + P(size, size);

            NVec4 fill = Mix(baseColor, White, 0.10f * hv + (held ? 0.08f : 0f));
            float r = size * IconRoundness;

            dl.AddRectFilled(pos, max, U32(A(fill, 0.96f)), r);
            dl.AddRect(pos, max, U32(A(Mix(OutlineC, OnC, hv), 0.9f)), r);

            DrawIcon(dl, icon, pos + P(size / 2f, size / 2f), size * 0.27f * (1f + 0.08f * hv), U32(Text0));

            if (pressed)
                Main.PlayClickSound();   // click sound picked in your settings

            return pressed;
        }

        private static void DrawIcon(ImDrawListPtr dl, string icon, NVec2 c, float s, uint col)
        {
            const float t = 2f;

            switch (icon)
            {
                case "search":
                    dl.AddCircle(P(c.X - s * 0.12f, c.Y - s * 0.12f), s * 0.5f, col, 20, t);
                    dl.AddLine(P(c.X + s * 0.25f, c.Y + s * 0.25f), P(c.X + s * 0.7f, c.Y + s * 0.7f), col, t);
                    break;

                case "leave":
                    dl.AddLine(P(c.X + s * 0.05f, c.Y - s * 0.65f), P(c.X - s * 0.6f, c.Y - s * 0.65f), col, t);
                    dl.AddLine(P(c.X - s * 0.6f, c.Y - s * 0.65f), P(c.X - s * 0.6f, c.Y + s * 0.65f), col, t);
                    dl.AddLine(P(c.X - s * 0.6f, c.Y + s * 0.65f), P(c.X + s * 0.05f, c.Y + s * 0.65f), col, t);
                    dl.AddLine(P(c.X - s * 0.15f, c.Y), P(c.X + s * 0.7f, c.Y), col, t);
                    dl.AddLine(P(c.X + s * 0.4f, c.Y - s * 0.3f), P(c.X + s * 0.7f, c.Y), col, t);
                    dl.AddLine(P(c.X + s * 0.7f, c.Y), P(c.X + s * 0.4f, c.Y + s * 0.3f), col, t);
                    break;

                case "prev":
                    dl.AddLine(P(c.X + s * 0.65f, c.Y), P(c.X - s * 0.65f, c.Y), col, t);
                    dl.AddLine(P(c.X - s * 0.25f, c.Y - s * 0.42f), P(c.X - s * 0.65f, c.Y), col, t);
                    dl.AddLine(P(c.X - s * 0.65f, c.Y), P(c.X - s * 0.25f, c.Y + s * 0.42f), col, t);
                    break;

                case "home":
                    dl.AddLine(P(c.X - s * 0.75f, c.Y - s * 0.05f), P(c.X, c.Y - s * 0.75f), col, t);
                    dl.AddLine(P(c.X, c.Y - s * 0.75f), P(c.X + s * 0.75f, c.Y - s * 0.05f), col, t);
                    dl.AddLine(P(c.X - s * 0.5f, c.Y - s * 0.05f), P(c.X - s * 0.5f, c.Y + s * 0.65f), col, t);
                    dl.AddLine(P(c.X - s * 0.5f, c.Y + s * 0.65f), P(c.X + s * 0.5f, c.Y + s * 0.65f), col, t);
                    dl.AddLine(P(c.X + s * 0.5f, c.Y + s * 0.65f), P(c.X + s * 0.5f, c.Y - s * 0.05f), col, t);
                    break;

                default: // next
                    dl.AddLine(P(c.X - s * 0.65f, c.Y), P(c.X + s * 0.65f, c.Y), col, t);
                    dl.AddLine(P(c.X + s * 0.25f, c.Y - s * 0.42f), P(c.X + s * 0.65f, c.Y), col, t);
                    dl.AddLine(P(c.X + s * 0.65f, c.Y), P(c.X + s * 0.25f, c.Y + s * 0.42f), col, t);
                    break;
            }
        }

        private static void DrawStar(ImDrawListPtr dl, NVec2 center, float radius, bool filled, NVec4 color)
        {
            NVec2[] pts = new NVec2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? radius : radius * 0.42f;
                pts[i] = P(center.X + Mathf.Cos(angle) * r, center.Y + Mathf.Sin(angle) * r + 0.5f);
            }

            uint col = U32(color);

            if (filled)
            {
                for (int i = 0; i < 10; i++)
                    dl.AddTriangleFilled(center, pts[i], pts[(i + 1) % 10], col);
            }
            else
            {
                for (int i = 0; i < 10; i++)
                    dl.AddLine(pts[i], pts[(i + 1) % 10], col, 1.5f);
            }
        }

        // ---------- Enabled mods list, beside the panel (PC only) ----------
        private static void DrawArrayList()
        {
            string[] names = Buttons.buttons
                .SelectMany(l => l)
                .Where(b => b != null && b.enabled && b.isTogglable && b.buttonText != null)
                .Select(b => b.buttonText.ToUpper())
                .OrderByDescending(n => n.Length)
                .ToArray();

            if (names.Length == 0)
                return;

            ImDrawListPtr fg = ImGui.GetForegroundDrawList();
            ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;

            float x = lastWindowPos.X + PanelWidth + 14f;
            float y = lastWindowPos.Y + 8f;

            for (int i = 0; i < names.Length; i++)
            {
                float ly = y + i * 19f;
                fg.AddCircleFilled(P(x + 3f, ly + 9f), 2.5f, U32(OnC), 10);
                fg.AddText(bold, 16f, P(x + 12f, ly + 1f), U32(new NVec4(0f, 0f, 0f, 0.55f)), names[i]);
                fg.AddText(bold, 16f, P(x + 11f, ly), U32(Text1), names[i]);
            }
        }
    }
}