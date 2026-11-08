using StupidTemplate.Classes;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using System.Text;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods.Settings
{
    public class Menu
    {
        public static int GunLineIndex = 0;

        public static void ChangeGunLine()
        {
            string[] lineNames = new string[]
            {
                "Normal",
                "Curved"
            };

            GunLineIndex++;
            GunLineIndex %= lineNames.Length;

            GetIndex("Change Gun Line").overlapText =
                $"Change Gun Line [{lineNames[GunLineIndex]}]";
        }

        public static bool UnnamedGuiEnabled = true;

        public static void ToggleUnnamedGui()
        {
            UnnamedGuiEnabled = !UnnamedGuiEnabled;

            GetIndex("Unnamed Gui").overlapText =
                $"Unnamed Gui [{(UnnamedGuiEnabled ? "On" : "Off")}]";
        }

        


    }
}