using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaTag;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using StupidTemplate.Mods;
using StupidTemplate.Notifications;
using System;
using System.Reflection;
using UnityEngine;
using static StupidTemplate.Menu.Main;

namespace Unnamed_Free.Mods
{//this is all undefined code this code isnt mine in this class
    public class Master
    {
        public static void Serialize()
        {
            typeof(PhotonNetwork).GetMethod("RunViewUpdate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(typeof(PhotonNetwork), Array.Empty<object>());
        }

        public static bool IsMasterClient
        {
            get
            {
                return PhotonNetwork.IsMasterClient;
            }
        }

        public static HitTargetNetworkState[] tagetcache;

        public static void SpazTargets()
        {
            if (tagetcache == null)
            {
                tagetcache = Resources.FindObjectsOfTypeAll<HitTargetNetworkState>();
            }
            if (PhotonNetwork.IsMasterClient)
            {
                foreach (HitTargetNetworkState item in tagetcache)
                {
                    item.hitCooldownTime = 0;
                    item.TargetHit(Vector3.zero, Vector3.zero);
                }
            }
        }

        public static void SlowGun()
        {
            bool isMasterClient = Master.IsMasterClient;
            if (isMasterClient)
            {
                GunLib.GunLibData gunLibData = GunLib.ShootLock();
                bool flag = gunLibData.isShooting && gunLibData.isTriggered && gunLibData.isLocked;
                if (flag)
                {
                    bool flag2 = Master.MasterClientTimedAction < Time.time;
                    if (flag2)
                    {
                        Master.MasterClientTimedAction = Time.time + 0.2f;
                        Master.SetNetStatus(0, new RaiseEventOptions
                        {
                            TargetActors = new int[]
                            {
                                gunLibData.lockedPlayer.Creator.ActorNumber
                            }
                        });
                    }
                }
            }
        }

        public static void SlowAll()
        {
            bool isMasterClient = Master.IsMasterClient;
            if (isMasterClient)
            {
                bool flag = Master.MasterClientTimedAction < Time.time;
                if (flag)
                {
                    Master.MasterClientTimedAction = Time.time + 0.2f;
                    Master.SetNetStatus(0, new RaiseEventOptions
                    {
                        Receivers = ReceiverGroup.All,
                    });
                }
            }
        }

        public static void MatGun()
        {
            GunLib.GunLibData gunLibData = GunLib.ShootLock();
            bool flag = gunLibData.isShooting && gunLibData.isTriggered && gunLibData.isLocked;
            if (flag)
            {
                bool flag2 = Time.time > Master.MasterClientTimedAction;
                if (flag2)
                {
                    Master.MasterClientTimedAction = Time.time + 0.05f;
                    bool materialSwap = Master.MaterialSwap;
                    if (materialSwap)
                    {
                        TagPlayer(gunLibData.lockedPlayer.Creator);
                        Master.MaterialSwap = true;
                    }
                    else
                    {
                        UntagPlayer(gunLibData.lockedPlayer.Creator);
                        Master.MaterialSwap = false;
                    }
                    Master.Serialize();
                }
            }
        }

        public static void UntagPlayer(NetPlayer player)
        {
            bool isMasterClient = Master.IsMasterClient;
            if (isMasterClient)
            {
                GorillaTagManager gorillaTagManager = (GorillaTagManager)GorillaGameManager.instance;
                bool flag = gorillaTagManager != null;
                if (flag)
                {
                    gorillaTagManager.currentInfected.Remove(player);
                }
            }
        }

        public static void TagPlayer(NetPlayer player)
        {
            bool isMasterClient = Master.IsMasterClient;
            if (isMasterClient)
            {
                GorillaTagManager gorillaTagManager = (GorillaTagManager)GorillaGameManager.instance;
                bool flag = gorillaTagManager != null;
                if (flag)
                {
                    gorillaTagManager.currentInfected.Add(player);
                }
            }
        }


        private static HalloweenGhostChaser lucy;
        private static GameObject terraformer;
        private static float delay;

        public static void SpawnLucy()
        {
            if (IsMaster())
                return;

            HalloweenGhostChaser ghost = GetLucy();

            if (ghost == null || !ghost.IsMine)
                return;

            ghost.timeGongStarted = Time.time;
            ghost.currentState = HalloweenGhostChaser.ChaseState.Gong;
            ghost.isSummoned = false;
        }

        private static HalloweenGhostChaser GetLucy()
        {
            if (lucy == null)
            {
                GameObject obj = GameObject.Find(
                    "Environment Objects/05Maze_PersistentObjects/Ghosts/Halloween Ghost/FloatingChaseSkeleton");

                if (obj != null)
                    lucy = obj.GetComponent<HalloweenGhostChaser>();
            }

            return lucy;
        }



        private static LurkerGhost lurker;

        private static LurkerGhost Lurker
        {
            get
            {
                if (lurker == null)
                    lurker = GameObject.Find("Environment Objects/05Maze_PersistentObjects/Ghosts/GhostLurker_Prefab").GetComponent<LurkerGhost>();

                return lurker;
            }
        }

        private static GameObject GetTerraformer()
        {
            if (terraformer == null)
            {
                terraformer = GameObject.Find(
                    "Environment Objects/LocalObjects_Prefab/Forest/2026_Halloween_Forest/Terraformer");
            }

            return terraformer;
        }
        public static bool Overseer = true;

        public static bool IsMaster(bool notify = true)
        {
            if (NetworkSystem.Instance.InRoom && PhotonNetwork.IsMasterClient)
                return true;

            if (notify)
                NotifiLib.SendNotification("You are not the master client!");

            return false;
        }



        public static void SpawnRedLucy()
        {
            if (!IsMaster())
                return;

            HalloweenGhostChaser ghost = GetLucy();

            if (ghost == null || !ghost.IsMine)
                return;

            ghost.timeGongStarted = Time.time;
            ghost.currentState = HalloweenGhostChaser.ChaseState.Gong;
            ghost.isSummoned = true;
        }


        public static void LucyChaseGun()
        {
            GunLib.StartGun(() =>
            {
                HalloweenGhostChaser ghost = GetLucy();

                if (ghost == null || GunLib.LockedPlayer == null)
                    return;

                if (!IsMaster() || !ghost.IsMine)
                    return;

                ghost.currentState = HalloweenGhostChaser.ChaseState.Chasing;
                ghost.targetPlayer = GunLib.LockedPlayer.creator;
                ghost.followTarget = GunLib.LockedPlayer.head.rigTarget;
            }, true);
        }


        public static void SlowLucy()
        {
            HalloweenGhostChaser ghost = GetLucy();

            if (ghost == null || !ghost.IsMine || !IsMaster(false))
                return;

            ghost.currentSpeed = 1f;
            ghost.velocityStep = 0.25f;
            ghost.velocityIncreaseTime = 20f;
        }


        public static void FastLucy()
        {
            HalloweenGhostChaser ghost = GetLucy();

            if (ghost == null || !ghost.IsMine || !IsMaster(false))
                return;

            ghost.currentSpeed = 15f;
            ghost.velocityStep = 3f;
            ghost.velocityIncreaseTime = 5f;
        }


        public static void ResetLucySpeed()
        {
            HalloweenGhostChaser ghost = GetLucy();

            if (ghost == null || !ghost.IsMine || !IsMaster(false))
                return;

            ghost.currentSpeed = 3f;
            ghost.velocityStep = 1f;
            ghost.velocityIncreaseTime = 20f;
        }

        public static void LucyGrabGun()
        {
            GunLib.StartGun(() =>
            {
                HalloweenGhostChaser ghost = GetLucy();

                if (ghost == null || GunLib.LockedPlayer == null)
                    return;

                if (!IsMaster(false) || !ghost.IsMine)
                    return;

                ghost.isSummoned = true;
                ghost.targetPlayer = GunLib.LockedPlayer.creator;
                ghost.followTarget = GunLib.LockedPlayer.head.rigTarget;
                ghost.currentState = HalloweenGhostChaser.ChaseState.Grabbing;
            }, true);
        }







        public static void SetNetStatus(int state, RaiseEventOptions balls)
        {
            object[] array = new object[]
            {
                state
            };
            PhotonNetwork.RaiseEvent(3, new object[]
            {
                PhotonNetwork.ServerTimestamp,
                2,
                array
            }, balls, SendOptions.SendUnreliable);
        }
        

        public static float MasterClientTimedAction;

        // Token: 0x040000D3 RID: 211
        public static bool MaterialSwap;

        // Token: 0x040000D4 RID: 212
        
    }
}