using StupidTemplate.Classes;
using StupidTemplate.ImGuiUI;
using StupidTemplate.Mods;
using System.Reflection;
using Unnamed_Free.Classes.Assets.Modules;
using Unnamed_Free.Menu;
using Unnamed_Free.Mods;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Settings;
namespace StupidTemplate.Menu
{
    public class Buttons
    {
        /*
         * Here is where all of your buttons are located.
         * To create a button, you may use the following code:
         * 
         * Move to Category:
         *   new ButtonInfo { buttonText = "Settings", method =() => currentCategory = 1, isTogglable = false, toolTip = "Opens the main settings page for the menu."},
         *   new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},
         * 
         * Togglable Mod:
         *   new ButtonInfo { buttonText = "Platforms", method =() => Movement.Platforms(), toolTip = "Spawns platforms on your hands when pressing grip."},
         */

        public static ButtonInfo[][] buttons = new ButtonInfo[][]
        {
            new ButtonInfo[] { // Main Mods [0]
                new ButtonInfo { buttonText = "Settings", method =() => currentCategory = 1, isTogglable = false, toolTip = "Opens the main settings page for the menu."},

                new ButtonInfo { buttonText = "Client", method =() => currentCategory = 4, isTogglable = false, toolTip = "Opens the Client mods tab."},
                new ButtonInfo { buttonText = "Movement Mods", method =() => currentCategory = 5, isTogglable = false, toolTip = "Opens the movement mods tab."},
                new ButtonInfo { buttonText = "Safety Mods", method =() => currentCategory = 6, isTogglable = false, toolTip = "Opens the safety mods tab."},
                new ButtonInfo { buttonText = "Gamemode", method =() => currentCategory = 7, isTogglable = false, toolTip = "Opens the Gamemode mods tab."},
                new ButtonInfo { buttonText = "Overpowered", method =() => currentCategory = 8, isTogglable = false, toolTip = "Opens the Overpowered mods tab."},
                new ButtonInfo { buttonText = "Global", method =() => currentCategory = 9, isTogglable = false, toolTip = "Opens the Global mods tab."},
                new ButtonInfo { buttonText = "Master", method =() => currentCategory = 10, isTogglable = false, toolTip = "Opens the Master mods tab."},
                new ButtonInfo { buttonText = "Favorite", method =() => currentCategory = 11, isTogglable = false, toolTip = "Opens the Favorite mods tab."},
                new ButtonInfo { buttonText = "Custom Assets", method =() => currentCategory = 12, isTogglable = false, toolTip = "Opens the Custom Assets tab."},
                

            },

            new ButtonInfo[] { // Settings [1]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},
                new ButtonInfo { buttonText = "Menu", method =() => currentCategory = 2, isTogglable = false, toolTip = "Opens the settings for the menu."},
                new ButtonInfo { buttonText = "Movement", method =() => currentCategory = 3, isTogglable = false, toolTip = "Opens the movement settings for the menu."},
                new ButtonInfo { buttonText = "Gun", method =() => currentCategory = 13, isTogglable = false, toolTip = "Opens the Gun Settings for the menu."},
            },

            new ButtonInfo[] { // Menu Settings [2]
                new ButtonInfo { buttonText = "Return to Settings", method =() => currentCategory = 1, isTogglable = false, toolTip = "Returns to the main settings page for the menu."},
                new ButtonInfo { buttonText = "Right Hand", enableMethod =() => rightHanded = true, disableMethod =() => rightHanded = false, toolTip = "Puts the menu on your right hand."},
                new ButtonInfo { buttonText = "Notifications", enableMethod =() => disableNotifications = false, disableMethod =() => disableNotifications = true, enabled = !disableNotifications, toolTip = "Toggles the notifications."},
                new ButtonInfo { buttonText = "FPS Counter", enableMethod =() => fpsCounter = true, disableMethod =() => fpsCounter = false, enabled = fpsCounter, toolTip = "Toggles the FPS counter."},
                new ButtonInfo { buttonText = "Disconnect Button", enableMethod =() => disconnectButton = true, disableMethod =() => disconnectButton = false, enabled = disconnectButton, toolTip = "Toggles the disconnect button."},
                new ButtonInfo { buttonText = "Menu Click Sound", overlapText = "Click Sound: Custom 1", method = () => Main.CycleClickSound(), isTogglable = false, toolTip = "Changed the menu click sound" },
                new ButtonInfo { buttonText = "Menu Close Style", overlapText = "Close Style: Shrink", method = () => Main.CycleCloseStyle(), isTogglable = false, toolTip = "Changed the menu close style" },
                new ButtonInfo { buttonText = "Array List", enableMethod =() => MenuSettings.ImGuiArrayList.Enabled = true, disableMethod =() => MenuSettings.ImGuiArrayList.Enabled = false, enabled = MenuSettings.ImGuiArrayList.Enabled, toolTip = "Toggles the array list."},
                new ButtonInfo { buttonText = "Laser", enableMethod =() => ImGuiVR.Laser = true, disableMethod =() => ImGuiVR.Laser = false, enabled = ImGuiVR.Laser, toolTip = "Toggles the laser (aiming and the line)."},



            },

            new ButtonInfo[] { // Movement Settings [3]
                new ButtonInfo { buttonText = "Return to Settings", method =() => currentCategory = 1, isTogglable = false, toolTip = "Returns to the main settings page for the menu."},

                new ButtonInfo { buttonText = "Change Fly Speed", overlapText = "Change Fly Speed [Normal]", method =() => Mods.Settings.Movement.ChangeFlySpeed(), isTogglable = false, toolTip = "Changes the speed of the fly mod."},
            },

            new ButtonInfo[] { // Client [4]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Disconnect", method =() => NetworkSystem.Instance.ReturnToSinglePlayer(), isTogglable = false, toolTip = "Disconnects you from the room."},
                new ButtonInfo { buttonText = "Unlock Fan Club", method =() => Global.UnlockFanClub(), isTogglable = false, toolTip = "Unlocks Fan Club."},
                new ButtonInfo { buttonText = "Unlock All Cosmetics", method =() => Client.UnlockAllCosmetics(), isTogglable = false, toolTip = "Gives you all the cosmetics."},




            },

            new ButtonInfo[] { // Movement Mods [5]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Platforms", method =() => Movement.Platforms(), toolTip = "Spawns platforms on your hands when pressing grip."},
                new ButtonInfo { buttonText = "Fly", method =() => Movement.Fly(), toolTip = "Sends you forward when holding A."},
                new ButtonInfo { buttonText = "WASD Fly", method =() => Movement.WASDFly(), toolTip = "Flys you with WASD."},
                new ButtonInfo { buttonText = "Ghost Monkey", method =() => Movement.GhostMonkey(), toolTip = "Freezes your rig ."},
                new ButtonInfo { buttonText = "Invis Monkey", method =() => Movement.InvisMonkey(), toolTip = "Makes you Invisible ."},
                new ButtonInfo { buttonText = "Grapple", method =() => Movement.SpiderWeb(), toolTip = "Makes you spiderman (LITERALLY) ."},
                new ButtonInfo { buttonText = "Hold Rig", method =() => Movement.HoldRig(), toolTip = "Holds your rig ."},
                new ButtonInfo { buttonText = "Speed Boost", method =() => Movement.SpeedBoost(), enableMethod =() => Movement.SpeedBoostOn(), disableMethod =() => Movement.SpeedBoostOff(), toolTip = "Boosts your speed. Change the level in Speed Boost Level."},
                new ButtonInfo { buttonText = "Speed Boost Level", overlapText = "Speed Level: 1 (Mosa)", method =() => Movement.CycleSpeedBoost(), isTogglable = false, toolTip = "Cycles the speed boost level (1 mosa, 2 fast, 3 faster, 4 ultra fast)."},

            },

            new ButtonInfo[] { // Safety Mods [6]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Anti Report", method =() => Safety.AntiReportDisconnect(), toolTip = "Disconnects you when someone tries to report you."},
                new ButtonInfo { buttonText = "Anti RPC Ban", method =() => Safety.AntiRPCBan(), toolTip = "Prevents disconnects from using too many mods."},
            },
             new ButtonInfo[] { // Gamemode [7]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Tag Gun", method =() => Gamemode.TagGun(), toolTip = "Tags whoever you wannt ."},
                new ButtonInfo { buttonText = "Tag Self", method =() => Gamemode.TagSelf(), toolTip = "Tags self ."},
                new ButtonInfo { buttonText = "Tag All", method =() => Gamemode.TagAll(), toolTip = "Tags Everyone ."},
                new ButtonInfo { buttonText = "No Tag On Join", method =() => Gamemode.NoTagOnJoin(), toolTip = "Doesnt tag you when you join ."},
                new ButtonInfo { buttonText = "Get Guardian", method = () => Gamemode.AlwaysGuardian(), disableMethod = () => Gamemode.AlwaysGuardianOff(), isTogglable = true, toolTip = "Makes you the guardian in Guardian mode." },
                new ButtonInfo { buttonText = "Guardian Grab All", method = () => Gamemode.GuardianGrabAll(), isTogglable = true, toolTip = "Hold right grip to grab everyone as guardian." },
                new ButtonInfo { buttonText = "Guardian Fling All", method = () => Gamemode.GuardianFlingAll(), disableMethod = () => Gamemode.GuardianFlingAllOff(), isTogglable = true, toolTip = "Hold right grip / right click to fling everyone as guardian." },



             },

             new ButtonInfo[] { // Overpowered [8]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},
                new ButtonInfo { buttonText = "Elevator Kick All(MASTER)", method =() => Overpowered.ElevatorKickAll(), toolTip = "Kicks all targets."},
                new ButtonInfo { buttonText = "Elevator Kick Gun(MASTER)", method =() => Overpowered.ElevatorKickGun(), toolTip = "Kicks the target."},
                new ButtonInfo { buttonText = "Lag Gun", method =() => Overpowered.LagGun(), toolTip = "Lags the target."},
                new ButtonInfo { buttonText = "Lag All", method =() => Overpowered.LagAll(), toolTip = "Lags the targets."},
                new ButtonInfo { buttonText = "Lag GunV2", method =() => Overpowered.LagGunV2(), toolTip = "Lags the target but better."},
                new ButtonInfo { buttonText = "Lag On Touch", method =() => Overpowered.LagOnTouch(), toolTip = "Lags the target that touches you."},
                new ButtonInfo { buttonText = "Party Kick Gun(PARTY)", method =() => Overpowered.PartyKickGun(), toolTip = "Kicks the target in your party."},
                new ButtonInfo { buttonText = "Instant Party", method =() => Overpowered.InstantParty(), toolTip = "Makes the party thing instant."},












             },
              new ButtonInfo[] { // Global [9]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "UpsideDown Rig", method =() => Global.UpsideDownRig(), toolTip = "Turns youre rig upsideDown."},
                new ButtonInfo { buttonText = "UpsideDown Rig Off", method =() => Global.UpsideDownRigOff(), isTogglable = false,  toolTip = "Turns youre rig right way."},
                new ButtonInfo { buttonText = "Set Quest Score 67", method =() => Global.SetQuestScore(67), isTogglable = false, toolTip = "Sets your quest score to 67."},
                new ButtonInfo { buttonText = "Set Quest Score 6767", method =() => Global.SetQuestScore(6767), isTogglable = false, toolTip = "Sets your quest score to 6767."},
                new ButtonInfo { buttonText = "Set Quest Score 1000000000", method =() => Global.SetQuestScore(1000000000), isTogglable = false, toolTip = "Sets your quest score to 1000000000."},
                new ButtonInfo { buttonText = "RGB Monkey", method =() => Global.RGBMonke(), toolTip = "Makes you rainbow."},
                new ButtonInfo { buttonText = "Rope Spaz All", method =() => Global.SpazAllRopes(), toolTip = "Spazes all ropes."},
                new ButtonInfo { buttonText = "Spaz Rope Gun", method =() => Global.SpazRopeGun(), toolTip = "Spazes the targeted rope."},
                new ButtonInfo { buttonText = "Destory Bat", method =() => Global.DestroyBat(), isTogglable = false, toolTip = "Destroys Bat."},
                new ButtonInfo { buttonText = "Grab Bat", method =() => Global.GrabBat(), toolTip = "Grabs Bat."},
                new ButtonInfo { buttonText = "Orbit Bat", method =() => Global.OrbitBat(), toolTip = "Orbits Bat."},
                new ButtonInfo { buttonText = "Spaz Bat", method =() => Global.SpazBat(), toolTip = "Spazes Bat."},
                new ButtonInfo { buttonText = "Destory Bug", method =() => Global.DestroyBug(), isTogglable = false, toolTip = "Destroys Bug."},
                new ButtonInfo { buttonText = "Grab Bug", method =() => Global.GrabBug(), toolTip = "Grabs Bug."},
                new ButtonInfo { buttonText = "Orbit Bug", method =() => Global.OrbitBug(), toolTip = "Orbits Bug."},
                new ButtonInfo { buttonText = "Spaz Bug", method =() => Global.SpazBug(), toolTip = "Spazes Bug."},
                new ButtonInfo { buttonText = "Destory Firefly", method =() => Global.DestroyFirefly(), isTogglable = false, toolTip = "Destroys Firefly."},
                new ButtonInfo { buttonText = "Grab Firefly", method =() => Global.GrabFirefly(), toolTip = "Grabs Firefly."},
                new ButtonInfo { buttonText = "Orbit Firefly", method =() => Global.OrbitFirefly(), toolTip = "Orbits Firefly."},
                new ButtonInfo { buttonText = "Spaz Firefly", method =() => Global.SpazFirefly(), toolTip = "Spazes Firefly."},
                new ButtonInfo { buttonText = "Create Public Room", method =() => Global.CreatePublicRoom(), isTogglable = false, toolTip = "Creates a public room."},








              },



              new ButtonInfo[] { // Master [10]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Target Hit Spam", method =() => Master.SpazTargets(), toolTip = "Spam Hits Targets."},
                new ButtonInfo { buttonText = "Slow Gun", method =() => Master.SlowGun(), toolTip = "Slows the target."},
                new ButtonInfo { buttonText = "Slow All", method =() => Master.SlowAll(), toolTip = "Slows the targets."},
                new ButtonInfo { buttonText = "Untag Gun", method =() => Master.MatGun(), toolTip = "Tags and untags the target."},
                new ButtonInfo { buttonText = "Lucy Chase Gun", method =() => Master.LucyChaseGun(), toolTip = "Makes Lucy Chase the target."},
                new ButtonInfo { buttonText = "Spawn Red Lucy", method =() => Master.SpawnRedLucy(), isTogglable = false, toolTip = "Spawns Red Lucy."},
                new ButtonInfo { buttonText = "Slow Lucy", method =() => Master.SlowLucy(), isTogglable = false, toolTip = "Makes lucy slow."},
                new ButtonInfo { buttonText = "Fast Lucy", method =() => Master.FastLucy(), isTogglable = false, toolTip = "Makes lucy fast."},
                new ButtonInfo { buttonText = "Reset Lucy Speed", method =() => Master.ResetLucySpeed(), isTogglable = false, toolTip = "Resets lucys speed."},
                new ButtonInfo { buttonText = "Lucy Grab Gun", method =() => Master.LucyGrabGun(), toolTip = "Makes Lucy Grab the target."},



              },


              new ButtonInfo[] { // Favorite [11]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},
              },

               new ButtonInfo[] { // CustomAssets [12]
                new ButtonInfo { buttonText = "Return to Main", method =() => currentCategory = 0, isTogglable = false, toolTip = "Returns to the main page of the menu."},

                new ButtonInfo { buttonText = "Robux Rain", method =() => ModuleAssets.RainRobux(), toolTip = "Rains Robux."},
               },

                new ButtonInfo[] { // Gun Settings [13]
                new ButtonInfo { buttonText = "Return to Settings", method =() => currentCategory = 1, isTogglable = false, toolTip = "Returns to the main settings page for the menu."},

                new ButtonInfo { buttonText = "Gun Line Style", overlapText = "Gun Line: Curved", method = () => GunLib.CycleBeamStyle(), isTogglable = false, toolTip = "Changes the gun line (Normal, Curved, Twirly, Zigzag etc)." },

                },


                
        };
    }
}
