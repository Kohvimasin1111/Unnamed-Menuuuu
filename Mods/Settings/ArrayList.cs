using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using StupidTemplate.Classes;
using StupidTemplate.Menu;

namespace StupidTemplate.Mods.Settings
{
    public class ArrayList : MonoBehaviour
    {
        public static ArrayList Instance;

        public static bool ArrayListEnabled = false;

        private GUIStyle labelStyle;
        private GUIStyle shadowStyle;

        private bool stylesCreated;

        private List<string> cachedMods =
            new List<string>();

        private int lastFrame = -1;

        // Position
        private float rightOffset = 20f;
        private float topOffset = 20f;

        // Text
        private int fontSize = 17;
        private float lineSpacing = 2f;

        // ============================================================
        // INITIALIZE
        // ============================================================

        public static void Initialize()
        {
            if (Instance != null)
                return;

            GameObject obj =
                new GameObject("PC_ArrayList");

            Object.DontDestroyOnLoad(obj);

            Instance =
                obj.AddComponent<ArrayList>();
        }

        // ============================================================
        // GUI
        // ============================================================

        private void OnGUI()
        {
            if (!ArrayListEnabled)
                return;

            CreateStyles();

            if (lastFrame != Time.frameCount)
            {
                lastFrame = Time.frameCount;
                cachedMods = GetActiveMods();
            }

            DrawArrayList();
        }

        // ============================================================
        // DRAW ARRAY LIST
        // ============================================================

        private void DrawArrayList()
        {
            if (cachedMods == null ||
                cachedMods.Count == 0)
                return;

            float y = topOffset;

            for (int i = 0; i < cachedMods.Count; i++)
            {
                string mod = cachedMods[i];

                if (string.IsNullOrEmpty(mod))
                    continue;

                float width =
                    labelStyle.CalcSize(
                        new GUIContent(mod)
                    ).x;

                float x =
                    Screen.width -
                    rightOffset -
                    width;

                Rect rect =
                    new Rect(
                        x,
                        y,
                        width + 5f,
                        fontSize + 4f
                    );

                // Small black shadow/outline.
                GUI.Label(
                    new Rect(
                        rect.x + 1f,
                        rect.y + 1f,
                        rect.width,
                        rect.height
                    ),
                    mod,
                    shadowStyle
                );

                // Actual text.
                GUI.Label(
                    rect,
                    mod,
                    labelStyle
                );

                y += fontSize + lineSpacing;
            }
        }

        // ============================================================
        // GET ENABLED MODS
        // ============================================================

        private List<string> GetActiveMods()
        {
            List<string> activeMods =
                new List<string>();

            try
            {
                if (Buttons.buttons == null)
                    return activeMods;

                foreach (ButtonInfo[] category in Buttons.buttons)
                {
                    if (category == null)
                        continue;

                    foreach (ButtonInfo button in category)
                    {
                        if (button == null)
                            continue;

                        if (!button.isTogglable)
                            continue;

                        if (!button.enabled)
                            continue;

                        if (string.IsNullOrWhiteSpace(
                            button.buttonText))
                            continue;

                        string text =
                            button.buttonText.Trim();

                        if (text.StartsWith("Return"))
                            continue;

                        if (text.StartsWith("Back"))
                            continue;

                        if (text.EndsWith(">"))
                            continue;

                        if (!activeMods.Contains(text))
                        {
                            activeMods.Add(text);
                        }
                    }
                }
            }
            catch
            {
                return cachedMods != null
                    ? new List<string>(cachedMods)
                    : new List<string>();
            }

            // Longest entries first, like a typical mod ArrayList.
            return activeMods
                .OrderByDescending(
                    x => x.Length
                )
                .ThenBy(
                    x => x
                )
                .ToList();
        }

        // ============================================================
        // STYLES
        // ============================================================

        private void CreateStyles()
        {
            if (stylesCreated)
                return;

            // ========================================================
            // MAIN TEXT
            // ========================================================

            labelStyle =
                new GUIStyle(
                    GUI.skin.label
                );

            labelStyle.fontSize =
                fontSize;

            labelStyle.fontStyle =
                FontStyle.Bold;

            labelStyle.alignment =
                TextAnchor.UpperRight;

            labelStyle.normal.textColor =
                Color.white;

            labelStyle.hover.textColor =
                Color.white;

            labelStyle.active.textColor =
                Color.white;

            labelStyle.focused.textColor =
                Color.white;

            labelStyle.richText = true;

            // ========================================================
            // SHADOW
            // ========================================================

            shadowStyle =
                new GUIStyle(
                    GUI.skin.label
                );

            shadowStyle.fontSize =
                fontSize;

            shadowStyle.fontStyle =
                FontStyle.Bold;

            shadowStyle.alignment =
                TextAnchor.UpperRight;

            shadowStyle.normal.textColor =
                new Color32(
                    0,
                    0,
                    0,
                    180
                );

            shadowStyle.richText = true;

            stylesCreated = true;
        }

        // ============================================================
        // TOGGLE
        // ============================================================

        public static void Toggle()
        {
            ArrayListEnabled =
                !ArrayListEnabled;
        }

        // ============================================================
        // ENABLE
        // ============================================================

        public static void Enable()
        {
            ArrayListEnabled = true;
        }

        // ============================================================
        // DISABLE
        // ============================================================

        public static void Disable()
        {
            ArrayListEnabled = false;
        }
    }
}