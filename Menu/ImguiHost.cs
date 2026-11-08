using ImGuiNET;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using NVec2 = System.Numerics.Vector2;
using ArrayListUI = Unnamed_Free.Menu.MenuSettings.ImGuiArrayList;

namespace StupidTemplate.ImGuiUI
{
    // =====================================================================================
    //  ImGuiHost, also draws the toasts (menu open or not) and the array list (monitor + headset)
    // =====================================================================================
    public class ImGuiHost : MonoBehaviour
    {
        public static ImGuiHost Instance;

        // true while the menu is logically open (it keeps drawing a moment longer while closing)
        public static bool Visible;

        // true while the menu is drawn at all (open, or playing the close animation). Used by ImGuiVR.
        public static bool Drawing
        {
            get { return Visible || (Instance != null && Instance.closing); }
        }

        // true while the menu is being drawn into the VR render texture (hand menu). Q on PC draws it on the monitor instead.
        public static bool DrawingVR
        {
            get { return Instance != null && Instance.drawVR && Drawing; }
        }

        // true when ImGui can show the toasts (menu open or not, PC and VR). Falls back to the 3D toast if something broke.
        public static bool CanShowToasts
        {
            get { return Instance != null && Instance.healthy && !Instance.toastBroken; }
        }

        // Boxed ImFontPtr, so this class still loads when ImGui.NET.dll is missing
        public static object Regular, Bold;

        private const bool ShowDebug = false;

        // Animation timings (seconds)
        private const float OpenGrow = 0.08f;
        private const float OpenSettle = 0.06f;
        private const float CloseGrow = 0.04f;
        private const float CloseShrink = 0.08f;

        // ---- toasts: ImGui draws them, they are turned into a mesh that hangs on the camera (same on PC and VR) ----
        // PC: bottom-left corner of the monitor, this many pixels from the edges
        private const float ToastMargin = 24f;

        // Array list on the monitor: distance from the top-right corner of the screen
        private const float ArrayListMarginX = 10f;
        private const float ArrayListMarginY = 10f;

        // VR: size of the toast area in "pixels", meters per pixel, and where its bottom-left corner sits in front of the eyes
        private const float ToastVRW = 440f;
        private const float ToastVRH = 320f;
        private const float ToastVRMeters = 0.0011f;
        private const float ToastVRLeft = -0.48f;     // camera space, more negative = further left
        private const float ToastVRBottom = -0.40f;   // more negative = further down
        private const float ToastVRDist = 1.0f;       // meters in front of the camera

        // VR array list (top-right of the headset view)
        private const float ListVRW = 300f;           // area size in "pixels"
        private const float ListVRH = 420f;
        private const float ListVRMeters = 0.0011f;
        private const float ListVRRight = 0.48f;      // camera space, right edge of the list (more positive = further right)
        private const float ListVRTop = 0.34f;        // camera space, top edge (more positive = further up)
        private const float ListVRDist = 1.0f;        // meters in front of the camera

        private static bool failed;
        private bool healthy;

        private IntPtr context;
        private IntPtr toastContext;   // separate ImGui context for the toasts, so they never disturb the menu's window state
        private IntPtr listContext;    // separate ImGui context for the array list on the monitor (drawn straight on the screen)
        private IntPtr vrListContext;  // separate ImGui context for the array list inside the headset (mesh)
        private bool listBroken;
        private bool vrListBroken;
        private bool hasListFrame;
        private bool loggedList;
        private bool loggedVRList;
        private Texture2D fontTexture;
        private Material material;
        private bool hasFrame;
        private Keyboard hookedKeyboard;

        private GameObject toastObj;
        private Mesh toastMesh;
        private Material toastMat;
        private bool toastBroken;

        private GameObject listObj;
        private Mesh listMesh;

        private readonly List<Vector3> tVerts = new List<Vector3>();
        private readonly List<Vector2> tUvs = new List<Vector2>();
        private readonly List<Color> tCols = new List<Color>();
        private readonly List<int> tTris = new List<int>();
        private static float[] colorLut;

        private bool cursorSaved;
        private CursorLockMode savedLock;
        private bool savedCursorVisible;

        private float[] vertFloats = new float[0];
        private int[] vertInts = new int[0];
        private short[] indices = new short[0];

        private bool closing;
        private bool drawVR;
        private bool justOpened;
        private float animStart = -10f;

        private string status = "starting";
        private int framesBuilt;
        private int framesRendered;
        private int lastVerts;
        private float visibleSince = -1f;
        private string shaderName = "none";

        // true when the headset is active and the render texture exists
        private static bool VR
        {
            get { return ImGuiVR.Active && ImGuiVR.Target != null; }
        }

        // true only when a real headset is running (ImGuiVR.Active can be true without one)
        private static bool RealHeadset
        {
            get
            {
                try { return UnityEngine.XR.XRSettings.isDeviceActive; }
                catch { return false; }
            }
        }

        public static void Ensure()
        {
            if (Instance != null || failed)
                return;

            GameObject go = new GameObject("ImGuiHost");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ImGuiHost>();
            go.AddComponent<ImGuiVR>();   // VR support, does nothing on PC
        }

        private Material CreateMaterial()
        {
            string[] candidates =
            {
                "GUI/Text Shader",
                "Hidden/Internal-GUITextureClip",
                "Hidden/Internal-GUITexture",
                "UI/Default"
            };

            foreach (string n in candidates)
            {
                Shader s = Shader.Find(n);
                if (s == null)
                    continue;

                Material m = new Material(s);
                m.hideFlags = HideFlags.HideAndDontSave;
                m.mainTexture = fontTexture;
                m.color = Color.white;

                if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
                if (m.HasProperty("_ZTest")) m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

                shaderName = n;
                Debug.Log("ImGuiHost: using shader " + n);
                return m;
            }

            throw new Exception("No usable shader found for ImGui rendering");
        }

        private void Awake()
        {
            try
            {
                context = ImGui.CreateContext();
                ImGui.SetCurrentContext(context);
                ImGuiIOPtr io = ImGui.GetIO();

                string fonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
                string regularPath = Path.Combine(fonts, "segoeui.ttf");
                string boldPath = Path.Combine(fonts, "seguisb.ttf");

                ImFontPtr reg = File.Exists(regularPath) ? io.Fonts.AddFontFromFileTTF(regularPath, 17f) : io.Fonts.AddFontDefault();
                ImFontPtr bold = File.Exists(boldPath) ? io.Fonts.AddFontFromFileTTF(boldPath, 22f) : reg;
                Regular = reg;
                Bold = bold;

                io.Fonts.GetTexDataAsAlpha8(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
                byte[] data = new byte[width * height];
                Marshal.Copy(pixels, data, 0, data.Length);

                fontTexture = new Texture2D(width, height, TextureFormat.Alpha8, false);
                fontTexture.filterMode = FilterMode.Bilinear;
                fontTexture.wrapMode = TextureWrapMode.Clamp;
                fontTexture.hideFlags = HideFlags.HideAndDontSave;
                fontTexture.LoadRawTextureData(data);
                fontTexture.Apply();

                io.Fonts.SetTexID((IntPtr)1);
                io.Fonts.ClearTexData();

                material = CreateMaterial();

                // the toast / list meshes use the same shader + font texture, drawn on top of everything
                toastMat = new Material(material);
                toastMat.hideFlags = HideFlags.HideAndDontSave;
                toastMat.renderQueue = 4000;

                ImGuiMenu.ApplyStyle();

                // second context for the toasts, shares the font atlas with the menu context
                try
                {
                    toastContext = ImGui.CreateContext(io.Fonts);
                }
                catch (Exception exc)
                {
                    toastBroken = true;
                    Debug.LogError("ImGuiHost: toast context failed, using the 3D toast: " + exc);
                }

                // third context for the array list on the monitor
                try
                {
                    listContext = ImGui.CreateContext(io.Fonts);
                }
                catch (Exception exc)
                {
                    listBroken = true;
                    Debug.LogError("ImGuiHost: array list context failed, no array list: " + exc);
                }

                // fourth context for the array list inside the headset
                try
                {
                    vrListContext = ImGui.CreateContext(io.Fonts);
                }
                catch (Exception exc)
                {
                    vrListBroken = true;
                    Debug.LogError("ImGuiHost: VR array list context failed, no VR array list: " + exc);
                }
                ImGui.SetCurrentContext(context);

                healthy = true;
                ImGuiBridge.Active = true;
                status = "ready (press Q or Insert)";
                Debug.Log("ImGui started OK");
            }
            catch (Exception exc)
            {
                failed = true;
                healthy = false;
                ImGuiBridge.Active = false;
                status = "INIT FAILED";
                Debug.LogError("ImGui init failed: " + exc);
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Debug.Log("ImGuiHost: DESTROYED\n" + Environment.StackTrace);

            healthy = false;
            ImGuiBridge.Active = false;
            ImGuiBridge.Typing = false;

            try
            {
                if (toastContext != IntPtr.Zero)
                    ImGui.DestroyContext(toastContext);
            }
            catch { }
            toastContext = IntPtr.Zero;

            try
            {
                if (listContext != IntPtr.Zero)
                    ImGui.DestroyContext(listContext);
            }
            catch { }
            listContext = IntPtr.Zero;

            try
            {
                if (vrListContext != IntPtr.Zero)
                    ImGui.DestroyContext(vrListContext);
            }
            catch { }
            vrListContext = IntPtr.Zero;

            if (hookedKeyboard != null)
                hookedKeyboard.onTextInput -= OnText;
        }

        // The old menu played these itself. With ImGui active that code never runs, so they are played from here.
        private void PlayMenuSound(bool opening)
        {
            if (!healthy)
                return;

            try
            {
                Debug.Log("ImGuiHost: playing " + (opening ? "open" : "close") + " sound");

                if (opening)
                {
                    Unnamed_Free.Classes.Assets.StartupSound.Play();
                    Main.PlayMenuSound("menuopen.wav");
                }
                else
                    Main.PlayMenuSound("menuclose.wav");
            }
            catch (Exception exc)
            {
                Debug.LogError("ImGuiHost: menu sound failed: " + exc);
            }
        }

        private void SetVisible(bool value)
        {
            if (Visible == value)
                return;

            Visible = value;
            animStart = Time.unscaledTime;
            Debug.Log("ImGuiHost: Visible -> " + value + " (healthy=" + healthy + ")");

            if (value)
            {
                justOpened = true;
                closing = false;
                savedLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                cursorSaved = true;
                visibleSince = Time.unscaledTime;
                framesRendered = 0;

                PlayMenuSound(true);
            }
            else
            {
                ImGuiBridge.Typing = false;
                visibleSince = -1f;

                if (!drawVR)
                    ImGuiMenu.CloseSearch();   // PC menu closed: don't come back with the search still open

                closing = healthy && Main.closeStyle != 2;

                if (cursorSaved)
                {
                    Cursor.lockState = savedLock;
                    Cursor.visible = savedCursorVisible;
                    cursorSaved = false;
                }

                PlayMenuSound(false);
            }
        }

        private void OnText(char c)
        {
            if (Visible && c >= 32)
                ImGui.GetIO().AddInputCharacter(c);
        }

        private void UpdateAnimation()
        {
            float t = Time.unscaledTime - animStart;
            float scale = 1f;
            float alpha = 1f;

            if (Visible)
            {
                if (t < OpenGrow)
                {
                    float k = t / OpenGrow;
                    scale = Mathf.Lerp(0.90f, 1.04f, k);
                    alpha = k;
                }
                else if (t < OpenGrow + OpenSettle)
                    scale = Mathf.Lerp(1.04f, 1f, (t - OpenGrow) / OpenSettle);
            }
            else if (closing)
            {
                if (t >= CloseGrow + CloseShrink)
                    closing = false;
                else if (t < CloseGrow)
                    scale = Mathf.Lerp(1f, 1.04f, t / CloseGrow);
                else
                {
                    float k = (t - CloseGrow) / CloseShrink;
                    k = k * k * (3f - 2f * k);
                    scale = Mathf.Lerp(1.04f, 0.85f, k);
                    alpha = 1f - k;
                }
            }

            ImGuiMenu.AnimScale = scale;
            ImGuiMenu.AnimAlpha = alpha;
        }

        private void Update()
        {
            if (healthy && !ImGuiBridge.Active)
                ImGuiBridge.Active = true;

            Keyboard kb = Keyboard.current;

            if (kb != null && hookedKeyboard != kb)
            {
                if (hookedKeyboard != null)
                    hookedKeyboard.onTextInput -= OnText;

                kb.onTextInput += OnText;
                hookedKeyboard = kb;
            }

            if (kb != null && kb.insertKey.wasPressedThisFrame)
                Main.keyboardMenuOpen = !Main.keyboardMenuOpen;

            // Hand menu: open while the button is held, and kept open while the floating search is open / tweening back
            bool handWants = VR && (VRFlags.HandHeld || (drawVR && (ImGuiMenu.SearchOpen || ImGuiVR.FloatBlend > 0f)));
            SetVisible(Main.keyboardMenuOpen || handWants);
            UpdateAnimation();

            // Q (PC) always draws on the monitor, the VR button draws on your hand. Kept while the close animation plays.
            if (Visible)
                drawVR = VR && !Main.keyboardMenuOpen;

            if (justOpened)
            {
                justOpened = false;
                ImGuiMenu.CenterRequest = true;
            }

            if (healthy && Visible && visibleSince > 0f && Time.unscaledTime - visibleSince > 3f && framesRendered == 0)
            {
                Debug.LogError("ImGui never drew anything (built=" + framesBuilt + ", verts=" + lastVerts + ", shader=" + shaderName + "), falling back to the old menu");
                status = "NOTHING DREW -> old menu";
                healthy = false;
                ImGuiBridge.Active = false;
            }

            // toasts first (own ImGui frame), then the array list (own frames), then the menu frame. Works with the menu open or closed.
            BuildToasts();
            BuildArrayList();

            hasFrame = false;
            if (!(Visible || closing) || !healthy)
                return;

            try
            {
                bool vr = drawVR && VR;
                ImGuiMenu.VRMode = vr;

                ImGui.SetCurrentContext(context);
                ImGuiIOPtr io = ImGui.GetIO();

                if (vr)
                    io.DisplaySize = new NVec2(ImGuiVR.TexW, ImGuiVR.TexH);
                else
                    io.DisplaySize = new NVec2(Screen.width, Screen.height);

                io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

                // In VR the mouse comes from the controller laser (ImGuiVR), not the desktop mouse
                if (!vr)
                    FeedInput(io, kb, Visible);

                ImGui.NewFrame();
                ImGuiMenu.Draw();
                ImGui.Render();
                hasFrame = true;
                framesBuilt++;

                ImGuiBridge.Typing = Visible && io.WantTextInput;
            }
            catch (Exception exc)
            {
                Debug.LogError("ImGui frame error: " + exc);
                status = "FRAME ERROR -> old menu";
                healthy = false;
                closing = false;
                ImGuiBridge.Active = false;
                SetVisible(false);
            }
        }

        private void LateUpdate()
        {
            // Don't touch the cursor in VR
            if (Visible && healthy && !drawVR)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static void FeedInput(ImGuiIOPtr io, Keyboard kb, bool allowInput)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (allowInput)
                {
                    Vector2 p = mouse.position.ReadValue();
                    io.AddMousePosEvent(p.x, Screen.height - p.y);
                    io.AddMouseButtonEvent(0, mouse.leftButton.isPressed);
                    io.AddMouseButtonEvent(1, mouse.rightButton.isPressed);
                    io.AddMouseButtonEvent(2, mouse.middleButton.isPressed);

                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                        io.AddMouseWheelEvent(0f, scroll / 120f);
                }
                else
                {
                    io.AddMousePosEvent(-10000f, -10000f);
                    io.AddMouseButtonEvent(0, false);
                    io.AddMouseButtonEvent(1, false);
                    io.AddMouseButtonEvent(2, false);
                }
            }

            if (kb != null && allowInput)
            {
                io.AddKeyEvent(ImGuiKey.Backspace, kb.backspaceKey.isPressed);
                io.AddKeyEvent(ImGuiKey.Delete, kb.deleteKey.isPressed);
                io.AddKeyEvent(ImGuiKey.Enter, kb.enterKey.isPressed);
                io.AddKeyEvent(ImGuiKey.Escape, kb.escapeKey.isPressed);
                io.AddKeyEvent(ImGuiKey.LeftArrow, kb.leftArrowKey.isPressed);
                io.AddKeyEvent(ImGuiKey.RightArrow, kb.rightArrowKey.isPressed);
                io.AddKeyEvent(ImGuiKey.Home, kb.homeKey.isPressed);
                io.AddKeyEvent(ImGuiKey.End, kb.endKey.isPressed);
                io.AddKeyEvent(ImGuiKey.ModCtrl, kb.ctrlKey.isPressed);
                io.AddKeyEvent(ImGuiKey.ModShift, kb.shiftKey.isPressed);
                io.AddKeyEvent(ImGuiKey.ModAlt, kb.altKey.isPressed);
                io.AddKeyEvent(ImGuiKey.A, kb.aKey.isPressed);
            }
        }

        private void OnGUI()
        {
            if (ShowDebug)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(10, 10, 1100, 24),
                    "ImGui: " + status + " | healthy=" + healthy + " active=" + ImGuiBridge.Active + " visible=" + Visible +
                    " closing=" + closing + " built=" + framesBuilt + " drawn=" + framesRendered +
                    " verts=" + lastVerts + " shader=" + shaderName + " vr=" + VR);
                GUI.color = Color.white;
            }

            if (!healthy || Event.current.type != EventType.Repaint)
                return;

            // 1) the array list on the monitor, straight on the screen (own context, drawn first so the menu sits on top of it)
            if (hasListFrame && listContext != IntPtr.Zero && !listBroken)
            {
                try
                {
                    ImGui.SetCurrentContext(listContext);
                    RenderDrawData(null, Screen.width, Screen.height, false);
                }
                catch (Exception exc)
                {
                    Debug.LogError("ImGui array list render error: " + exc);
                    listBroken = true;
                }
                finally
                {
                    ImGui.SetCurrentContext(context);
                }
            }

            // 2) the menu (only when Update built a frame for it)
            if (hasFrame)
            {
                try
                {
                    RenderDrawData(drawVR && VR ? ImGuiVR.Target : null,
                        drawVR && VR ? ImGuiVR.TexW : Screen.width,
                        drawVR && VR ? ImGuiVR.TexH : Screen.height,
                        true);
                    framesRendered++;
                }
                catch (Exception exc)
                {
                    Debug.LogError("ImGui render error: " + exc);
                    status = "RENDER ERROR -> old menu";
                    healthy = false;
                    closing = false;
                    ImGuiBridge.Active = false;
                    SetVisible(false);
                    return;
                }
            }
        }

        // ---------- array list (monitor + headset) ----------
        private void HideVRList()
        {
            if (listObj != null && listObj.activeSelf)
                listObj.SetActive(false);
        }

        private void BuildArrayList()
        {
            hasListFrame = false;

            if (!healthy || listBroken || listContext == IntPtr.Zero)
            {
                HideVRList();
                return;
            }

            try
            {
                if (!ArrayListUI.Enabled)
                {
                    HideVRList();
                    return;
                }

                ArrayListUI.Refresh();
                if (!ArrayListUI.HasItems)
                {
                    HideVRList();
                    return;
                }

                // monitor version (drawn in OnGUI)
                ImGui.SetCurrentContext(listContext);
                ImGuiIOPtr io = ImGui.GetIO();
                io.DisplaySize = new NVec2(Screen.width, Screen.height);
                io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

                ImGui.NewFrame();
                ArrayListUI.Draw(new NVec2(Screen.width - ArrayListMarginX, ArrayListMarginY));
                ImGui.Render();

                hasListFrame = true;

                if (!loggedList)
                {
                    loggedList = true;
                    Debug.Log("ImGuiHost: array list drawing (screen " + Screen.width + "x" + Screen.height + ", headset=" + RealHeadset + ")");
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("ImGui array list error: " + exc);
                listBroken = true;
                hasListFrame = false;
            }
            finally
            {
                ImGui.SetCurrentContext(context);
            }

            // headset version (mesh on the camera), only with a real headset
            if (RealHeadset && !vrListBroken && vrListContext != IntPtr.Zero)
            {
                try
                {
                    DrawVRList();
                }
                catch (Exception exc)
                {
                    Debug.LogError("ImGui VR array list error: " + exc);
                    vrListBroken = true;
                    HideVRList();
                }
                finally
                {
                    ImGui.SetCurrentContext(context);
                }
            }
            else
            {
                HideVRList();
            }
        }

        private bool EnsureListObject()
        {
            if (listObj != null)
                return true;

            GameObject cam = GameObject.Find("Main Camera");
            if (cam == null)
                return false;

            if (listMesh == null)
            {
                listMesh = new Mesh();
                listMesh.name = "IMGUI_LIST_MESH";
                listMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                listMesh.MarkDynamic();
            }

            listObj = new GameObject("IMGUI_ARRAYLIST");
            listObj.transform.SetParent(cam.transform, false);
            listObj.transform.localPosition = Vector3.zero;
            listObj.transform.localRotation = Quaternion.identity;

            listObj.AddComponent<MeshFilter>().sharedMesh = listMesh;
            MeshRenderer mr = listObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = toastMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            listObj.SetActive(false);
            Debug.Log("ImGuiHost: VR array list mesh ready");
            return true;
        }

        private void DrawVRList()
        {
            if (!EnsureListObject())
                return;

            float w = ListVRW;
            float h = ListVRH;
            float s = ListVRMeters;
            Vector3 topLeft = new Vector3(ListVRRight - w * s, ListVRTop, ListVRDist);

            ImGui.SetCurrentContext(vrListContext);
            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new NVec2(w, h);
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            ImGui.NewFrame();
            ArrayListUI.Draw(new NVec2(w - 6f, 6f));
            ImGui.Render();

            FillMesh(listMesh, listObj, topLeft, s);

            if (!loggedVRList)
            {
                loggedVRList = true;
                Debug.Log("ImGuiHost: VR array list drawing");
            }
        }

        // ---------- toasts ----------
        private void BuildToasts()
        {
            if (!healthy || toastBroken)
                return;

            if (!ImGuiNotifications.HasAny)
            {
                if (toastObj != null && toastObj.activeSelf)
                    toastObj.SetActive(false);
                return;
            }

            try
            {
                DrawToastMesh();
            }
            catch (Exception exc)
            {
                Debug.LogError("ImGui toast error (using the 3D toast instead): " + exc);
                toastBroken = true;
                ImGuiNotifications.Clear();
                if (toastObj != null)
                    toastObj.SetActive(false);
            }
        }

        private bool EnsureToastObject()
        {
            if (toastObj != null)
                return true;

            GameObject cam = GameObject.Find("Main Camera");
            if (cam == null)
                return false;

            if (toastMesh == null)
            {
                toastMesh = new Mesh();
                toastMesh.name = "IMGUI_TOAST_MESH";
                toastMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                toastMesh.MarkDynamic();
            }

            toastObj = new GameObject("IMGUI_TOAST");
            toastObj.transform.SetParent(cam.transform, false);
            toastObj.transform.localPosition = Vector3.zero;
            toastObj.transform.localRotation = Quaternion.identity;

            toastObj.AddComponent<MeshFilter>().sharedMesh = toastMesh;
            MeshRenderer mr = toastObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = toastMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            toastObj.SetActive(false);
            Debug.Log("ImGuiHost: toast mesh ready");
            return true;
        }

        private static Color ToColor(int col)
        {
            if (colorLut == null)
            {
                colorLut = new float[256];
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (int i = 0; i < 256; i++)
                    colorLut[i] = linear ? Mathf.GammaToLinearSpace(i / 255f) : i / 255f;
            }

            return new Color(colorLut[col & 0xFF], colorLut[(col >> 8) & 0xFF], colorLut[(col >> 16) & 0xFF], ((col >> 24) & 0xFF) / 255f);
        }

        // runs the toast frame in its own ImGui context, then switches back to the menu context
        private void DrawToastMesh()
        {
            if (toastContext == IntPtr.Zero)
            {
                toastBroken = true;
                return;
            }

            try
            {
                ImGui.SetCurrentContext(toastContext);
                DrawToastMeshInner();
            }
            finally
            {
                ImGui.SetCurrentContext(context);
            }
        }

        private void DrawToastMeshInner()
        {
            if (!EnsureToastObject())
                return;   // no camera yet, the toasts stay queued

            bool vr = ImGuiVR.Active;
            float w, h, s;
            Vector3 topLeft;

            if (vr)
            {
                // bottom-left of the headset view
                w = ToastVRW;
                h = ToastVRH;
                s = ToastVRMeters;
                topLeft = new Vector3(ToastVRLeft, ToastVRBottom + h * s, ToastVRDist);
            }
            else
            {
                // bottom-left of the monitor, 1 ImGui pixel = 1 screen pixel
                Camera cam = toastObj.transform.parent.GetComponent<Camera>();
                float fov = cam != null ? cam.fieldOfView : 60f;
                float d = 1f;
                float halfH = d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                float halfW = halfH * Screen.width / Mathf.Max(1, Screen.height);

                w = Screen.width;
                h = Screen.height;
                s = 2f * halfH / h;
                topLeft = new Vector3(-halfW, halfH, d);
            }

            // own ImGui frame just for the toasts, in the toast context (the menu context is not touched)
            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new NVec2(w, h);
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            ImGui.NewFrame();
            float margin = vr ? 8f : ToastMargin;
            ImGuiNotifications.Draw(new NVec2(margin, h - margin), !vr, io.DeltaTime);
            ImGui.Render();

            FillMesh(toastMesh, toastObj, topLeft, s);
        }

        // turns the current context's draw data into a mesh on obj (hidden when there is nothing to draw)
        private void FillMesh(Mesh mesh, GameObject obj, Vector3 topLeft, float s)
        {
            tVerts.Clear();
            tUvs.Clear();
            tCols.Clear();
            tTris.Clear();

            ImDrawDataPtr drawData = ImGui.GetDrawData();
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr list = drawData.CmdLists[n];
                int vtxCount = list.VtxBuffer.Size;
                int idxCount = list.IdxBuffer.Size;

                if (vertFloats.Length < vtxCount * 5)
                {
                    vertFloats = new float[vtxCount * 5];
                    vertInts = new int[vtxCount * 5];
                }
                if (indices.Length < idxCount)
                    indices = new short[idxCount];

                Marshal.Copy(list.VtxBuffer.Data, vertFloats, 0, vtxCount * 5);
                Marshal.Copy(list.VtxBuffer.Data, vertInts, 0, vtxCount * 5);
                Marshal.Copy(list.IdxBuffer.Data, indices, 0, idxCount);

                int baseV = tVerts.Count;
                for (int i = 0; i < vtxCount; i++)
                {
                    int v = i * 5;
                    tVerts.Add(new Vector3(topLeft.x + vertFloats[v] * s, topLeft.y - vertFloats[v + 1] * s, topLeft.z));
                    tUvs.Add(new Vector2(vertFloats[v + 2], vertFloats[v + 3]));
                    tCols.Add(ToColor(vertInts[v + 4]));
                }

                for (int c = 0; c < list.CmdBuffer.Size; c++)
                {
                    ImDrawCmdPtr cmd = list.CmdBuffer[c];
                    if (cmd.UserCallback != IntPtr.Zero)
                        continue;

                    int idxStart = (int)cmd.IdxOffset;
                    int vtxOffset = (int)cmd.VtxOffset;
                    int elemCount = (int)cmd.ElemCount;

                    for (int i = 0; i < elemCount; i++)
                        tTris.Add(baseV + (ushort)indices[idxStart + i] + vtxOffset);
                }
            }

            mesh.Clear();
            if (tTris.Count == 0)
            {
                obj.SetActive(false);
                return;
            }

            mesh.SetVertices(tVerts);
            mesh.SetUVs(0, tUvs);
            mesh.SetColors(tCols);
            mesh.SetTriangles(tTris, 0, true);

            if (!obj.activeSelf)
                obj.SetActive(true);
        }

        // Draws the current ImGui draw data. target = null draws on the screen, otherwise into that render texture.
        // menuAnim = apply the menu's open/close scale (never for toasts / array list).
        private void RenderDrawData(RenderTexture target, int screenW, int screenH, bool menuAnim)
        {
            RenderTexture prevRT = RenderTexture.active;

            if (target != null)
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);   // transparent background
            }

            ImDrawDataPtr drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                if (target != null)
                    RenderTexture.active = prevRT;
                return;
            }

            int totalVerts = 0;

            float sc = menuAnim ? ImGuiMenu.AnimScale : 1f;
            float pvX = menuAnim ? ImGuiMenu.AnimPivot.X : 0f;
            float pvY = menuAnim ? ImGuiMenu.AnimPivot.Y : 0f;

            bool oldSrgb = GL.sRGBWrite;
            GL.sRGBWrite = false;

            GL.PushMatrix();

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr list = drawData.CmdLists[n];

                int vtxCount = list.VtxBuffer.Size;
                int idxCount = list.IdxBuffer.Size;
                totalVerts += vtxCount;

                if (vertFloats.Length < vtxCount * 5)
                {
                    vertFloats = new float[vtxCount * 5];
                    vertInts = new int[vtxCount * 5];
                }
                if (indices.Length < idxCount)
                    indices = new short[idxCount];

                Marshal.Copy(list.VtxBuffer.Data, vertFloats, 0, vtxCount * 5);
                Marshal.Copy(list.VtxBuffer.Data, vertInts, 0, vtxCount * 5);
                Marshal.Copy(list.IdxBuffer.Data, indices, 0, idxCount);

                for (int c = 0; c < list.CmdBuffer.Size; c++)
                {
                    ImDrawCmdPtr cmd = list.CmdBuffer[c];
                    if (cmd.UserCallback != IntPtr.Zero)
                        continue;

                    float minX = Mathf.Max(0f, pvX + (cmd.ClipRect.X - pvX) * sc);
                    float minY = Mathf.Max(0f, pvY + (cmd.ClipRect.Y - pvY) * sc);
                    float maxX = Mathf.Min(screenW, pvX + (cmd.ClipRect.Z - pvX) * sc);
                    float maxY = Mathf.Min(screenH, pvY + (cmd.ClipRect.W - pvY) * sc);
                    if (maxX <= minX || maxY <= minY)
                        continue;

                    material.SetPass(0);

                    GL.Viewport(new Rect(minX, screenH - maxY, maxX - minX, maxY - minY));
                    GL.LoadPixelMatrix(minX, maxX, maxY, minY);

                    GL.Begin(GL.TRIANGLES);

                    int idxStart = (int)cmd.IdxOffset;
                    int vtxOffset = (int)cmd.VtxOffset;
                    int elemCount = (int)cmd.ElemCount;

                    for (int i = 0; i < elemCount; i++)
                    {
                        int v = ((ushort)indices[idxStart + i] + vtxOffset) * 5;
                        int col = vertInts[v + 4];

                        GL.Color(new Color32(
                            (byte)(col & 0xFF),
                            (byte)((col >> 8) & 0xFF),
                            (byte)((col >> 16) & 0xFF),
                            (byte)((col >> 24) & 0xFF)));
                        GL.TexCoord2(vertFloats[v + 2], vertFloats[v + 3]);
                        GL.Vertex3(pvX + (vertFloats[v] - pvX) * sc, pvY + (vertFloats[v + 1] - pvY) * sc, 0f);
                    }

                    GL.End();
                }
            }

            GL.Viewport(new Rect(0, 0, screenW, screenH));
            GL.PopMatrix();
            GL.sRGBWrite = oldSrgb;

            if (target != null)
                RenderTexture.active = prevRT;

            if (menuAnim)
                lastVerts = totalVerts;
        }
    }
}