using StupidTemplate.Menu;
using System;
using System.Linq;
using UnityEngine;

namespace StupidTemplate.Classes
{
    public class ExtGradient
    {
        // ---------- Theme colors (change them here) ----------
        // Look: Untitled menu (near-black panel, deep purple rows, thin dark outline)
        public static readonly Color32 BackgroundBase = new Color32(13, 10, 20, 255);   // menu panel: near-black purple
        public static readonly Color32 ButtonOffBase = new Color32(27, 19, 42, 255);    // disabled row / footer button: deep purple
        public static readonly Color32 ButtonOnBase = new Color32(110, 68, 205, 255);   // enabled row: bright purple
        public static readonly Color32 SearchBase = new Color32(20, 14, 31, 255);       // "search..." row: a bit darker than the rows
        public static readonly Color32 DisconnectBase = new Color32(38, 42, 100, 255);  // floating Disconnect pill: navy blue
        public static readonly Color32 OutlineBase = new Color32(60, 48, 88, 255);      // thin dark line around the panel and every button
        public static readonly Color32 DividerBase = new Color32(38, 30, 54, 255);      // thin lines under the title and above the footer
        public static readonly Color32 SubtextBase = new Color32(110, 104, 134, 255);   // small grey text (page, fps, search...)
        public static readonly Color32 KeyBase = new Color32(24, 20, 34, 255);          // search keyboard keys: dark
        public static readonly Color32 AccentBase = new Color32(170, 112, 240, 255);    // purple backspace key + search underline

        public static ExtGradient Background() => new ExtGradient { colors = GetSolidGradient(BackgroundBase) };
        public static ExtGradient ButtonOff() => new ExtGradient { colors = GetSolidGradient(ButtonOffBase) };
        public static ExtGradient ButtonOn() => new ExtGradient { colors = GetSolidGradient(ButtonOnBase) };
        public static ExtGradient Search() => new ExtGradient { colors = GetSolidGradient(SearchBase) };
        public static ExtGradient Disconnect() => new ExtGradient { colors = GetSolidGradient(DisconnectBase) };
        public static ExtGradient Key() => new ExtGradient { colors = GetSolidGradient(KeyBase) };
        public static ExtGradient Accent() => new ExtGradient { colors = GetSolidGradient(AccentBase) };

        public static GradientColorKey[] GetSolidGradient(Color color) =>
            new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) };

        public static GradientColorKey[] GetSimpleGradient(Color a, Color b) =>
            new GradientColorKey[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.5f), new GradientColorKey(a, 1f) };

        // Default color for any ExtGradient that doesn't set its own
        public GradientColorKey[] colors = GetSolidGradient(ButtonOnBase);

        public Color GetColor(int index)
        {
            if (rainbow)
                return Color.HSVToRGB((Time.time + (index / 8)) % 1f, 1f, 1f);

            if (pastelRainbow)
                return Color.HSVToRGB((Time.time + (index / 8)), 0.3f, 1f);

            if (epileptic)
                return Main.RandomColor();

            if (copyRigColor)
                return RigManager.GetPlayerColor(VRRig.LocalRig);

            if (transparent)
            {
                Color targetColor = colors[index].color;
                targetColor.a = 0f;

                return targetColor;
            }

            if (customColor != null)
                return customColor?.Invoke() ?? Color.magenta;

            return colors[index].color;
        }

        public void SetColor(int index, Color color, bool setMirror = true)
        {
            rainbow = false;
            pastelRainbow = false;

            epileptic = false;
            copyRigColor = false;

            customColor = null;

            if (colors.Length <= 2)
                colors = GetSimpleGradient(colors[0].color, colors[^1].color);

            if (setMirror && index == 0)
            {
                colors[0].color = color;
                colors[^1].color = color;
            }
            else
                colors[index].color = color;
        }

        public void SetColors(Color color)
        {
            rainbow = false;
            pastelRainbow = false;

            epileptic = false;
            copyRigColor = false;

            customColor = null;

            for (int i = 0; i < colors.Length; i++)
                colors[i].color = color;
        }

        public Color GetColorTime(float time)
        {
            if (rainbow)
                return Color.HSVToRGB(time, 1f, 1f);

            if (pastelRainbow)
                return Color.HSVToRGB(time, 0.3f, 1f);

            if (epileptic)
                return Main.RandomColor();

            if (copyRigColor)
                return RigManager.GetPlayerColor(VRRig.LocalRig);

            if (transparent)
            {
                Color targetColor = new Gradient { colorKeys = colors }.Evaluate(time);
                targetColor.a = 0f;

                return targetColor;
            }

            if (customColor != null)
                return customColor?.Invoke() ?? Color.magenta;

            return new Gradient { colorKeys = colors }.Evaluate(time);
        }

        public Color GetCurrentColor(float offset = 0f) =>
            GetColorTime((offset + (Time.time * Settings.gradientSpeed)) % 1f);

        public bool IsFlat() =>
            !rainbow && !pastelRainbow && !epileptic && !copyRigColor &&
            colors.Length > 0 && colors.All(key => key.color == colors[0].color);

        public ExtGradient Clone()
        {
            return new ExtGradient
            {
                rainbow = rainbow,
                pastelRainbow = pastelRainbow,
                epileptic = epileptic,
                copyRigColor = copyRigColor,
                customColor = customColor,
                colors = colors.Select(c => new GradientColorKey(c.color, c.time)).ToArray()
            };
        }

        public bool rainbow;
        public bool pastelRainbow;

        public bool epileptic;
        public bool copyRigColor;

        public bool transparent;

        public Func<Color> customColor;
    }
}