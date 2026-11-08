using BepInEx;
using GorillaLocomotion;
using HarmonyLib;
using Photon.Pun;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using Unnamed_Free.Mods;
using static StupidTemplate.Menu.Buttons;
using static StupidTemplate.Settings;

namespace StupidTemplate.Menu
{
    [HarmonyPatch(typeof(GTPlayer), "LateUpdate")]
    public class Main : MonoBehaviour
    {
        // Constant

        public static void Prefix()
        {
            // Initialize Menu
            try
            {
                // ImGui overlay. If it can't load, this logs an error and the old menu keeps working.
                StupidTemplate.ImGuiUI.ImGuiBridge.TryStart();

                // Keyboard: press the key once to open, press it again to close (disabled while typing a search)
                if (!searchActive && !StupidTemplate.ImGuiUI.ImGuiBridge.Typing && UnityInput.Current.GetKeyDown(keyboardButton))
                    keyboardMenuOpen = !keyboardMenuOpen;

                bool toOpen = (!rightHanded && ControllerInputPoller.instance.leftControllerSecondaryButton) || (rightHanded && ControllerInputPoller.instance.rightControllerSecondaryButton);

                // ImGui VR menu uses this same button (held), so the old 3D hand menu must not open as well
                // ImGui VR menu uses this same button (held), so the old 3D hand menu must not open as well
                StupidTemplate.ImGuiUI.VRFlags.HandHeld = toOpen;
                if (StupidTemplate.ImGuiUI.VRFlags.VRActive && StupidTemplate.ImGuiUI.ImGuiBridge.Active)
                    toOpen = false;

                // When ImGui is running it replaces the 3D PC menu. The VR hand menu is untouched.
                bool keyboardOpen = keyboardMenuOpen && !StupidTemplate.ImGuiUI.ImGuiBridge.Active;

                // Floating search (VR only): moves the blend toward the hand or the spot in the air
                UpdateFloatBlend();

                // Keep the menu alive while it floats or tweens back, even if you let go of the button
                bool floatKeep = !keyboardOpen && menu != null && (floatBlend > 0f || searchActive);

                if (menu == null)
                {
                    if (toOpen || keyboardOpen)
                    {
                        // Remember the cursor so it can be restored when the menu closes
                        if (keyboardOpen && !cursorSaved)
                        {
                            savedLockState = Cursor.lockState;
                            savedCursorVisible = Cursor.visible;
                            cursorSaved = true;
                        }

                        Unnamed_Free.Classes.Assets.StartupSound.Play();

                        CreateMenu();
                        RecenterMenu(rightHanded, keyboardOpen);
                        menuOpenedAt = Time.time; // starts the grow animation
                        PlayMenuSound("menuopen.wav");
                        if (reference == null)
                            CreateReference(rightHanded);
                    }
                    // Ghost body (timed)
                    UpdateGhostBody();

                }
                else
                {
                    if (toOpen || keyboardOpen || floatKeep)
                        RecenterMenu(rightHanded, keyboardOpen);
                    else
                    {
                        CloseMenuObject();

                        menu = null;
                        menuOpenedAt = -1f;
                        draggingMenu = false;
                        CloseSearch();

                        if (cursorSaved)
                        {
                            Cursor.lockState = savedLockState;
                            Cursor.visible = savedCursorVisible;
                            cursorSaved = false;
                        }

                        Destroy(reference);
                        reference = null;
                    }
                }

                // Typing into the search box (PC keyboard)
                UpdateSearchTyping();

                // Grow animation (does nothing once it has finished)
                UpdateGrowAnimation();

                // Close animation of menus that are closing (does nothing when there are none)
                UpdateCloseAnimations();
            }
            catch (Exception exc)
            {
                Debug.LogError(string.Format("{0} // Error initializing at {1}: {2}", PluginInfo.Name, exc.StackTrace, exc.Message));
            }

            // Cleanup Gun
            try
            {
                if (GunPointer != null)
                {
                    if (!GunPointer.activeSelf)
                        Destroy(GunPointer);
                    else
                        GunPointer.SetActive(false);
                }

                if (GunLine != null)
                {
                    if (!GunLine.gameObject.activeSelf)
                    {
                        Destroy(GunLine.gameObject);
                        GunLine = null;
                    }
                    else
                        GunLine.gameObject.SetActive(false);
                }
            }
            catch { }

            // Constant
            try
            {
                // Pre-Execution
                if (fpsObject != null)
                    fpsObject.text = "FPS: " + Mathf.Ceil(1f / Time.unscaledDeltaTime).ToString();

                // Execute Enabled Mods
                foreach (ButtonInfo button in buttons
                    .SelectMany(list => list)
                    .Where(button => button.enabled && button.method != null))
                {
                    try
                    {
                        button.method.Invoke();
                    }
                    catch (Exception exc)
                    {
                        Debug.LogError(string.Format("{0} // Error with mod {1} at {2}: {3}", PluginInfo.Name, button.buttonText, exc.StackTrace, exc.Message));
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.LogError(string.Format("{0} // Error with executing mods at {1}: {2}", PluginInfo.Name, exc.StackTrace, exc.Message));
            }
        }

        // ----- Size (all values are world units) -----
        private const float PanelWidth = 0.25f;       // width of the whole menu
        private const float PanelHeight = 0.42f;      // height of the whole menu
        private const int RowsPerPage = 5;            // mod rows per page (rows are taller now: title + description)

        // Layout
        private const float Pad = 0.012f;             // space between the panel edge and the content
        private const float Gap = 0.004f;             // space between rows / buttons
        private const float DividerHeight = 0.001f;   // thin lines under the header and above the footer
        private const float HeaderHeight = 0.036f;    // title + fps area at the top
        private const float HeaderButton = 0.034f;    // search / leave square icon buttons
        private const float FooterHeight = 0.040f;    // prev / home / next square icon buttons
        private const float IconRoundness = 0.28f;    // corner radius of the square icon buttons = this * button size
        private const float IconScale = 0.55f;        // icon size compared to its button
        private const string ReturnButtonText = "Return to Main"; // hidden from the lists, the home button does this
        private const float PanelRadius = 0.012f;     // corner radius of the whole menu
        private const float ButtonRoundness = 0.18f;  // row corner radius = this * row height (0.5 = pill shape)
        private const float LabelScale = 0.5f;        // text height compared to its button (smaller = smaller text)
        private const float RowTextPad = 0.012f;      // space between the row edge and its text
        private const float RowArrowWidth = 0.025f;   // room reserved for the ">" on the right of a row
        private const float StarZoneWidth = 0.036f;   // room reserved for the favorite star at the end of a mod row
        private const float StarIconSize = 0.024f;    // size of the star icon inside the row
        private const float CycleArrowSize = 0.02f;   // clickable size of each cycle arrow
        private const float CycleArrowGap = 0.002f;   // space between the two cycle arrows
        private const float CycleZoneWidth = CycleArrowSize * 2f + CycleArrowGap + 0.004f; // room reserved for both cycle arrows
        private const float CycleColliderGap = 0.006f; // extra gap between the row's collider and the arrows, so the row never fires with them
        private const float OutlineWidth = 0.0006f;   // thickness of the line around buttons
        private const float PanelOutlineWidth = 0.0008f; // thickness of the line around the panel
        private const float MenuDistance = 0.5f;      // how far in front of the screen camera the keyboard menu floats
        private const float MenuVerticalNudge = -0.02f; // small vertical offset of the menu when it is not dragged

        // Extras. Move these into Settings if you want them next to fpsCounter. Colors are in ExtGradient.
        private static bool outlineEnabled = true;    // thin outline around the panel and every button
        private static bool arrayList = true;         // list of enabled mods next to the panel

        // Grow animation
        private static readonly Vector3 HolderScale = new Vector3(0.1f, 0.3f, 0.3825f);
        private const float GrowTime1 = 0.07f;        // 0 -> overshoot
        private const float GrowTime2 = 0.04f;        // overshoot -> normal size
        private const float GrowOvershoot = 1.3f;
        private static float menuOpenedAt = -1f;

        // ---------- Close style setting ----------
        // 0 = Shrink (Untitled style), 1 = Drop (VR hand menu falls from your hand), 2 = Instant
        public static int closeStyle = 0;
        public static readonly string[] closeStyleNames = { "Shrink", "Drop", "Instant" };
        public const string CloseStyleButtonText = "Menu Close Style"; // buttonText of the button that cycles it

        // ---------- Click sound setting ----------
        // Which wav plays when you press something in the menu. Change the files / names here if you add more sounds.
        public static int clickSound = 3;             // index into the lists below (1 = Custom 1)
        public static readonly string[] clickSoundNames = { "Off", "Custom 1", "Custom 2", "Custom 3", "Minecraft" };
        public static readonly string[] clickSoundFiles = { null, "customclick1.wav", "customclick2.wav", "customclick3.wav", "minecraftclick.wav" };
        public const string ClickSoundButtonText = "Menu Click Sound"; // buttonText of the button that cycles it

        // Close animation (same as Untitled): grows a little, then shrinks to nothing
        private const float CloseTime1 = 0.05f;       // normal size -> slightly bigger
        private const float CloseTime2 = 0.07f;       // slightly bigger -> nothing
        private const float CloseGrow = 1.2f;

        private class ClosingMenu
        {
            public GameObject obj;
            public Vector3 startScale;
            public float startTime;
        }

        private static readonly List<ClosingMenu> closingMenus = new List<ClosingMenu>();

        // Floating search (VR only). While search is open the menu leaves your hand and floats in the air
        // in front of your face (fixed in the world, it does not follow you). Closing search tweens it back.
        private const float FloatDistance = 0.5f;       // how far in front of your face it floats
        private const float FloatHeightOffset = -0.03f; // vertical offset from eye level
        private const float FloatCenterShift = 0.125f;  // shifts it left so the panel + search keyboard end up centered
        private const float FloatTweenTime = 0.55f;     // seconds to fly to your face / back to your hand
        private const float FloatMaxDistance = 3f;      // walk further than this from it and the search closes by itself
        private static float floatBlend;                // 0 = in your hand, 1 = floating in the air
        private static bool floatWasSearching;
        private static Vector3 floatPosition;
        private static Quaternion floatRotation = Quaternion.identity;

        // Search
        private const int MaxSearchLength = 16;
        private static bool searchActive;             // true while the search box is open
        private static string searchQuery = "";
        private static bool searchDirty;              // set by typing, the menu is rebuilt next frame
        private static UnityEngine.InputSystem.Keyboard hookedKeyboard;

        // Names of the categories, index = currentCategory. Add one per category.
        public static string[] categoryNames = { "Main" };

        public static string GetCategoryName()
        {
            // The favorites category has its own fixed name
            if (currentCategory == FavoritesCategory)
                return "Favorites";

            if (currentCategory >= 0 && currentCategory < categoryNames.Length)
                return categoryNames[currentCategory];

            return "Category " + currentCategory;
        }

        // ---------- Cycle buttons (arrows to go back / forward) ----------
        // 1 = forward, -1 = back. Cycle methods read this through StepIndex.
        public static int cycleDirection = 1;
        private static bool cycleStepUsed;   // set to true when a cycle method used StepIndex

        // Use this in your cycle methods instead of (x + 1) % length
        public static int StepIndex(int current, int length)
        {
            cycleStepUsed = true;
            return ((current + cycleDirection) % length + length) % length;
        }

        // Runs a cycle button's method one step forward or back.
        // Methods that use StepIndex go back by themselves. Methods that still do their own "+ 1"
        // are handled here: the method is looped around until the button text repeats, which tells us
        // how many states there are, then it is stepped forward until it sits one state before where it started.
        public static void RunCycle(ButtonInfo target, bool forward)
        {
            if (forward)
            {
                cycleDirection = 1;
                try { target.method.Invoke(); } catch { }
                return;
            }

            cycleDirection = -1;
            cycleStepUsed = false;
            try { target.method.Invoke(); } catch { }
            cycleDirection = 1;

            // The method used StepIndex, so it already went back
            if (cycleStepUsed)
                return;

            // It went one step forward instead. Text of that state:
            string first = target.overlapText;

            int length = 0;
            do
            {
                try { target.method.Invoke(); } catch { }
                length++;
            }
            while (target.overlapText != first && length < 64);

            // Could not find the end of the loop (the method doesn't change the button text)
            if (target.overlapText != first)
                return;

            // We are on the first state again, step forward until we are one before where we started
            for (int i = 0; i < length - 2; i++)
            {
                try { target.method.Invoke(); } catch { }
            }
        }

        private static readonly Dictionary<ButtonInfo, bool> cycleCache = new Dictionary<ButtonInfo, bool>();

        // True for one-shot buttons whose method calls something with "cycle" in its name
        public static bool IsCycleButton(ButtonInfo button)
        {
            if (button == null || button.isTogglable || button.method == null)
                return false;

            bool result;
            if (cycleCache.TryGetValue(button, out result))
                return result;

            result = MethodCallsCycle(button.method);
            cycleCache[button] = result;
            return result;
        }

        private static bool MethodCallsCycle(Delegate d)
        {
            try
            {
                System.Reflection.MethodInfo info = d.Method;
                if (info.Name.IndexOf("cycle", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                System.Reflection.MethodBody body = info.GetMethodBody();
                if (body == null)
                    return false;

                byte[] il = body.GetILAsByteArray();
                System.Reflection.Module module = info.Module;

                for (int i = 0; i < il.Length - 4; i++)
                {
                    // 0x28 = call, 0x6F = callvirt, followed by a 4 byte method token
                    if (il[i] != 0x28 && il[i] != 0x6F)
                        continue;

                    try
                    {
                        System.Reflection.MethodBase called = module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                        if (called != null && called.Name.IndexOf("cycle", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        // ---------- Favorites ----------
        // A star sits at the end of every mod row (toggles only, not categories). Starred mods are listed in the Favorites category.
        public const int FavoritesCategory = 11;                 // category number that shows the favorited mods
        public const string FavoritesButtonText = "Favorites";   // buttonText of the button (in Buttons.cs) that opens it
        private const string FavoritesPrefKey = "Menu_Favorites"; // where the favorites are saved
        private static HashSet<string> favorites;

        private static void LoadFavorites()
        {
            favorites = new HashSet<string>();
            string saved = PlayerPrefs.GetString(FavoritesPrefKey, "");
            foreach (string name in saved.Split('\n'))
                if (!string.IsNullOrEmpty(name))
                    favorites.Add(name);
        }

        private static void SaveFavorites()
        {
            PlayerPrefs.SetString(FavoritesPrefKey, string.Join("\n", favorites.ToArray()));
            PlayerPrefs.Save();
        }

        public static bool IsFavorite(string buttonText)
        {
            if (favorites == null)
                LoadFavorites();

            return buttonText != null && favorites.Contains(buttonText);
        }

        public static void ToggleFavorite(string buttonText)
        {
            if (favorites == null)
                LoadFavorites();

            if (favorites.Contains(buttonText))
            {
                favorites.Remove(buttonText);
                NotifiLib.SendNotification("<color=grey>[</color><color=red>FAV</color><color=grey>]</color> Removed " + buttonText);
            }
            else
            {
                favorites.Add(buttonText);
                NotifiLib.SendNotification("<color=grey>[</color><color=yellow>FAV</color><color=grey>]</color> Added " + buttonText);
            }

            SaveFavorites();
        }

        // Only real mods (toggles) can be favorited. Categories, one-shot buttons and the Return / Favorites buttons can't.
        // Any mod can be favorited (toggles and one-shot buttons). Category buttons can't:
        // they are the non-togglable buttons in the main category (0), the ones that only change currentCategory.
        public static bool CanFavorite(ButtonInfo button)
        {
            if (button == null
                || button.buttonText == null
                || button.buttonText == ReturnButtonText
                || button.buttonText == FavoritesButtonText)
                return false;

            // Toggles are always fine
            if (button.isTogglable)
                return true;

            // One-shot buttons are fine unless they sit in the main category (those are the tab buttons)
            return buttons.Length == 0 || System.Array.IndexOf(buttons[0], button) < 0;
        }

        // Rows without a toggle (they open a category or run once) get a ">" on the right.
        // Change this if some of your one-shot buttons should not show the arrow.
        // Only rows in the main category (0) that aren't toggles open a category, so only they get the ">".
        // One-shot buttons in other categories no longer get an arrow.
        private static bool IsCategoryButton(ButtonInfo button)
        {
            if (button.isTogglable)
                return false;

            // While searching, the list mixes every category, so check where the button actually lives
            if (searchActive && searchQuery.Length > 0)
                return buttons.Length > 0 && System.Array.IndexOf(buttons[0], button) >= 0;

            return currentCategory == 0;
        }

        // ---------- Menu sounds ----------
        // Loading and playing is done by AssetLoader (Classes/AssetLoader.cs)

        // Plays an embedded wav by file name, e.g. PlayMenuSound("menuopen.wav")
        public static void PlayMenuSound(string fileName)
        {
            AssetLoader.PlayClick(fileName);
        }

        // Plays the click sound that is picked in the menu settings (does nothing when it is Off)
        public static void PlayClickSound()
        {
            if (clickSound <= 0 || clickSound >= clickSoundFiles.Length)
                return;

            string file = clickSoundFiles[clickSound];
            if (string.IsNullOrEmpty(file))
                return;

            AssetLoader.PlayClick(file);
        }

        // ---------- Search ----------

        // What the list shows: the current category, the favorites, or every mod whose name contains the search text.
        // "Return to Main" buttons are never listed, the home button at the bottom does that instead.
        private static ButtonInfo[] GetVisibleButtons()
        {
            bool searching = searchActive && searchQuery.Length > 0;

            // Favorites category: every starred mod from every category
            if (!searching && currentCategory == FavoritesCategory)
                return buttons
                    .SelectMany(list => list)
                    .Where(b => CanFavorite(b) && IsFavorite(b.buttonText))
                    .Distinct()
                    .ToArray();

            if (!searching)
                return buttons[currentCategory]
                    .Where(b => b != null && b.buttonText != ReturnButtonText)
                    .ToArray();

            string query = searchQuery.ToLower();
            return buttons
                .SelectMany(list => list)
                .Where(b => b != null && b.buttonText != null && b.buttonText != ReturnButtonText && b.buttonText.ToLower().Contains(query))
                .ToArray();
        }

        private static void CloseSearch()
        {
            searchActive = false;
            searchQuery = "";
            pageNumber = 0;
        }

        // Called for every character typed on the PC keyboard while search is open
        private static void OnTextTyped(char c)
        {
            if (!searchActive)
                return;

            if (c == '\b')
            {
                if (searchQuery.Length > 0)
                {
                    searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);
                    searchDirty = true;
                }
                return;
            }

            if ((char.IsLetterOrDigit(c) || c == ' ') && searchQuery.Length < MaxSearchLength)
            {
                searchQuery += char.ToLower(c);
                searchDirty = true;
            }
        }

        // Hooks the PC keyboard while search is open, rebuilds the menu after typing, Esc closes the search
        private static void UpdateSearchTyping()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (searchActive && keyboard != null && hookedKeyboard != keyboard)
            {
                if (hookedKeyboard != null)
                    hookedKeyboard.onTextInput -= OnTextTyped;

                keyboard.onTextInput += OnTextTyped;
                hookedKeyboard = keyboard;
            }
            else if (!searchActive && hookedKeyboard != null)
            {
                hookedKeyboard.onTextInput -= OnTextTyped;
                hookedKeyboard = null;
            }

            if (searchActive && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseSearch();
                searchDirty = true;
            }

            if (searchDirty)
            {
                searchDirty = false;
                pageNumber = 0;
                RecreateMenu();
            }
        }

        // A key of the on-screen keyboard was pressed (VR)
        private static void HandleKey(string key)
        {
            if (key == "BACK")
            {
                if (searchQuery.Length > 0)
                    searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);
            }
            else if (key == "CLEAR")
            {
                searchQuery = "";
            }
            else if (key == "SPACE")
            {
                if (searchQuery.Length < MaxSearchLength)
                    searchQuery += " ";
            }
            else if (key == "CLOSE")
            {
                CloseSearch();
            }
            else if (searchQuery.Length < MaxSearchLength)
            {
                searchQuery += key.ToLower();
            }

            pageNumber = 0;
            RecreateMenu();
        }

        // Scales the menu up with a small overshoot when it opens
        private static void UpdateGrowAnimation()
        {
            if (menu == null || menuOpenedAt < 0f)
                return;

            float t = Time.time - menuOpenedAt;
            float scale;

            if (t < GrowTime1)
                scale = Mathf.Lerp(0.01f, GrowOvershoot, t / GrowTime1);
            else if (t < GrowTime1 + GrowTime2)
                scale = Mathf.Lerp(GrowOvershoot, 1f, (t - GrowTime1) / GrowTime2);
            else
            {
                scale = 1f;
                menuOpenedAt = -1f;
            }

            menu.transform.localScale = HolderScale * scale;
        }

        // ---------- Menu settings (call these from buttons) ----------

        // Switches between Shrink / Drop / Instant (arrows: back / forward)
        public static void CycleCloseStyle()
        {
            closeStyle = StepIndex(closeStyle, closeStyleNames.Length);

            // Show the current style on the button's row
            ButtonInfo button = GetIndex(CloseStyleButtonText);
            if (button != null)
                button.overlapText = "Close Style: " + closeStyleNames[closeStyle];
        }

        // Switches the click sound (Off / Custom 1 / Custom 2 / Custom 3 / Minecraft) and plays a preview of it
        public static void CycleClickSound()
        {
            clickSound = StepIndex(clickSound, clickSoundNames.Length);

            // Show the current sound on the button's row
            ButtonInfo button = GetIndex(ClickSoundButtonText);
            if (button != null)
                button.overlapText = "Click Sound: " + clickSoundNames[clickSound];

            PlayClickSound();
        }

        // ---------- Close animation ----------

        // Closes the current menu the way the setting says
        private static void CloseMenuObject()
        {
            if (menu == null)
                return;

            bool isHandMenu = menu.transform.parent == null;

            if (closeStyle == 1 && isHandMenu)
            {
                // Drop: let it fall from your hand
                Rigidbody comp = menu.AddComponent(typeof(Rigidbody)) as Rigidbody;
                comp.linearVelocity = (rightHanded ? GTPlayer.Instance.LeftHand.velocityTracker : GTPlayer.Instance.RightHand.velocityTracker).GetAverageVelocity(true, 0);

                Destroy(menu, 2f);
                PlayMenuSound("menuclose.wav");
            }
            else if (closeStyle == 0)
            {
                // Shrink: grows a little, shrinks to nothing, then it is destroyed
                StartCloseAnimation(menu);
                PlayMenuSound("menuclose.wav");
            }
            else
            {
                // Instant (also used by Drop on the keyboard menu)
                Destroy(menu);
                PlayMenuSound("menuclose.wav");
            }
        }

        // Hands the menu over to the close animation. It stays where it was when you closed it.
        private static void StartCloseAnimation(GameObject closingMenu)
        {
            if (closingMenu == null)
                return;

            // The closing menu must not be clickable anymore
            foreach (Collider collider in closingMenu.GetComponentsInChildren<Collider>())
                Destroy(collider);

            closingMenus.Add(new ClosingMenu
            {
                obj = closingMenu,
                startScale = closingMenu.transform.localScale,
                startTime = Time.time
            });
        }

        // Runs every frame from Prefix: grows a little, shrinks to nothing, then destroys the menu
        private static void UpdateCloseAnimations()
        {
            for (int i = closingMenus.Count - 1; i >= 0; i--)
            {
                ClosingMenu closing = closingMenus[i];

                if (closing.obj == null)
                {
                    closingMenus.RemoveAt(i);
                    continue;
                }

                float t = Time.time - closing.startTime;

                if (t < CloseTime1)
                    closing.obj.transform.localScale = Vector3.Lerp(closing.startScale, closing.startScale * CloseGrow, t / CloseTime1);
                else if (t < CloseTime1 + CloseTime2)
                    closing.obj.transform.localScale = Vector3.Lerp(closing.startScale * CloseGrow, Vector3.zero, (t - CloseTime1) / CloseTime2);
                else
                {
                    Destroy(closing.obj);
                    closingMenus.RemoveAt(i);
                }
            }
        }

        // ---------- Floating search ----------

        private static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        // Remembers a spot in the air in front of your face. It stays there, it is not updated while floating.
        private static void CaptureFloatPose()
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;

            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = Vector3.forward;
            flat.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, flat);

            floatPosition = head.position
                + flat * FloatDistance
                + Vector3.up * FloatHeightOffset
                - right * FloatCenterShift;

            // Same orientation the PC screen menu uses, so the front faces you
            floatRotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(-90f, 90f, 0f);
        }

        // Runs once per frame. Search open (VR) = blend goes to 1, search closed = blend goes back to 0.
        private static void UpdateFloatBlend()
        {
            if (menu == null)
            {
                floatBlend = 0f;
                floatWasSearching = false;
                return;
            }

            bool want = searchActive && !keyboardMenuOpen;

            // Search just opened: pick the spot in the air (not while it is still tweening back)
            if (want && !floatWasSearching && floatBlend <= 0.001f)
                CaptureFloatPose();

            // Walked away from the floating menu: close the search so it comes back to your hand
            if (want && floatBlend >= 1f
                && Vector3.Distance(GorillaTagger.Instance.headCollider.transform.position, floatPosition) > FloatMaxDistance)
            {
                CloseSearch();
                searchDirty = true;
                want = false;
            }

            floatWasSearching = want;
            floatBlend = Mathf.MoveTowards(floatBlend, want ? 1f : 0f, Time.deltaTime / FloatTweenTime);
        }

        // ---------- Icons (drawn from lines, so no font glyphs or assets are needed) ----------
        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        // A 5 point star outline (closed). scale 1 = full size.
        private static Vector2[] StarPoints(float scale)
        {
            Vector2[] points = new Vector2[11]; // 5 outer + 5 inner points, the last one closes the shape
            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = (i % 2 == 0 ? 0.78f : 0.32f) * scale;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.05f * scale);
            }
            return points;
        }

        private static Vector2[][] IconStrokes(string name)
        {
            switch (name)
            {
                case "search":
                    {
                        Vector2[] ring = new Vector2[25];
                        for (int i = 0; i <= 24; i++)
                        {
                            float a = i / 24f * Mathf.PI * 2f;
                            ring[i] = new Vector2(-0.12f + Mathf.Cos(a) * 0.42f, 0.12f + Mathf.Sin(a) * 0.42f);
                        }
                        return new[] { ring, new[] { new Vector2(0.18f, -0.18f), new Vector2(0.62f, -0.62f) } };
                    }
                case "leave":
                    return new[]
                    {
                        new[] { new Vector2(0.05f, 0.6f), new Vector2(-0.55f, 0.6f), new Vector2(-0.55f, -0.6f), new Vector2(0.05f, -0.6f) },
                        new[] { new Vector2(-0.15f, 0f), new Vector2(0.65f, 0f) },
                        new[] { new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0f), new Vector2(0.35f, -0.3f) }
                    };
                case "close":
                    return new[]
                    {
                        new[] { new Vector2(-0.55f, 0.55f), new Vector2(0.55f, -0.55f) },
                        new[] { new Vector2(-0.55f, -0.55f), new Vector2(0.55f, 0.55f) }
                    };
                case "home":
                    return new[]
                    {
                        new[] { new Vector2(-0.7f, 0.05f), new Vector2(0f, 0.72f), new Vector2(0.7f, 0.05f) },
                        new[] { new Vector2(-0.5f, 0.05f), new Vector2(-0.5f, -0.65f), new Vector2(0.5f, -0.65f), new Vector2(0.5f, 0.05f) },
                        new[] { new Vector2(-0.14f, -0.65f), new Vector2(-0.14f, -0.25f), new Vector2(0.14f, -0.25f), new Vector2(0.14f, -0.65f) }
                    };
                case "prev":
                    return new[]
                    {
                        new[] { new Vector2(0.65f, 0f), new Vector2(-0.65f, 0f) },
                        new[] { new Vector2(-0.25f, 0.4f), new Vector2(-0.65f, 0f), new Vector2(-0.25f, -0.4f) }
                    };
                case "star":
                    return new[] { StarPoints(1f) };
                case "starfilled":
                    {
                        // The same star drawn several times at smaller sizes, so it looks filled
                        float[] scales = { 1f, 0.85f, 0.7f, 0.55f, 0.4f, 0.25f, 0.1f };
                        Vector2[][] filled = new Vector2[scales.Length][];
                        for (int i = 0; i < scales.Length; i++)
                            filled[i] = StarPoints(scales[i]);
                        return filled;
                    }
                default: // "next"
                    return new[]
                    {
                        new[] { new Vector2(-0.65f, 0f), new Vector2(0.65f, 0f) },
                        new[] { new Vector2(0.25f, 0.4f), new Vector2(0.65f, 0f), new Vector2(0.25f, -0.4f) }
                    };
            }
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static Sprite GetIcon(string name)
        {
            Sprite sprite;
            if (iconCache.TryGetValue(name, out sprite) && sprite != null)
                return sprite;

            const int size = 64;
            const float thickness = 0.085f; // line thickness, in the icon's -1..1 space
            Vector2[][] strokes = IconStrokes(name);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float d = float.MaxValue;

                    foreach (Vector2[] stroke in strokes)
                        for (int i = 0; i < stroke.Length - 1; i++)
                            d = Mathf.Min(d, SegmentDistance(p, stroke[i], stroke[i + 1]));

                    float alpha = Mathf.Clamp01((thickness - d) / 0.04f + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();

            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            iconCache[name] = sprite;
            return sprite;
        }

        // Same placement rules as CreateLabel: position and size are in world units relative to the menu
        private static void CreateIcon(string name, float centerY, float centerZ, float size, Color color)
        {
            GameObject obj = new GameObject("icon_" + name);
            obj.transform.parent = canvasObject.transform;

            UnityEngine.UI.Image image = obj.AddComponent<UnityEngine.UI.Image>();
            image.sprite = GetIcon(name);
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = image.GetComponent<RectTransform>();
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = new Vector2(size, size);
            rect.localPosition = new Vector3(0.064f, centerY, centerZ);
            rect.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
        }

        // Functions
        public static void CreateMenu()
        {
            Vector3 holderScale = HolderScale;

            // Panel size in world units. On screen the menu's +Y side is the LEFT side, +Z is up.
            float panelWidth = PanelWidth;
            float panelHeight = PanelHeight;
            float panelBorder = outlineEnabled ? PanelOutlineWidth : 0f;
            float dividerX = 0.05f + (menuSize.x + 0.005f) * holderScale.x / 2f + 0.0002f; // just in front of the panel

            // Menu Holder
            menu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(menu.GetComponent<Rigidbody>());
            Destroy(menu.GetComponent<BoxCollider>());
            Destroy(menu.GetComponent<Renderer>());
            menu.transform.localScale = holderScale;

            // Menu Background. Two layers, both made with RoundedMesh like the buttons:
            // the outer one has the outline color (and the collider you grab with the mouse),
            // the inner one has the real panel color and is a bit smaller.
            menuBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(menuBackground.GetComponent<Rigidbody>());
            if (!keyboardMenuOpen)
                Destroy(menuBackground.GetComponent<BoxCollider>());
            menuBackground.transform.parent = menu.transform;
            menuBackground.transform.rotation = Quaternion.identity;
            menuBackground.transform.localScale = new Vector3(menuSize.x, panelWidth / holderScale.y, panelHeight / holderScale.z);
            menuBackground.GetComponent<Renderer>().material.color = outlineEnabled ? (Color)ExtGradient.OutlineBase : backgroundColor.colors[0].color;
            menuBackground.transform.position = new Vector3(0.05f, 0f, 0f);
            RoundedMesh.Apply(menuBackground, PanelRadius);

            // Keyboard mode: the panel needs a collider so you can grab it with the mouse and drag it
            if (keyboardMenuOpen)
            {
                BoxCollider backgroundCollider = menuBackground.GetComponent<BoxCollider>();
                if (backgroundCollider == null)
                    backgroundCollider = menuBackground.AddComponent<BoxCollider>();

                backgroundCollider.isTrigger = true;
            }

            GameObject panelFill = menuBackground;
            if (outlineEnabled)
            {
                panelFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(panelFill.GetComponent<Rigidbody>());
                Destroy(panelFill.GetComponent<Collider>());
                panelFill.transform.parent = menu.transform;
                panelFill.transform.rotation = Quaternion.identity;
                panelFill.transform.localScale = new Vector3(menuSize.x + 0.005f, panelWidth / holderScale.y - panelBorder * 2f / holderScale.y, panelHeight / holderScale.z - panelBorder * 2f / holderScale.z);
                panelFill.GetComponent<Renderer>().material.color = backgroundColor.colors[0].color;
                panelFill.transform.position = new Vector3(0.05f, 0f, 0f);
                RoundedMesh.Apply(panelFill, PanelRadius - panelBorder);
            }

            ColorChanger colorChanger = panelFill.AddComponent<ColorChanger>();
            colorChanger.colors = backgroundColor;

            // Canvas
            canvasObject = new GameObject();
            canvasObject.transform.parent = menu.transform;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasScaler.dynamicPixelsPerUnit = 1000f;

            Color subtext = ExtGradient.SubtextBase;

            float left = panelWidth / 2f - Pad;
            float right = -(panelWidth / 2f - Pad);
            float innerWidth = left - right;
            float top = panelHeight / 2f - Pad;
            float bottom = -panelHeight / 2f + Pad;

            // What is shown in the list (current category, favorites, or the search results)
            ButtonInfo[] visible = GetVisibleButtons();
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(visible.Length / (float)RowsPerPage));
            if (pageNumber > totalPages - 1)
                pageNumber = totalPages - 1;
            if (pageNumber < 0)
                pageNumber = 0;

            // ----- Header: icon buttons on the right (leave, then search next to it) -----
            float headerZ = top - HeaderHeight / 2f;
            int slot = 0;

            if (disconnectButton)
            {
                float leaveY = right + HeaderButton / 2f;
                CreateRoundedButton("Disconnect", leaveY, headerZ, HeaderButton, HeaderButton, ExtGradient.Disconnect(), IconRoundness);
                CreateIcon("leave", leaveY, headerZ, HeaderButton * IconScale, textColors[0]);
                slot++;
            }

            float searchY = right + HeaderButton / 2f + slot * (HeaderButton + Gap);
            CreateRoundedButton("Search", searchY, headerZ, HeaderButton, HeaderButton, ExtGradient.Search(), IconRoundness);
            CreateIcon("search", searchY, headerZ, HeaderButton * IconScale, textColors[0]);
            slot++;

            // ----- Header: title (or what you are typing while searching) + FPS / page under it -----
            float titleWidth = innerWidth - slot * (HeaderButton + Gap) - Gap;

            string titleText;
            Color titleColor;
            if (!searchActive)
            {
                titleText = currentCategory == 0 ? PluginInfo.Name : GetCategoryName();
                titleColor = textColors[0];
            }
            else if (searchQuery.Length == 0)
            {
                titleText = "type to search...";
                titleColor = subtext;
            }
            else
            {
                titleText = searchQuery + "_";
                titleColor = textColors[0];
            }

            Text title = CreateLabel(titleText, left - titleWidth / 2f, top - HeaderHeight * 0.30f, titleWidth, HeaderHeight * 0.42f, titleColor, TextAnchor.MiddleLeft);
            title.fontStyle = (searchActive && searchQuery.Length == 0) ? FontStyle.Italic : FontStyle.Bold;

            float infoZ = top - HeaderHeight * 0.80f;
            float infoHeight = HeaderHeight * 0.26f;
            float fpsWidth = 0.06f;
            float pageWidth = 0.07f;

            if (fpsCounter)
            {
                fpsObject = CreateLabel("FPS: " + Mathf.Ceil(1f / Time.unscaledDeltaTime).ToString(), left - fpsWidth / 2f, infoZ, fpsWidth, infoHeight, subtext, TextAnchor.MiddleLeft);
                fpsObject.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
                fpsObject = null;

            float pageOffset = fpsCounter ? fpsWidth + Gap * 2f : 0f;
            CreateLabel("page " + (pageNumber + 1).ToString() + "/" + totalPages.ToString(), left - pageOffset - pageWidth / 2f, infoZ, pageWidth, infoHeight, subtext, TextAnchor.MiddleLeft);

            // Thin line under the header
            float cursor = top - HeaderHeight - Gap / 2f;
            CreateDivider(cursor, innerWidth, dividerX);
            cursor -= Gap;
            float listTop = cursor;

            // ----- Footer: line, then prev (left), home (middle), next (right) -----
            float footerZ = bottom + FooterHeight / 2f;
            float footerLineZ = bottom + FooterHeight + Gap;
            float listBottom = footerLineZ + Gap;

            CreateDivider(footerLineZ, innerWidth, dividerX);

            float prevY = left - FooterHeight / 2f;     // +Y is the left side of the screen
            float nextY = right + FooterHeight / 2f;

            CreateRoundedButton("PreviousPage", prevY, footerZ, FooterHeight, FooterHeight, 0, IconRoundness);
            CreateIcon("prev", prevY, footerZ, FooterHeight * IconScale, textColors[0]);

            CreateRoundedButton("NextPage", nextY, footerZ, FooterHeight, FooterHeight, 0, IconRoundness);
            CreateIcon("next", nextY, footerZ, FooterHeight * IconScale, textColors[0]);

            // Home only shows when there is somewhere to return from. Remove the if to show it on the main page too.
            if (currentCategory != 0 || searchActive)
            {
                CreateRoundedButton("ReturnHome", 0f, footerZ, FooterHeight, FooterHeight, 0, IconRoundness);
                CreateIcon("home", 0f, footerZ, FooterHeight * IconScale, textColors[0]);
            }

            // ----- Rows -----
            float rowHeight = ((listTop - listBottom) - Gap * (RowsPerPage - 1)) / RowsPerPage;

            ButtonInfo[] activeButtons = visible.Skip(pageNumber * RowsPerPage).Take(RowsPerPage).ToArray();
            for (int i = 0; i < activeButtons.Length; i++)
                CreateButton(listTop - rowHeight / 2f - i * (rowHeight + Gap), rowHeight, innerWidth, activeButtons[i]);

            // Enabled mods list (beside the panel). While searching it moves past the keyboard.
            if (arrayList)
                CreateArrayList(panelWidth, top, searchActive ? panelWidth + Gap : 0f);

            // On-screen keyboard on the right side of the panel while searching
            if (searchActive)
                CreateKeyboard(panelWidth, panelHeight);
        }

        public static void CreateButton(float centerZ, float height, float width, ButtonInfo method)
        {
            // Only mods (toggles) get a star. Categories and one-shot buttons don't.
            bool showStar = CanFavorite(method);

            // Buttons whose method calls a "cycle" method get back / forward arrows
            bool showCycle = IsCycleButton(method);

            // The row keeps its full (old) size.
            GameObject row = CreateRoundedButton(method.buttonText, 0f, centerZ, width, height, method.enabled ? 1 : 0);

            float starZone = showStar ? StarZoneWidth : 0f;
            float cycleZone = showCycle ? CycleZoneWidth : 0f;
            float reserved = starZone + cycleZone;

            // The star and the cycle arrows sit inside the row at the right end. To stop your hand from pressing the row
            // and them together in VR, the row's collider is made shorter so it doesn't cover that area.
            float starCenterY = -(width / 2f) + StarZoneWidth / 2f;   // +Y is the left side of the screen
            if (reserved > 0f)
            {
                BoxCollider rowCollider = row.GetComponent<BoxCollider>();
                if (rowCollider != null)
                {
                    float fraction = (width - reserved - (showCycle ? CycleColliderGap : 0f)) / width;
                    rowCollider.size = new Vector3(1f, fraction, 1f);
                    rowCollider.center = new Vector3(0f, (1f - fraction) / 2f, 0f);
                }
            }

            Color subtext = ExtGradient.SubtextBase;
            float rightReserve = reserved > 0f ? reserved : RowArrowWidth;
            float labelWidth = width - RowTextPad - rightReserve - RowTextPad;
            float labelY = width / 2f - RowTextPad - labelWidth / 2f;

            string titleText = method.overlapText != null ? method.overlapText : method.buttonText;
            bool hasDescription = !string.IsNullOrEmpty(method.toolTip);

            // Title (bold, bigger). Centered when there is no description.
            Text titleLabel = CreateLabel(
                titleText,
                labelY, hasDescription ? centerZ + height * 0.17f : centerZ,
                labelWidth, height * (hasDescription ? 0.30f : LabelScale),
                method.enabled ? textColors[1] : textColors[0],
                TextAnchor.MiddleLeft);
            titleLabel.fontStyle = FontStyle.Bold;

            // Description (small) = the button's toolTip
            if (hasDescription)
            {
                CreateLabel(
                    method.toolTip,
                    labelY, centerZ - height * 0.22f,
                    labelWidth, height * 0.17f,
                    subtext,
                    TextAnchor.MiddleLeft);
            }

            // ">" on the right for rows that open something (categories)
            if (!showCycle && IsCategoryButton(method))
            {
                CreateLabel(
                    ">",
                    -(width / 2f - RowTextPad) + RowArrowWidth / 2f, centerZ, RowArrowWidth, height * LabelScale,
                    textColors[0],
                    TextAnchor.MiddleRight);
            }

            // Cycle arrows: back on the left, forward on the right, just left of the star
            if (showCycle)
            {
                float zoneStart = -(width / 2f) + starZone;
                float fwdY = zoneStart + 0.002f + CycleArrowSize / 2f;
                float backY = fwdY + CycleArrowSize + CycleArrowGap;

                CreateIcon("next", fwdY, centerZ, CycleArrowSize, textColors[0]);
                CreateHitbox("cyc:fwd:" + method.buttonText, fwdY, centerZ, CycleArrowSize, height);

                CreateIcon("prev", backY, centerZ, CycleArrowSize, textColors[0]);
                CreateHitbox("cyc:back:" + method.buttonText, backY, centerZ, CycleArrowSize, height);
            }

            // Star at the end of the row: outline = not favorited, filled yellow = favorited
            if (showStar)
            {
                bool fav = IsFavorite(method.buttonText);
                CreateIcon(fav ? "starfilled" : "star", starCenterY, centerZ, StarIconSize, fav ? new Color(1f, 0.82f, 0.2f) : textColors[0]);
                CreateHitbox("fav:" + method.buttonText, starCenterY, centerZ, StarZoneWidth, height);
            }
        }

        // An invisible clickable box (used for the star and the cycle arrows, it has no look of its own, the icon is drawn on the row).
        private static void CreateHitbox(string relatedText, float centerY, float centerZ, float width, float height)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!keyboardMenuOpen)
                box.layer = 2;

            Destroy(box.GetComponent<Rigidbody>());
            Destroy(box.GetComponent<Renderer>());
            box.GetComponent<BoxCollider>().isTrigger = true;
            box.transform.parent = menu.transform;
            box.transform.rotation = Quaternion.identity;
            box.transform.localScale = new Vector3(0.09f, width / HolderScale.y, height / HolderScale.z);
            box.transform.localPosition = new Vector3(0.56f, centerY / HolderScale.y, centerZ / HolderScale.z);
            box.AddComponent<Classes.Button>().relatedText = relatedText;
        }

        // The search keyboard (Untitled style), placed on the RIGHT side of the panel, top aligned with it:
        // header, input line with a purple underline, dark rounded keys, and a bottom row with clear / space / purple backspace.
        private static void CreateKeyboard(float panelWidth, float panelHeight)
        {
            const float keyGap = 0.003f;
            const float kbPad = 0.008f;
            const float headerH = 0.014f;
            const float inputH = 0.022f;
            const int rows = 5;

            float kbWidth = panelWidth;
            float keyW = (kbWidth - kbPad * 2f - keyGap * 9f) / 10f;
            float keyH = keyW * 1.15f;
            float kbHeight = kbPad * 2f + headerH + inputH + keyGap * 2f + rows * keyH + (rows - 1) * keyGap;

            // Right of the panel (+Y is the LEFT side of the screen, so the right side is negative Y)
            float kbCenterY = -(panelWidth / 2f + Gap + kbWidth / 2f);
            float kbTop = panelHeight / 2f;                 // top edge lines up with the top of the panel
            float kbCenterZ = kbTop - kbHeight / 2f;
            float plateRadius = PanelRadius;

            // Backing plate (outline color, then the panel color a bit smaller)
            if (outlineEnabled)
            {
                CreatePlate(kbCenterY, kbCenterZ, kbWidth, kbHeight, (Color)ExtGradient.OutlineBase, plateRadius, 0.54f, 0.05f);
                CreatePlate(kbCenterY, kbCenterZ, kbWidth - PanelOutlineWidth * 2f, kbHeight - PanelOutlineWidth * 2f, backgroundColor.colors[0].color, plateRadius - PanelOutlineWidth, 0.54f, 0.055f);
            }
            else
                CreatePlate(kbCenterY, kbCenterZ, kbWidth, kbHeight, backgroundColor.colors[0].color, plateRadius, 0.54f, 0.05f);

            Color subtext = ExtGradient.SubtextBase;
            float textWidth = kbWidth - kbPad * 2f;

            // Header: "Unn4meD / search"
            float headerZ = kbTop - kbPad - headerH / 2f;
            Text header = CreateLabel("Unn4meD / search", kbCenterY, headerZ, textWidth, headerH, textColors[0], TextAnchor.MiddleLeft);
            header.fontStyle = FontStyle.Bold;

            // Input line: what you typed, with a purple underline
            float inputZ = kbTop - kbPad - headerH - inputH / 2f;
            float inputBottom = kbTop - kbPad - headerH - inputH;

            if (searchQuery.Length == 0)
            {
                Text placeholder = CreateLabel("search mods, settings...", kbCenterY, inputZ, textWidth, inputH * 0.6f, subtext, TextAnchor.MiddleLeft);
                placeholder.fontStyle = FontStyle.Italic;
            }
            else
                CreateLabel(searchQuery + "_", kbCenterY, inputZ, textWidth, inputH * 0.6f, textColors[0], TextAnchor.MiddleLeft);

            CreatePlate(kbCenterY, inputBottom - 0.001f, textWidth, 0.0012f, (Color)ExtGradient.AccentBase, 0.0006f, 0.57f, 0.01f);

            // Keys
            float keysTop = inputBottom - keyGap * 2f;
            string[] letterRows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };

            for (int r = 0; r < letterRows.Length; r++)
            {
                string row = letterRows[r];
                float rowZ = keysTop - keyH / 2f - r * (keyH + keyGap);
                float rowTotal = row.Length * keyW + (row.Length - 1) * keyGap;

                for (int i = 0; i < row.Length; i++)
                {
                    float keyY = kbCenterY + rowTotal / 2f - keyW / 2f - i * (keyW + keyGap); // +Y is the left side of the screen
                    string letter = row[i].ToString();

                    CreateRoundedButton("key:" + letter.ToUpper(), keyY, rowZ, keyW, keyH, ExtGradient.Key(), ButtonRoundness);
                    Text keyLabel = CreateLabel(letter, keyY, rowZ, keyW, keyH * 0.5f, textColors[0], TextAnchor.MiddleCenter);
                    keyLabel.fontStyle = FontStyle.Bold;
                }
            }

            // Bottom row: clear (2 keys wide), space (6), purple backspace (2)
            float bottomZ = keysTop - keyH / 2f - (rows - 1) * (keyH + keyGap);
            float leftEdge = (kbWidth - kbPad * 2f) / 2f;

            string[] names = { "clear", "space", "\u2190" };
            string[] ids = { "key:CLEAR", "key:SPACE", "key:BACK" };
            int[] units = { 2, 6, 2 };
            ExtGradient[] gradients = { ExtGradient.Key(), ExtGradient.Key(), ExtGradient.Accent() };
            int start = 0;

            for (int i = 0; i < 3; i++)
            {
                float width = units[i] * keyW + (units[i] - 1) * keyGap;
                float segmentLeft = leftEdge - start * (keyW + keyGap);
                float segmentY = kbCenterY + segmentLeft - width / 2f;

                CreateRoundedButton(ids[i], segmentY, bottomZ, width, keyH, gradients[i], ButtonRoundness);
                Text keyLabel = CreateLabel(names[i], segmentY, bottomZ, width, keyH * 0.5f, textColors[0], TextAnchor.MiddleCenter);
                keyLabel.fontStyle = FontStyle.Bold;

                start += units[i];
            }
        }

        // A rounded box that is not clickable (backing for the keyboard)
        private static void CreatePlate(float centerY, float centerZ, float width, float height, Color color, float radius, float localX, float localThickness)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!keyboardMenuOpen)
                plate.layer = 2;

            Destroy(plate.GetComponent<Rigidbody>());
            Destroy(plate.GetComponent<Collider>());
            plate.transform.parent = menu.transform;
            plate.transform.rotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(localThickness, width / HolderScale.y, height / HolderScale.z);
            plate.transform.localPosition = new Vector3(localX, centerY / HolderScale.y, centerZ / HolderScale.z);
            plate.GetComponent<Renderer>().material.color = color;

            RoundedMesh.Apply(plate, radius);
        }

        // Creates a rounded, clickable box with a thin outline. Position and size are in world units relative to the menu.
        private static GameObject CreateRoundedButton(string relatedText, float centerY, float centerZ, float width, float height, int colorIndex, float roundness = ButtonRoundness)
        {
            return CreateRoundedButton(relatedText, centerY, centerZ, width, height, buttonColors[colorIndex], roundness);
        }

        private static GameObject CreateRoundedButton(string relatedText, float centerY, float centerZ, float width, float height, ExtGradient gradient, float roundness = ButtonRoundness)
        {
            Vector3 holderScale = HolderScale;
            float border = outlineEnabled ? OutlineWidth : 0f;

            // Outer layer: this is the clickable one. With outlines on it has the outline color
            // and the real color sits on top of it, a bit smaller. Both use RoundedMesh the same way.
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!keyboardMenuOpen)
                gameObject.layer = 2;

            Destroy(gameObject.GetComponent<Rigidbody>());
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            gameObject.transform.parent = menu.transform;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(0.09f, width / holderScale.y, height / holderScale.z);
            gameObject.transform.localPosition = new Vector3(0.56f, centerY / holderScale.y, centerZ / holderScale.z);
            gameObject.GetComponent<Renderer>().material.color = outlineEnabled ? (Color)ExtGradient.OutlineBase : gradient.colors[0].color;
            gameObject.AddComponent<Classes.Button>().relatedText = relatedText;

            RoundedMesh.Apply(gameObject, height * roundness);

            if (!outlineEnabled)
            {
                ColorChanger colorChanger = gameObject.AddComponent<ColorChanger>();
                colorChanger.colors = gradient;
                return gameObject;
            }

            // Inner layer: same shape, smaller by the border width on every side, a hair in front
            float innerWidth = width - border * 2f;
            float innerHeight = height - border * 2f;

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!keyboardMenuOpen)
                fill.layer = 2;

            Destroy(fill.GetComponent<Rigidbody>());
            Destroy(fill.GetComponent<Collider>());
            fill.transform.parent = menu.transform;
            fill.transform.rotation = Quaternion.identity;
            fill.transform.localScale = new Vector3(0.095f, innerWidth / holderScale.y, innerHeight / holderScale.z);
            fill.transform.localPosition = new Vector3(0.56f, centerY / holderScale.y, centerZ / holderScale.z);
            fill.GetComponent<Renderer>().material.color = gradient.colors[0].color;

            ColorChanger fillChanger = fill.AddComponent<ColorChanger>();
            fillChanger.colors = gradient;

            RoundedMesh.Apply(fill, innerHeight * roundness);

            return gameObject;
        }

        // Thin flat line (under the header, above the footer). Plain box, no rounding needed.
        private static void CreateDivider(float centerZ, float width, float worldX)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(line.GetComponent<Rigidbody>());
            Destroy(line.GetComponent<Collider>());

            line.transform.SetParent(menu.transform, false);
            line.transform.localRotation = Quaternion.identity;
            line.transform.localScale = new Vector3(0.004f, width / HolderScale.y, DividerHeight / HolderScale.z);
            line.transform.localPosition = new Vector3(worldX / HolderScale.x, 0f, centerZ / HolderScale.z);
            line.GetComponent<Renderer>().material.color = ExtGradient.DividerBase;
        }

        // Lists every enabled toggle mod, longest name first, to the right of the panel.
        // extraOffset pushes it further right (used while the search keyboard is open).
        private static void CreateArrayList(float panelWidth, float top, float extraOffset = 0f)
        {
            string[] names = buttons
                .SelectMany(list => list)
                .Where(b => b.enabled && b.isTogglable)
                .Select(b => b.buttonText.ToUpper())
                .OrderByDescending(n => n.Length)
                .ToArray();

            if (names.Length == 0)
                return;

            float width = 0.16f;
            float height = 0.012f * names.Length;

            CreateLabel(
                string.Join("\n", names.Select(n => "\u2022 " + n)),
                -(panelWidth / 2f) - 0.01f - extraOffset - width / 2f,
                top - height / 2f,
                width, height,
                textColors[1],
                TextAnchor.UpperLeft);
        }

        // Creates a text label. Position and size are in world units relative to the menu.
        private static Text CreateLabel(string content, float centerY, float centerZ, float width, float height, Color color, TextAnchor anchor)
        {
            Text label = new GameObject
            {
                transform =
                {
                    parent = canvasObject.transform
                }
            }.AddComponent<Text>();
            label.font = currentFont;
            label.text = content;
            label.supportRichText = true;
            label.fontSize = 1;
            label.color = color;
            label.alignment = anchor;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 0;

            RectTransform rect = label.GetComponent<RectTransform>();
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.localPosition = new Vector3(0.064f, centerY, centerZ);
            rect.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            return label;
        }

        public static void RecreateMenu()
        {
            if (menu != null)
            {
                Destroy(menu);
                menu = null;

                CreateMenu();
                RecenterMenu(rightHanded, keyboardMenuOpen);
            }
        }

        public static void RecenterMenu(bool isRightHanded, bool isKeyboardCondition)
        {
            if (!isKeyboardCondition)
            {
                if (!isRightHanded)
                {
                    menu.transform.position = GorillaTagger.Instance.leftHandTransform.position;
                    menu.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation;
                }
                else
                {
                    menu.transform.position = GorillaTagger.Instance.rightHandTransform.position;
                    Vector3 rotation = GorillaTagger.Instance.rightHandTransform.rotation.eulerAngles;
                    rotation += new Vector3(0f, 0f, 180f);
                    menu.transform.rotation = Quaternion.Euler(rotation);
                }

                // Floating search: fly from the hand pose to the fixed spot in the air (and back)
                if (floatBlend > 0f)
                {
                    float t = SmootherStep(floatBlend);
                    menu.transform.position = Vector3.Lerp(menu.transform.position, floatPosition, t);
                    menu.transform.rotation = Quaternion.Slerp(menu.transform.rotation, floatRotation, t);
                }
            }
            else
            {
                if (TPC == null)
                {
                    try
                    {
                        TPC = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera").GetComponent<Camera>();
                    }
                    catch { }
                }

                if (TPC != null)
                {
                    // The game can lock the cursor, so unlock it while the menu is open
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;

                    if (menu.transform.parent != TPC.transform)
                        menu.transform.parent = TPC.transform;

                    // ----- Mouse: press buttons, or hold the panel to drag it -----
                    if (Mouse.current != null)
                    {
                        bool leftPressedNow = Mouse.current.leftButton.wasPressedThisFrame;
                        bool leftHeld = Mouse.current.leftButton.isPressed;
                        Vector2 mousePosition = Mouse.current.position.ReadValue();

                        // Letting go of the mouse always ends the drag
                        if (!leftHeld)
                            draggingMenu = false;

                        // Where the cursor is, measured on the plane the menu floats on (screen right / up, in world units)
                        Ray ray = TPC.ScreenPointToRay(mousePosition);
                        float facing = Mathf.Max(0.0001f, Vector3.Dot(ray.direction, TPC.transform.forward));
                        Vector3 cursorPoint = ray.origin + ray.direction * (MenuDistance / facing);
                        Vector3 fromCamera = cursorPoint - TPC.transform.position;
                        Vector2 cursorOffset = new Vector2(
                            Vector3.Dot(fromCamera, TPC.transform.right),
                            Vector3.Dot(fromCamera, TPC.transform.up) - MenuVerticalNudge);

                        if (leftHeld)
                        {
                            // Look at everything the ray passes through, so your own body can't block the click
                            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
                            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                            Classes.Button hitButton = null;
                            bool hitPanel = false;

                            foreach (RaycastHit hit in hits)
                            {
                                Classes.Button candidate = hit.collider.GetComponent<Classes.Button>();
                                if (candidate != null && hitButton == null)
                                    hitButton = candidate;

                                if (menuBackground != null && hit.collider.gameObject == menuBackground)
                                    hitPanel = true;
                            }

                            if (!draggingMenu && hitButton != null)
                            {
                                // Clicking a button presses it
                                hitButton.OnTriggerEnter(buttonCollider);
                            }
                            else if (leftPressedNow && hitPanel && hitButton == null)
                            {
                                // Started holding on the empty panel: grab it right where the cursor is
                                draggingMenu = true;
                                dragGrabOffset = menuScreenOffset - cursorOffset;
                            }
                        }

                        // Only while you are holding it, the menu follows the cursor
                        if (draggingMenu && leftHeld)
                        {
                            menuScreenOffset = cursorOffset + dragGrabOffset;

                            // Keep the menu's center on screen
                            float halfHeight = MenuDistance * Mathf.Tan(TPC.fieldOfView * 0.5f * Mathf.Deg2Rad);
                            float halfWidth = halfHeight * ((float)Screen.width / Mathf.Max(1, Screen.height));
                            menuScreenOffset.x = Mathf.Clamp(menuScreenOffset.x, -halfWidth, halfWidth);
                            menuScreenOffset.y = Mathf.Clamp(menuScreenOffset.y, -halfHeight, halfHeight);
                        }

                        if (reference != null && !leftHeld)
                            reference.transform.position = new Vector3(999f, -999f, -999f);
                    }

                    menu.transform.position = TPC.transform.position
                        + (TPC.transform.forward * MenuDistance)
                        + (TPC.transform.up * (MenuVerticalNudge + menuScreenOffset.y))
                        + (TPC.transform.right * menuScreenOffset.x);
                    menu.transform.rotation = TPC.transform.rotation * Quaternion.Euler(-90f, 90f, 0f);
                }
            }
        }

        public static void CreateReference(bool isRightHanded)
        {
            reference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reference.transform.parent = isRightHanded ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
            reference.GetComponent<Renderer>().material.color = backgroundColor.colors[0].color;
            reference.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            reference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            buttonCollider = reference.GetComponent<SphereCollider>();

            ColorChanger colorChanger = reference.AddComponent<ColorChanger>();
            colorChanger.colors = backgroundColor;
        }

        public static void Toggle(string buttonText)
        {
            // Every press in the menu plays the click sound that is picked in the settings
            PlayClickSound();

            // Search button: start / stop searching
            if (buttonText == "Search")
            {
                if (searchActive)
                    CloseSearch();
                else
                {
                    searchActive = true;
                    searchQuery = "";
                    pageNumber = 0;
                }

                RecreateMenu();
                return;
            }

            // Home button at the bottom: same as the "Return to Main" button
            if (buttonText == "ReturnHome")
            {
                if (searchActive)
                    CloseSearch();

                currentCategory = 0;
                RecreateMenu();
                return;
            }

            // On-screen keyboard keys
            if (buttonText.StartsWith("key:"))
            {
                HandleKey(buttonText.Substring(4));
                return;
            }

            // Star on a row: add / remove it from the favorites
            if (buttonText.StartsWith("fav:"))
            {
                ToggleFavorite(buttonText.Substring(4));
                RecreateMenu();
                return;
            }

            // Arrows on a cycle row: run that button's cycle method forward ("cyc:fwd:") or back ("cyc:back:")
            if (buttonText.StartsWith("cyc:"))
            {
                bool forward = buttonText.StartsWith("cyc:fwd:");
                ButtonInfo cycleTarget = GetIndex(buttonText.Substring(forward ? 8 : 9));

                if (cycleTarget != null && cycleTarget.method != null)
                    RunCycle(cycleTarget, forward);

                RecreateMenu();
                return;
            }

            int visibleCount = GetVisibleButtons().Length;
            int rowsPerPage = RowsPerPage;
            int lastPage = Mathf.Max(0, ((visibleCount + rowsPerPage - 1) / rowsPerPage) - 1);

            if (buttonText == "PreviousPage")
            {
                pageNumber--;
                if (pageNumber < 0)
                    pageNumber = lastPage;
            }
            else
            {
                if (buttonText == "NextPage")
                {
                    pageNumber++;
                    if (pageNumber > lastPage)
                        pageNumber = 0;
                }
                else
                {
                    ButtonInfo target = GetIndex(buttonText);
                    if (target != null)
                    {
                        if (target.isTogglable)
                        {
                            target.enabled = !target.enabled;
                            if (target.enabled)
                            {
                                NotifiLib.SendNotification("<color=grey>[</color><color=green>ENABLE</color><color=grey>]</color> " + target.toolTip);
                                if (target.enableMethod != null)
                                    try { target.enableMethod.Invoke(); } catch { }
                            }
                            else
                            {
                                NotifiLib.SendNotification("<color=grey>[</color><color=red>DISABLE</color><color=grey>]</color> " + target.toolTip);
                                if (target.disableMethod != null)
                                    try { target.disableMethod.Invoke(); } catch { }
                            }
                        }
                        else
                        {
                            NotifiLib.SendNotification("<color=grey>[</color><color=green>ENABLE</color><color=grey>]</color> " + target.toolTip);
                            if (target.method != null)
                                try { target.method.Invoke(); } catch { }

                            // Opening a category / running a one-shot button ends the search
                            if (searchActive)
                                CloseSearch();
                        }
                    }
                    else
                        Debug.LogError(buttonText + " does not exist");
                }
            }
            RecreateMenu();
        }

        private static readonly Dictionary<string, (int Category, int Index)> cacheGetIndex = new Dictionary<string, (int Category, int Index)>(); // Looping through 800 elements is not a light task :/
        public static ButtonInfo GetIndex(string buttonText)
        {
            if (buttonText == null)
                return null;

            if (cacheGetIndex.ContainsKey(buttonText))
            {
                var CacheData = cacheGetIndex[buttonText];
                try
                {
                    if (buttons[CacheData.Category][CacheData.Index].buttonText == buttonText)
                        return buttons[CacheData.Category][CacheData.Index];
                }
                catch { cacheGetIndex.Remove(buttonText); }
            }

            int categoryIndex = 0;
            foreach (ButtonInfo[] buttons in buttons)
            {
                int buttonIndex = 0;
                foreach (ButtonInfo button in buttons)
                {
                    if (button.buttonText == buttonText)
                    {
                        try
                        {
                            cacheGetIndex.Add(buttonText, (categoryIndex, buttonIndex));
                        }
                        catch
                        {
                            if (cacheGetIndex.ContainsKey(buttonText))
                                cacheGetIndex.Remove(buttonText);
                        }

                        return button;
                    }
                    buttonIndex++;
                }
                categoryIndex++;
            }

            return null;
        }

        public static Vector3 RandomVector3(float range = 1f) =>
            new Vector3(UnityEngine.Random.Range(-range, range),
                        UnityEngine.Random.Range(-range, range),
                        UnityEngine.Random.Range(-range, range));

        public static Quaternion RandomQuaternion(float range = 360f) =>
            Quaternion.Euler(UnityEngine.Random.Range(0f, range),
                        UnityEngine.Random.Range(0f, range),
                        UnityEngine.Random.Range(0f, range));

        public static Color RandomColor(byte range = 255, byte alpha = 255) =>
            new Color32((byte)UnityEngine.Random.Range(0, range),
                        (byte)UnityEngine.Random.Range(0, range),
                        (byte)UnityEngine.Random.Range(0, range),
                        alpha);

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueLeftHand()
        {
            Quaternion rot = GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handRotOffset;
            return (GorillaTagger.Instance.leftHandTransform.position + GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
        }

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueRightHand()
        {
            Quaternion rot = GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handRotOffset;
            return (GorillaTagger.Instance.rightHandTransform.position + GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
        }

        public static void WorldScale(GameObject obj, Vector3 targetWorldScale)
        {
            Vector3 parentScale = obj.transform.parent.lossyScale;
            obj.transform.localScale = new Vector3(
                targetWorldScale.x / parentScale.x,
                targetWorldScale.y / parentScale.y,
                targetWorldScale.z / parentScale.z
            );
        }

        public static void FixStickyColliders(GameObject platform)
        {
            Vector3[] localPositions = new Vector3[]
            {
                new Vector3(0, 1f, 0),
                new Vector3(0, -1f, 0),
                new Vector3(1f, 0, 0),
                new Vector3(-1f, 0, 0),
                new Vector3(0, 0, 1f),
                new Vector3(0, 0, -1f)
            };
            Quaternion[] localRotations = new Quaternion[]
            {
                Quaternion.Euler(90, 0, 0),
                Quaternion.Euler(-90, 0, 0),
                Quaternion.Euler(0, -90, 0),
                Quaternion.Euler(0, 90, 0),
                Quaternion.identity,
                Quaternion.Euler(0, 180, 0)
            };
            for (int i = 0; i < localPositions.Length; i++)
            {
                GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    if (platform.GetComponent<GorillaSurfaceOverride>() != null)
                    {
                        side.AddComponent<GorillaSurfaceOverride>().overrideIndex = platform.GetComponent<GorillaSurfaceOverride>().overrideIndex;
                    }
                }
                catch { }
                float size = 0.025f;
                side.transform.SetParent(platform.transform);
                side.transform.position = localPositions[i] * (size / 2);
                side.transform.rotation = localRotations[i];
                WorldScale(side, new Vector3(size, size, 0.01f));
                side.GetComponent<Renderer>().enabled = false;
            }
        }

        private static int? noInvisLayerMask;
        public static int NoInvisLayerMask()
        {
            noInvisLayerMask ??= ~(
                1 << LayerMask.NameToLayer("TransparentFX") |
                1 << LayerMask.NameToLayer("Ignore Raycast") |
                1 << LayerMask.NameToLayer("Zone") |
                1 << LayerMask.NameToLayer("Gorilla Trigger") |
                1 << LayerMask.NameToLayer("Gorilla Boundary") |
                1 << LayerMask.NameToLayer("GorillaCosmetics") |
                1 << LayerMask.NameToLayer("GorillaParticle"));

            return noInvisLayerMask ?? GTPlayer.Instance.locomotionEnabledLayers;
        }

        public static bool gunLocked;
        public static VRRig lockTarget;

        public static (RaycastHit Ray, GameObject NewPointer) RenderGun(int? overrideLayerMask = null)
        {
            Transform GunTransform = GorillaTagger.Instance.rightHandTransform;

            Vector3 StartPosition = GunTransform.position;
            Vector3 Direction = GunTransform.forward;

            Physics.Raycast(StartPosition + Direction / 4f, Direction, out var Ray, 512f, overrideLayerMask ?? NoInvisLayerMask());
            Vector3 EndPosition = gunLocked ? lockTarget.transform.position : Ray.point;

            if (EndPosition == Vector3.zero)
                EndPosition = StartPosition + Direction * 512f;

            if (GunPointer == null)
                GunPointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            GunPointer.SetActive(true);
            GunPointer.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            GunPointer.transform.position = EndPosition;

            Renderer PointerRenderer = GunPointer.GetComponent<Renderer>();
            PointerRenderer.material.shader = Shader.Find("GUI/Text Shader");
            PointerRenderer.material.color = gunLocked || ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f ? buttonColors[1].GetCurrentColor() : buttonColors[0].GetCurrentColor();

            Destroy(GunPointer.GetComponent<Collider>());

            if (GunLine == null)
            {
                GameObject line = new GameObject("iiMenu_GunLine");
                GunLine = line.AddComponent<LineRenderer>();
            }

            GunLine.gameObject.SetActive(true);
            GunLine.material.shader = Shader.Find("GUI/Text Shader");
            GunLine.startColor = backgroundColor.GetCurrentColor();
            GunLine.endColor = backgroundColor.GetCurrentColor(0.5f);
            GunLine.startWidth = 0.025f;
            GunLine.endWidth = 0.025f;
            GunLine.positionCount = 2;
            GunLine.useWorldSpace = true;

            GunLine.SetPosition(0, StartPosition);
            GunLine.SetPosition(1, EndPosition);

            return (Ray, GunPointer);
        }

        // Variables
        // Important
        // Objects
        public static GameObject menu;
        public static GameObject menuBackground;
        public static GameObject reference;
        public static GameObject canvasObject;

        public static SphereCollider buttonCollider;
        public static Camera TPC;
        public static Text fpsObject;

        private static GameObject GunPointer;
        private static LineRenderer GunLine;

        // Keyboard menu state
        public static bool keyboardMenuOpen;        // toggled by pressing the keyboard button
        private static Vector2 menuScreenOffset;    // where you dragged the menu (screen right / up, in world units)
        private static Vector2 dragGrabOffset;      // distance between the cursor and the menu center when you grabbed it
        private static bool draggingMenu;           // true only while you are holding left click on the panel
        private static bool cursorSaved;            // true once the cursor state was saved for restoring
        private static CursorLockMode savedLockState;
        private static bool savedCursorVisible;

        // Data
        public static int pageNumber = 0;
        public static int _currentCategory;
        public static int currentCategory
        {
            get => _currentCategory;
            set
            {
                _currentCategory = value;
                pageNumber = 0;
            }
        }
        // Short input helpers
        public static bool rightGrip => ControllerInputPoller.instance.rightGrab;
        public static bool leftGrip => ControllerInputPoller.instance.leftGrab;
        public static bool rightTrigger => ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f;
        public static bool leftTrigger => ControllerInputPoller.instance.leftControllerIndexFloat > 0.5f;

        // ---------- Ghost body ----------
        private const float GhostAlpha = 0.25f;          // 0 = fully invisible, 1 = solid
        private const bool GhostHidesCosmetics = true;   // hide hats, name tag etc. while ghosting

        private static float ghostUntil;
        private static Material ghostMaterial;
        private static Material[] ghostOriginalMaterials;
        private static readonly List<Renderer> ghostHiddenRenderers = new List<Renderer>();

        // Call this from any mod: Main.GhostBody(0.5f);
        // Calling it again while it's active just extends the time.
        public static void GhostBody(float seconds = 0.5f)
        {
            ghostUntil = Mathf.Max(ghostUntil, Time.time + seconds);
        }

        // Runs every frame from Prefix. Applies the ghost look while the timer is running, restores afterwards.
        private static void UpdateGhostBody()
        {
            try
            {
                VRRig rig = GorillaTagger.Instance.offlineVRRig;
                SkinnedMeshRenderer skin = rig.mainSkin;
                if (skin == null)
                    return;

                if (Time.time < ghostUntil)
                {
                    if (ghostMaterial == null)
                    {
                        ghostOriginalMaterials = skin.sharedMaterials;
                        ghostMaterial = new Material(Shader.Find("GUI/Text Shader"));

                        Material[] ghost = new Material[ghostOriginalMaterials.Length];
                        for (int i = 0; i < ghost.Length; i++)
                            ghost[i] = ghostMaterial;

                        skin.materials = ghost;

                        // Hide everything else on the rig (cosmetics etc.) and remember what we hid
                        if (GhostHidesCosmetics)
                        {
                            ghostHiddenRenderers.Clear();
                            foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>())
                            {
                                if (renderer != skin && renderer.enabled)
                                {
                                    renderer.enabled = false;
                                    ghostHiddenRenderers.Add(renderer);
                                }
                            }
                        }
                    }
                    else if (skin.sharedMaterial != ghostMaterial)
                    {
                        // The game swapped the material (color change, respawn...), put ours back
                        Material[] ghost = new Material[skin.sharedMaterials.Length];
                        for (int i = 0; i < ghost.Length; i++)
                            ghost[i] = ghostMaterial;

                        skin.materials = ghost;
                    }

                    // Menu background gradient color, with our own alpha
                    Color color = backgroundColor.GetCurrentColor();
                    color.a = GhostAlpha;
                    ghostMaterial.color = color;
                }
                else if (ghostMaterial != null)
                {
                    // Time is up: give the real body and cosmetics back
                    if (ghostOriginalMaterials != null)
                        skin.materials = ghostOriginalMaterials;

                    foreach (Renderer renderer in ghostHiddenRenderers)
                        if (renderer != null)
                            renderer.enabled = true;
                    ghostHiddenRenderers.Clear();

                    Destroy(ghostMaterial);
                    ghostMaterial = null;
                    ghostOriginalMaterials = null;
                }
            }
            catch { }
        }

        public static void RPCProtection()
        {
            if (!NetworkSystem.Instance.InRoom)
                return;

            try
            {
                MonkeAgent.instance.rpcErrorMax = int.MaxValue;
                MonkeAgent.instance.rpcCallLimit = int.MaxValue;
                MonkeAgent.instance.logErrorMax = int.MaxValue;
                MonkeAgent.instance.userRPCCalls.Clear();

                PhotonNetwork.MaxResendsBeforeDisconnect = int.MaxValue;
                PhotonNetwork.QuickResends = int.MaxValue;

                PhotonNetwork.SendAllOutgoingCommands();
            }
            catch { NotifiLib.SendNotification("RPC protection failed, are you in a lobby?"); }
        }
    }
}