using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace StupidTemplate.ImGuiUI
{
    // No ImGui types in this file, so it always loads, even when ImGui.NET.dll is missing.
    public static class ImGuiBridge
    {
        private static bool active;
        private static bool broken;

        // true once ImGui started fine. Logs who turns it off.
        public static bool Active
        {
            get { return active; }
            set
            {
                if (active == value)
                    return;

                active = value;
                if (!value)
                    Debug.Log("ImGuiBridge: Active set to FALSE by:\n" + Environment.StackTrace);
            }
        }

        public static bool Typing;   // true while an ImGui text box has focus

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Start()
        {
            ImGuiHost.Ensure();
        }

        public static void TryStart()
        {
            if (broken)
                return;

            try
            {
                ImGuiLoader.Init();
                Start();
            }
            catch (Exception exc)
            {
                broken = true;
                Active = false;
                Debug.LogError("ImGui could not load, using the old menu. Reason: " + exc);
            }
        }
    }
}