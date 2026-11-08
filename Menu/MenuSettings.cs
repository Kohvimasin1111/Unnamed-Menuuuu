using ImGuiNET;
using StupidTemplate.Classes;
using StupidTemplate.ImGuiUI;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using static StupidTemplate.Settings;
using NVec2 = System.Numerics.Vector2;
using NVec4 = System.Numerics.Vector4;

namespace Unnamed_Free.Menu
{
    public class MenuSettings
    {
        // PC array list (enabled mods), top-right of the screen.
        // ImGuiHost draws it every frame in the same frame as the toasts, menu open or closed.
        // PC only, VR does not show it.
        public static class ImGuiArrayList
        {
            // Off by default. The "PC Array List" button in Menu Settings turns it on.
            public static bool Enabled = false;

            // ---- look ----
            public static bool ShowTitle = true;          // menu name above the list
            private const float FontSize = 16f;           // text size of the rows
            private const float TitleSize = 19f;
            private const float RowHeight = 22f;
            private const float RowGap = 3f;
            private const float PadX = 10f;               // space left / right of the text
            private const float BarWidth = 3f;            // accent bar on the right edge
            private const float Radius = 6f;

            // ---- gradient ----
            private const bool AnimateGradient = true;    // colors slowly flow up and down the list
            private const float FlowSpeed = 0.12f;
            private const float BackgroundAlpha = 0.45f;  // dark panel behind each row
            private const float TintAlpha = 0.22f;        // how strong the color glow behind each row is

            // The two colors of the gradient. Swap these two lines if you want other theme colors.
            private static NVec4 ColorA()
            {
                Color c = buttonColors[1].GetCurrentColor();
                return new NVec4(c.r, c.g, c.b, 1f);
            }

            private static NVec4 ColorB()
            {
                Color c = (Color)ExtGradient.AccentBase;
                return new NVec4(c.r, c.g, c.b, 1f);
            }

            // ---- enabled mods (refreshed a few times a second, no ImGui needed for this) ----
            private const float RefreshEvery = 0.2f;
            private const string ToggleButtonName = "PC Array List";   // hidden from the list, must match the button text
            private static readonly List<string> names = new List<string>();
            private static readonly HashSet<string> seen = new HashSet<string>();
            private static float nextRefresh;

            public static bool HasItems
            {
                get { return Enabled && names.Count > 0; }
            }

            public static void Refresh()
            {
                if (Time.unscaledTime < nextRefresh)
                    return;
                nextRefresh = Time.unscaledTime + RefreshEvery;

                names.Clear();
                seen.Clear();

                try
                {
                    foreach (ButtonInfo[] list in Buttons.buttons)
                    {
                        if (list == null)
                            continue;

                        foreach (ButtonInfo b in list)
                        {
                            if (b == null || !b.enabled || !b.isTogglable || string.IsNullOrEmpty(b.buttonText))
                                continue;

                            if (b.buttonText == ToggleButtonName)
                                continue;

                            string n = b.buttonText.ToUpper();
                            if (seen.Add(n))
                                names.Add(n);
                        }
                    }
                }
                catch (Exception) { }

                // longest first, like the old list
                names.Sort((x, y) => y.Length.CompareTo(x.Length));
            }

            // ---- drawing (call between NewFrame and Render) ----
            private static NVec2 P(float x, float y)
            {
                return new NVec2(x, y);
            }

            private static uint Col(NVec4 c, float alpha)
            {
                return ImGui.ColorConvertFloat4ToU32(new NVec4(c.X, c.Y, c.Z, Mathf.Clamp01(alpha)));
            }

            private static float Measure(ImFontPtr font, float size, string text)
            {
                ImGui.PushFont(font);
                float w = ImGui.CalcTextSize(text).X;
                ImGui.PopFont();
                float native = font.FontSize > 1f ? font.FontSize : size;
                return w * size / native;
            }

            // topRight = where the right edge of the first row sits
            public static void Draw(NVec2 topRight)
            {
                if (!HasItems)
                    return;

                ImDrawListPtr dl = ImGui.GetForegroundDrawList();
                ImFontPtr bold = (ImFontPtr)ImGuiHost.Bold;

                NVec4 a = ColorA();
                NVec4 b = ColorB();
                NVec4 black = new NVec4(0f, 0f, 0f, 1f);

                float flow = AnimateGradient ? Time.unscaledTime * FlowSpeed : 0f;
                int count = names.Count;
                float y = topRight.Y;

                if (ShowTitle)
                {
                    string title = StupidTemplate.PluginInfo.Name;
                    float tw = Measure(bold, TitleSize, title);
                    NVec4 tc = NVec4.Lerp(a, b, Mathf.PingPong(flow, 1f));

                    // soft shadow, then the title
                    dl.AddText(bold, TitleSize, P(topRight.X - tw + 1f, y + 1f), Col(black, 0.55f), title);
                    dl.AddText(bold, TitleSize, P(topRight.X - tw, y), Col(tc, 1f), title);
                    y += TitleSize + 8f;
                }

                for (int i = 0; i < count; i++)
                {
                    string name = names[i];
                    float tw = Measure(bold, FontSize, name);
                    float x1 = topRight.X;
                    float x0 = x1 - (tw + PadX * 2f + BarWidth);

                    // color for this row: runs from color A (top) to color B (bottom), flowing over time
                    float t = Mathf.PingPong(i / (float)Mathf.Max(1, count - 1) * 0.85f + flow, 1f);
                    NVec4 c = NVec4.Lerp(a, b, t);
                    NVec4 text = NVec4.Lerp(c, new NVec4(1f, 1f, 1f, 1f), 0.30f);   // a bit brighter so it reads on any background

                    NVec2 min = P(x0, y);
                    NVec2 max = P(x1, y + RowHeight);

                    // dark panel, then a color glow that fades in from the left
                    dl.AddRectFilled(min, max, Col(black, BackgroundAlpha), Radius, ImDrawFlags.RoundCornersLeft);
                    uint clear = Col(c, 0f);
                    uint tint = Col(c, TintAlpha);
                    dl.AddRectFilledMultiColor(min, max, clear, tint, tint, clear);

                    // accent bar on the right edge
                    dl.AddRectFilled(P(x1 - BarWidth, y), max, Col(c, 1f));

                    // text with a tiny shadow
                    float ty = y + (RowHeight - FontSize) / 2f - 1f;
                    dl.AddText(bold, FontSize, P(x0 + PadX + 1f, ty + 1f), Col(black, 0.5f), name);
                    dl.AddText(bold, FontSize, P(x0 + PadX, ty), Col(text, 1f), name);

                    y += RowHeight + RowGap;
                }
            }
        }
    }
}