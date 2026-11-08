namespace StupidTemplate.ImGuiUI
{
    // Shared flags between Main, ImGuiHost and ImGuiVR (no ImGui.NET types, safe to reference from anywhere)
    public static class VRFlags
    {
        public static bool HandHeld;    // true while the VR menu button is held
        public static bool VRActive;    // set by ImGuiVR once the VR menu is set up
    }
}