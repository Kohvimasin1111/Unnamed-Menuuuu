using ExitGames.Client.Photon;
using GorillaGameModes;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using StupidTemplate.Notifications;
using StupidTemplate.Notifications;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unnamed_Free.Mods;
using Valve.VR.InteractionSystem;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Settings;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace StupidTemplate.Mods
{
    public class Gamemode
    {
        public static float cooldown;
        public static float TagAuraDistance = 4f;
        public static int counter;
        public static Vector3 smoothedBallPos = Vector3.zero;
        public static TappableGuardianIdol[] idols;
        public static float flingCooldown = 0f;

        public enum GameModes
        {
            Infection,
            Ghost,
            FreezeTag,
            Guardian,
            Casual,
            Ambush,
            Paintbrawl,
            Hunt
        }

        public static bool IsTagged
        {
            get
            {
                if (!TryGetGameMode(GameModes.Infection, out GorillaGameManager gm)) return false;
                GorillaTagManager tagManager = gm as GorillaTagManager;
                if (tagManager == null) return false;
                return tagManager.currentInfected.Contains(GorillaTagger.Instance.offlineVRRig.Creator);
            }
        }

        public static bool TryGetGameMode(GameModes mode, out GorillaGameManager manager)
        {
            GorillaGameManager current = GorillaGameManager.instance;
            manager = null;

            if (current == null) return false;

            switch (mode)
            {
                case GameModes.Infection:
                    manager = current as GorillaTagManager;
                    break;
                case GameModes.Ghost:
                case GameModes.Ambush:
                    manager = current as GorillaAmbushManager;
                    break;
                case GameModes.FreezeTag:
                    manager = current as GorillaFreezeTagManager;
                    break;
                case GameModes.Guardian:
                    manager = current as GorillaGuardianManager;
                    break;
                case GameModes.Casual:
                    manager = current as CasualGameMode;
                    break;
                case GameModes.Paintbrawl:
                    manager = current as GorillaPaintbrawlManager;
                    break;
                case GameModes.Hunt:
                    manager = current as GorillaHuntManager;
                    break;
            }

            return manager != null;
        }

        public static void TagPlayer(VRRig plr)
        {
            if (plr == null || plr.Creator == null) return;

            if (Master.IsMasterClient)
            {
                TagPlayer(plr);
                return;
            }

            if (!TryGetGameMode(GameModes.Infection, out GorillaGameManager gm)) return;
            GorillaTagManager tagManager = (GorillaTagManager)gm;

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            if (tagManager.currentInfected.Contains(plr.Creator) || !tagManager.currentInfected.Contains(me.Creator)) return;


            Vector3 backPos = GorillaTagger.Instance.headCollider.transform.position;

            me.enabled = false;
            me.transform.position = plr.transform.position + new Vector3(0f, -0.2f, 0f);
            Master.Serialize();

            PhotonView view = PunExtensions.GetPhotonView(GameObject.Find("Player Objects/RigCache/Network Parent/GameMode(Clone)"));
            view.RPC("RPC_ReportTag", RpcTarget.MasterClient, new object[] { plr.Creator.ActorNumber });

            bypasstp(GorillaTagger.Instance.headCollider.transform.position, true);


            me.enabled = true;
            Master.Serialize();

            

        }

        public static void TagGun()
        {
            GunLib.GunLibData gunLibData = GunLib.ShootLock();
            if (gunLibData.isShooting && gunLibData.isTriggered)
            {
                TagPlayer(gunLibData.lockedPlayer);
            }
        }

        public static void TagSelf()
        {
            // Ghost setup: creates the component once and turns Ghostview on
            if (Ghost.Ghost.Instance == null)
                new GameObject("GhostObject").AddComponent<Ghost.Ghost>();
            Ghost.Ghost.SetGhostview(true);

            VRRig me = GorillaTagger.Instance.offlineVRRig;

            if (IsTagged)
            {
                me.enabled = true;
                return;
            }

            if (Master.IsMasterClient)
            {
                TagPlayer(me);
                return;
            }





            if (!TryGetGameMode(GameModes.Infection, out GorillaGameManager gm)) return;
            GorillaTagManager tagManager = (GorillaTagManager)gm;

            VRRig closest = null;
            float best = float.MaxValue;

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig == null || rig == me || rig.Creator == null) continue;
                if (!tagManager.currentInfected.Contains(rig.Creator)) continue;

                float d = (rig.transform.position - me.transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    closest = rig;
                }
            }

            if (closest == null) return;


            bypasstp(closest.rightHandTransform.position, true);


            me.enabled = false;
            me.transform.position = closest.rightHandTransform.position;
            Master.Serialize();
        }

        public static void TagSelfOff()
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
            Master.Serialize();
        }

        static float idolCooldown;

        public static TappableGuardianIdol[] GetGuardianIdols()
        {
            if (Time.time > idolCooldown)
            {
                idols = null;
                idolCooldown = Time.time + 5f;
            }

            if (idols == null)
                idols = UnityEngine.Object.FindObjectsOfType<TappableGuardianIdol>();

            return idols;
        }

        public static void AlwaysGuardian()
        {
            if (!TryGetGameMode(GameModes.Guardian, out GorillaGameManager gm)) return;
            GorillaGuardianManager manager = gm as GorillaGuardianManager;
            if (manager == null) return;

            if (PhotonNetwork.IsMasterClient)
            {
                if (manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer)) return;

                foreach (GorillaGuardianZoneManager zone in GorillaGuardianZoneManager.zoneManagers)
                {
                    if (zone.enabled && zone.CurrentGuardian != NetworkSystem.Instance.LocalPlayer)
                        zone.SetGuardian(NetworkSystem.Instance.LocalPlayer);
                }
                return;
            }

            VRRig me = GorillaTagger.Instance.offlineVRRig;

            if (manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer))
            {
                me.enabled = true;
                return;
            }

            foreach (TappableGuardianIdol idol in GetGuardianIdols())
            {
                if (idol == null) continue;

                if (idol.isChangingPositions)
                {
                    me.enabled = true;
                    continue;
                }

                me.enabled = false;
                me.transform.position = idol.transform.position;
                me.leftHand.rigTarget.transform.position = idol.transform.position;
                me.rightHand.rigTarget.transform.position = idol.transform.position;

                idol.manager.photonView.RPC("SendOnTapRPC", RpcTarget.All, new object[]
                {
            idol.tappableId,
            UnityEngine.Random.Range(0.2f, 0.4f)
                });
            }
        }

        public static void AlwaysGuardianOff()
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
        }

        static PhotonView GetRigView(VRRig rig)
        {
            PhotonView v = rig.GetComponent<PhotonView>();
            if (v != null) return v;

            v = rig.GetComponentInParent<PhotonView>();
            v = rig.GetComponentInParent<PhotonView>();
            if (v != null) return v;

            try
            {
                object netView = Traverse.Create(rig).Field("netView").GetValue();
                if (netView != null)
                {
                    v = Traverse.Create(netView).Field("punView").GetValue() as PhotonView;
                    if (v != null) return v;
                }
            }
            catch { }

            foreach (PhotonView pv in PhotonNetwork.PhotonViewCollection)
                if (pv.Owner != null && rig.Creator != null && pv.Owner.ActorNumber == rig.Creator.ActorNumber)
                    return pv;

            return null;
        }

        static float grabDebugCooldown;

        public static void GuardianGrabAll()
        {
            if (!TryGetGameMode(GameModes.Guardian, out GorillaGameManager gm))
            {
                GrabDebug("Not in Guardian mode.");
                return;
            }

            GorillaGuardianManager manager = gm as GorillaGuardianManager;
            if (manager == null || !manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer))
            {
                GrabDebug("You are not the guardian. Use Get Guardian first.");
                return;
            }

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            bool grabbing = GrabInput;

            foreach (VRRig rig in VRRigCache.ActiveRigs.ToList())
            {
                if (rig == null || rig == me || rig.Creator == null) continue;

                PhotonView view = GetRigView(rig);
                if (view == null)
                {
                    GrabDebug("No PhotonView found for " + rig.Creator.NickName);
                    continue;
                }

                if (grabbing)
                    view.RPC("GrabbedByPlayer", RpcTarget.All, new object[] { true, false, false });
                else
                    view.RPC("DroppedByPlayer", RpcTarget.All, new object[] { new Vector3(0f, 10f, 0f) });
            }
        }
        public static void GuardianFlingAll()
        {
            if (!TryGetGameMode(GameModes.Guardian, out GorillaGameManager gm)) return;

            GorillaGuardianManager manager = gm as GorillaGuardianManager;
            if (manager == null || !manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer))
            {
                GrabDebug("You are not the guardian. Use Get Guardian first.");
                return;
            }

            VRRig me = GorillaTagger.Instance.offlineVRRig;

            if (!GrabInput)
            {
                if (!me.enabled)
                {
                    me.enabled = true;
                    Master.Serialize();
                }
                return;
            }

            me.enabled = false;
            me.transform.position = new Vector3(500f, 500f, 500f);

            if (flingCooldown > Time.time) return;
            flingCooldown = Time.time + 0.1f;

            foreach (VRRig rig in VRRigCache.ActiveRigs.ToList())
            {
                if (rig == null || rig == me || rig.Creator == null) continue;

                PhotonView view = GetRigView(rig);
                if (view == null) continue;

                view.RPC("GrabbedByPlayer", RpcTarget.All, new object[] { true, false, false });
            }
        }

        public static void GuardianFlingAllOff()
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
            Master.Serialize();
        }

        // how fast the carry happens

        static VRRig kickTarget;
        static float kickTick;
        static Vector3 kickDestination;

        static bool FindMapTrigger(VRRig target, out Vector3 destination)
        {
            destination = Vector3.zero;
            float best = -1f;

            foreach (GorillaNetworkJoinTrigger trigger in UnityEngine.Object.FindObjectsOfType<GorillaNetworkJoinTrigger>())
            {
                if (trigger == null) continue;

                Collider col = trigger.GetComponent<Collider>();
                Vector3 pos = col != null ? col.bounds.center : trigger.transform.position;

                // the farthest trigger from the target is almost always a different map
                float d = (pos - target.transform.position).sqrMagnitude;
                if (d > best)
                {
                    best = d;
                    destination = pos;
                }
            }

            return best >= 0f;
        }

        public static void GuardianMapKickGun()
        {
            GunLib.GunLibData gun = GunLib.ShootLock();
            bool holding = gun.isShooting && gun.isTriggered;
            VRRig me = GorillaTagger.Instance.offlineVRRig;

            if (!holding)
            {
                if (kickTarget != null)
                {
                    PhotonView view = GetRigView(kickTarget);
                    if (view != null)
                        view.RPC("DroppedByPlayer", RpcTarget.All, new object[] { Vector3.zero });
                    kickTarget = null;
                }

                if (!me.enabled)
                {
                    me.enabled = true;
                    Master.Serialize();
                }
                return;
            }

            if (!TryGetGameMode(GameModes.Guardian, out GorillaGameManager gm)) return;

            GorillaGuardianManager manager = gm as GorillaGuardianManager;
            if (manager == null || !manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer))
            {
                GrabDebug("You are not the guardian. Use Get Guardian first.");
                return;
            }

            if (kickTarget == null)
            {
                VRRig candidate = gun.lockedPlayer;
                if (candidate == null || candidate == me || candidate.Creator == null) return;

                if (!FindMapTrigger(candidate, out kickDestination))
                {
                    GrabDebug("No map trigger found. Load into a map with portals first.");
                    return;
                }

                kickTarget = candidate;
            }

            me.enabled = false;
            me.transform.position = kickDestination;

            if (Time.time < kickTick) return;
            kickTick = Time.time + 0.1f;

            PhotonView grabView = GetRigView(kickTarget);
            if (grabView == null)
            {
                GrabDebug("No PhotonView found for " + kickTarget.Creator.NickName);
                return;
            }

            grabView.RPC("GrabbedByPlayer", RpcTarget.All, new object[] { true, false, false });
        }

        public static void GuardianMapKickGunOff()
        {
            if (kickTarget != null)
            {
                PhotonView view = GetRigView(kickTarget);
                if (view != null)
                    view.RPC("DroppedByPlayer", RpcTarget.All, new object[] { Vector3.zero });
            }

            kickTarget = null;
            GorillaTagger.Instance.offlineVRRig.enabled = true;
            Master.Serialize();
        }


        static void GrabDebug(string msg)
        {
            if (Time.time < grabDebugCooldown) return;
            grabDebugCooldown = Time.time + 3f;
            NotifiLib.SendNotification("Guardian Grab", msg);
        }
        static bool GrabInput
        {
            get
            {
                bool mouse = Mouse.current != null && Mouse.current.rightButton.isPressed;
                return rightGrip || mouse;
            }
        }

        public static void TagAll()
        {
            if (!IsTagged)
            {
                ButtonInfo button = Main.GetIndex("Tag All");
                if (button != null) button.enabled = false;
                return;
            }

            VRRig me = GorillaTagger.Instance.offlineVRRig;

            foreach (VRRig rig in VRRigCache.ActiveRigs.ToList())
            {
                if (rig == null || rig == me || rig.Creator == null) continue;
                TagPlayer(rig);
            }
        }

        public static void NoTagOnJoin()
        {
            PlayerPrefs.SetString("tutorial", "nope");
            PlayerPrefs.SetString("didTutorial", "nope");
            Hashtable hash = new Hashtable();
            hash.Add("didTutorial", false);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash, null, null);
            PlayerPrefs.Save();
        }

        public static void bypasstp(Vector3 position, bool tprig = false)
        {
            if (tprig)
            {
                if (GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null && VRRig.LocalRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.enabled = false;
                    GorillaTagger.Instance.offlineVRRig.transform.position = position;
                    if (GorillaTagger.Instance.offlineVRRig.rightHandTransform != null)
                        GorillaTagger.Instance.offlineVRRig.rightHandTransform.position = position;
                    if (GorillaTagger.Instance.offlineVRRig.leftHandTransform != null)
                        GorillaTagger.Instance.offlineVRRig.leftHandTransform.position = position;

                    VRRig.LocalRig.enabled = false;
                    VRRig.LocalRig.transform.position = position;
                    if (VRRig.LocalRig.rightHandTransform != null)
                        VRRig.LocalRig.rightHandTransform.position = position;
                    if (VRRig.LocalRig.leftHandTransform != null)
                        VRRig.LocalRig.leftHandTransform.position = position;
                }
                return;
            }

        }
    }
}