using GorillaGameModes;
using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using GorillaNetworking;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using StupidTemplate.Classes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows;
using static NetworkSystemPUN;
using static StupidTemplate.Classes.GunLib;
using static StupidTemplate.Menu.Main;
using static WorldShareableItem;
using Random = UnityEngine.Random;

namespace Unnamed_Free.Mods
{
    public class Global
    {
        public static void UpsideDownRig()
        {
            VRRig me = GorillaTagger.Instance.offlineVRRig;
            if (me == null) return;

            me.enabled = false;

            Transform head = GorillaTagger.Instance.headCollider.transform;
            Transform body = GorillaTagger.Instance.bodyCollider.transform;

            // pivot sits halfway between head and body, so the rig stays centered on you
            Vector3 pivot = Vector3.Lerp(head.position, body.position, 0.5f);

            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd == Vector3.zero) fwd = Vector3.forward;
            Quaternion roll = Quaternion.AngleAxis(180f, fwd);

            // root stays at your body, just rolled over
            me.transform.position = pivot + roll * (body.position - pivot);
            me.transform.rotation = roll * body.rotation;

            me.head.rigTarget.position = pivot + roll * (head.position - pivot);
            me.head.rigTarget.rotation = roll * head.rotation;

            Transform lh = GorillaTagger.Instance.leftHandTransform;
            Transform rh = GorillaTagger.Instance.rightHandTransform;

            me.leftHand.rigTarget.position = pivot + roll * (lh.position - pivot);
            me.leftHand.rigTarget.rotation = roll * lh.rotation;

            me.rightHand.rigTarget.position = pivot + roll * (rh.position - pivot);
            me.rightHand.rigTarget.rotation = roll * rh.rotation;

            Master.Serialize();
        }

        public static void UpsideDownRigOff()
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
            Master.Serialize();
        }



        public static void UnlockFanClub()
        {
            SubscriptionPatches.enabled = !SubscriptionPatches.enabled;
        }


        public static void SetQuestScore(int score)
        {
            VRRig.LocalRig.SetQuestScore(score);
        }
        public static void RGBMonke()
        {
            float time = Time.time * 1.8f;
            var R = Mathf.Sin(time) * 0.5f + 0.5f;
            var G = Mathf.Sin(time + 2f * Mathf.PI / 3f) * 0.5f + 0.5f;
            var B = Mathf.Sin(time + 4f * Mathf.PI / 3f) * 0.5f + 0.5f;
            GorillaTagger.Instance.myVRRig.SendRPC("RPC_InitializeNoobMaterial", RpcTarget.All, new object[] { R, G, B });
        }//rgb monkey end


        private const float RopeTickDelay = 0.15f;   // seconds between hits
        private const float RopeStrength = 1000f;    // velocity strength

        private static float RopeDelay;
        private static bool ropeRigDetached;

        public static void SpazAllRopes()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool pcHeld = mouse != null && mouse.leftButton.isPressed; // hold left click

            // hold any of: right trigger, left trigger, right grip, left grip (or left click on PC)
            bool held = rightTrigger || leftTrigger || rightGrip || leftGrip || pcHeld;

            if (!held)
            {
                // let go -> put the rig back on your body
                if (ropeRigDetached)
                {
                    VRRig back = GorillaTagger.Instance.offlineVRRig;
                    if (back != null) back.enabled = true;
                    ropeRigDetached = false;
                    Master.Serialize();
                }
                return;
            }

            if (Time.time < RopeDelay) return;
            RopeDelay = Time.time + RopeTickDelay;

            GorillaRopeSwing rope = GetRandomRope();
            if (rope == null) return;

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            if (me == null) return;

            // detach rig and put it on the rope so the game sees you next to it
            me.enabled = false;
            ropeRigDetached = true;

            Vector3 pos = rope.transform.position;
            me.transform.position = pos;
            me.head.rigTarget.position = pos + Vector3.up * 0.2f;
            me.leftHand.rigTarget.position = pos + Vector3.left * 0.3f;
            me.rightHand.rigTarget.position = pos + Vector3.right * 0.3f;

            // send that position to everyone, then the rope velocity
            Master.Serialize();
            BetaSetRopeVelocity(rope, RandomVector3(RopeStrength));
        }
        //spaz rope gun
        private static float SpazRopeGunDelay;
        private static bool spazRopeGunRigDetached;
        private static GorillaRopeSwing spazRopeGunLocked;
        private static Vector3 spazRopeGunPoint;
        private static GameObject spazGunLine;
        private static LineRenderer spazGunLR;
        private static GameObject spazGunSphere;

        private static void ShowSpazGun(Vector3 start, Vector3 end, bool locked)
        {
            if (spazGunLine == null)
            {
                spazGunLine = new GameObject("SpazGunLine");
                spazGunLR = spazGunLine.AddComponent<LineRenderer>();
                spazGunLR.material = new Material(Shader.Find("GUI/Text Shader"));
                spazGunLR.startWidth = 0.015f;
                spazGunLR.endWidth = 0.015f;
                spazGunLR.positionCount = 2;

                spazGunSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(spazGunSphere.GetComponent<Collider>());
                spazGunSphere.transform.localScale = Vector3.one * 0.1f;
                spazGunSphere.GetComponent<Renderer>().material = new Material(Shader.Find("GUI/Text Shader"));
            }

            Color c = locked ? Color.red : Color.white;
            spazGunLine.SetActive(true);
            spazGunSphere.SetActive(true);
            spazGunLR.material.color = c;
            spazGunSphere.GetComponent<Renderer>().material.color = c;
            spazGunLR.SetPosition(0, start);
            spazGunLR.SetPosition(1, end);
            spazGunSphere.transform.position = end;
        }

       

        public static void SpazRopeGun()
        {
            GunLib.GunLibData gunLibData = GunLib.ShootLock(1); // 1 = ropes only
            bool active = gunLibData.isShooting && gunLibData.isTriggered && gunLibData.isLocked;

            if (!active)
            {
                // let go -> put the rig back on your body
                if (spazRopeGunRigDetached)
                {
                    VRRig back = GorillaTagger.Instance.offlineVRRig;
                    if (back != null) back.enabled = true;
                    spazRopeGunRigDetached = false;
                    Master.Serialize();
                }
                return;
            }

            if (Time.time < SpazRopeGunDelay) return;
            SpazRopeGunDelay = Time.time + RopeTickDelay;

            GorillaRopeSwing rope = gunLibData.lockedRope;
            if (rope == null) return;

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            if (me == null) return;

            // park the rig on the rope so the game sees you next to it
            me.enabled = false;
            spazRopeGunRigDetached = true;

            Vector3 pos = rope.transform.position;
            me.transform.position = pos;
            me.head.rigTarget.position = pos + Vector3.up * 0.2f;
            me.leftHand.rigTarget.position = pos + Vector3.left * 0.3f;
            me.rightHand.rigTarget.position = pos + Vector3.right * 0.3f;

            Master.Serialize();
            BetaSetRopeVelocity(rope, RandomVector3(RopeStrength));
        }//spaz rope gun end


        private static GameObject cachedBug;
        public static GameObject CachedBug { get { if (cachedBug == null) cachedBug = GameObject.Find("Floating Bug Holdable"); return cachedBug; } }

        private static GameObject cachedBat;
        public static GameObject CachedBat { get { if (cachedBat == null) cachedBat = GameObject.Find("Cave Bat Holdable"); return cachedBat; } }

        private static GameObject cachedFirefly;
        public static GameObject CachedFirefly { get { if (cachedFirefly == null) cachedFirefly = GameObject.Find("Firefly"); return cachedFirefly; } }

        public static void SpazBat()
        {
            GameObject targetObject = CachedBat;
            Vector3 spazPos = GTPlayer.Instance.transform.position + Vector3.up +
            new Vector3(
                Mathf.Sin(Time.time * 20f) * 1f,
                Mathf.Cos(Time.time * 25f) * 1f,
                Mathf.Sin(Time.time * 15f) * 1f
            );
            targetObject.transform.position = spazPos;
            targetObject.transform.rotation = Quaternion.Euler(
                Time.time * 200f,
                Time.time * 300f,
                Time.time * 100f
            );
        }

        public static void DestroyBat()
        {
            GameObject targetObject = CachedBat;
            targetObject.transform.position = new Vector3(999f, 999f, 999f);
        }

        public static void GrabBat()
        {
            if (rightGrip)
            {
                GameObject targetObject = CachedBat;
                Vector3 handPos = GTPlayer.Instance.rightHand.controllerTransform.position;
                targetObject.transform.position = handPos;
                targetObject.transform.SetParent(GTPlayer.Instance.rightHand.controllerTransform);
            }
            if (leftGrip)
            {
                GameObject targetObject2 = CachedBat;
                Vector3 handPos2 = GTPlayer.Instance.leftHand.controllerTransform.position;
                targetObject2.transform.position = handPos2;
                targetObject2.transform.SetParent(GTPlayer.Instance.leftHand.controllerTransform);
            }
        }
        public static void OrbitBat()
        {
            GameObject targetObject = CachedBat;
            float angle = Time.time * 15f;
            Vector3 orbitPos = GTPlayer.Instance.transform.position +
            new Vector3(Mathf.Cos(angle) * 1.5f, 0.5f, Mathf.Sin(angle) * 1.5f);
            targetObject.transform.position = orbitPos;
            targetObject.transform.Rotate(Vector3.up, 15f * 50f * Time.deltaTime);
        }


        public static void SpazBug()
        {
            GameObject targetObject = CachedBug;
            Vector3 spazPos = GTPlayer.Instance.transform.position + Vector3.up +
            new Vector3(
                Mathf.Sin(Time.time * 20f) * 1f,
                Mathf.Cos(Time.time * 25f) * 1f,
                Mathf.Sin(Time.time * 15f) * 1f
            );
            targetObject.transform.position = spazPos;
            targetObject.transform.rotation = Quaternion.Euler(
                Time.time * 200f,
                Time.time * 300f,
                Time.time * 100f
            );
        }

        public static void DestroyBug()
        {
            GameObject targetObject = CachedBug;
            targetObject.transform.position = new Vector3(999f, 999f, 999f);
        }

        public static void GrabBug()
        {
            if (rightGrip)
            {
                GameObject targetObject = CachedBug;
                Vector3 handPos = GTPlayer.Instance.rightHand.controllerTransform.position;
                targetObject.transform.position = handPos;
                targetObject.transform.SetParent(GTPlayer.Instance.rightHand.controllerTransform);
            }
        }

        public static void OrbitBug()
        {
            GameObject targetObject = CachedBug;
            float angle = Time.time * 15f;
            Vector3 orbitPos = GTPlayer.Instance.transform.position +
            new Vector3(Mathf.Cos(angle) * 1.5f, 0.5f, Mathf.Sin(angle) * 1.5f);
            targetObject.transform.position = orbitPos;
            targetObject.transform.Rotate(Vector3.up, 15f * 50f * Time.deltaTime);
        }

        public static void GrabFirefly()
        {
            if (rightGrip)
            {
                GameObject targetObject = CachedFirefly;
                Vector3 handPos = GTPlayer.Instance.rightHand.controllerTransform.position;
                targetObject.transform.position = handPos;
                targetObject.transform.SetParent(GTPlayer.Instance.rightHand.controllerTransform);
            }
            if (leftGrip)
            {
                GameObject targetObject2 = CachedFirefly;
                Vector3 handPos2 = GTPlayer.Instance.leftHand.controllerTransform.position;
                targetObject2.transform.position = handPos2;
                targetObject2.transform.SetParent(GTPlayer.Instance.leftHand.controllerTransform);
            }
        }
        public static void SpazFirefly()
        {
            GameObject targetObject = CachedFirefly;
            Vector3 spazPos = GTPlayer.Instance.transform.position + Vector3.up +
            new Vector3(
                Mathf.Sin(Time.time * 20f) * 1f,
                Mathf.Cos(Time.time * 25f) * 1f,
                Mathf.Sin(Time.time * 15f) * 1f
            );
            targetObject.transform.position = spazPos;
            targetObject.transform.rotation = Quaternion.Euler(
                Time.time * 200f,
                Time.time * 300f,
                Time.time * 100f
            );
        }
        public static void OrbitFirefly()
        {
            GameObject targetObject = CachedFirefly;
            float angle = Time.time * 15f;
            Vector3 orbitPos = GTPlayer.Instance.transform.position +
            new Vector3(Mathf.Cos(angle) * 1.5f, 0.5f, Mathf.Sin(angle) * 1.5f);
            targetObject.transform.position = orbitPos;
            targetObject.transform.Rotate(Vector3.up, 15f * 50f * Time.deltaTime);
        }
        public static void DestroyFirefly()
        {
            GameObject targetObject = CachedFirefly;
            targetObject.transform.position = new Vector3(999f, 999f, 999f);
        }
















        public static void BetaSetRopeVelocity(GorillaRopeSwing Rope, Vector3 Velocity)
        {
            if (Rope == null) return;
            BetaSetRopeVelocity(Rope.ropeId, Velocity);
        }

        public static void BetaSetRopeVelocity(int RopeId, Vector3 Velocity)
        {
            RopeSwingManager.instance.SendSetVelocity_RPC(RopeId, 1, Velocity, true);
        }

        private static float randomRopeDelay;
        private static GorillaRopeSwing randomRope;
        public static GorillaRopeSwing GetRandomRope()
        {
            if (Time.time > randomRopeDelay)
            {
                randomRopeDelay = Time.time + 0.5f;
                randomRope = RopeSwingManager.instance.ropes.Values.OrderBy(_ => Random.value).FirstOrDefault();
            }
            return randomRope;
        }


        private const string RoomCodeCharacters = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";

        private static readonly string[] PublicRoomTriggerPaths =
        {
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Forest Tree Exit",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - City Front",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Mountain For Computer",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Cave",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Canyon",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Beach from Forest",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Metropolis from City",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Clouds",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Rotating Map",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Basement For Computer",
        "Environment Objects/TriggerZonesPrefab/JoinRoomTriggersPrefab/JoinPublicRoom - Bayou",
        "Environment Objects/LocalObjectsPrefab/MallToMonkeBlocks/JoinPublicRoom - MonkeBlocks From Mall"
    };

        private void OnEnable()
        {
            CreatePublicRoom();
            
        }

        private static string GenerateRoomCode()
        {
            string roomCode;
            do
            {
                char[] characters = new char[4];
                for (int index = 0; index < characters.Length; index++)
                {
                    characters[index] = RoomCodeCharacters[Random.Range(0, RoomCodeCharacters.Length)];
                }

                roomCode = new string(characters);
            }
            while (GorillaComputer.instance.CheckAutoBanListForName(roomCode));

            return roomCode;
        }

        public static void CreatePublicRoom()
        {
            try
            {
                PhotonNetworkController networkController = PhotonNetworkController.Instance;
                string nearestTriggerPath = FindNearestPublicRoomTrigger();
                if (!string.IsNullOrEmpty(nearestTriggerPath))
                {
                    string zoneName = GetZoneName(nearestTriggerPath);
                    networkController.currentJoinTrigger = GorillaComputer.instance.GetJoinTriggerForZone(zoneName);
                }

                if (networkController.currentJoinTrigger == null)
                {
                    networkController.currentJoinTrigger = GorillaComputer.instance.GetJoinTriggerForZone("forest");
                }

                ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable
{
    { "gameMode", networkController.currentJoinTrigger.GetFullDesiredGameModeString() },
    { "platform", networkController.platformTag },
    { "queueName", GorillaComputer.instance.currentQueue },
    { "language", System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName },
    { "fanclub", SubscriptionManager.IsLocalSubscribed() ? "true" : "false" }
};

                RoomConfig roomConfig = new RoomConfig
                {
                    isPublic = true,
                    isJoinable = true,
                    createIfMissing = true,
                    MaxPlayers = RoomSystem.GetRoomSizeForCreate(
                        networkController.currentJoinTrigger.zone,
                        (GameModeType)Enum.Parse(
                            typeof(GameModeType),
                            GorillaComputer.instance.currentGameMode.Value,
                            true),
                        false,
                        SubscriptionManager.IsLocalSubscribed()),
                    CustomProps = roomProperties
                };

                if (NetworkSystem.Instance is NetworkSystemPUN punNetwork)
                {
                    punNetwork.internalState = (InternalState)16;
                }

                _ = ForceCreateRoom(GenerateRoomCode(), roomConfig);
                GorillaTagger.Instance.StartCoroutine(MakeRoomVisibleCoroutine());
            }
            catch (Exception)
            {
            }
        }

        private static string FindNearestPublicRoomTrigger()
        {
            Vector3 playerPosition = GorillaTagger.Instance.transform.position;
            float nearestDistance = float.MaxValue;
            string nearestPath = null;

            foreach (string triggerPath in PublicRoomTriggerPaths)
            {
                GameObject triggerObject = GameObject.Find(triggerPath);
                if (triggerObject == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(playerPosition, triggerObject.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPath = triggerPath;
                }
            }

            return nearestPath;
        }

        private static string GetZoneName(string triggerPath)
        {
            return triggerPath
                .ToLowerInvariant()
                .Replace("environment objects/triggerzonesprefab/joinroomtriggersprefab/", string.Empty)
                .Replace("environment objects/localobjectsprefab/malltomonkeblocks/", string.Empty)
                .Replace("joinpublicroom - ", string.Empty)
                .Replace(",", string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]
                .Trim();
        }

        public static async Task ForceCreateRoom(string roomCode, RoomConfig roomConfig)
        {
            if (NetworkSystem.Instance.InRoom)
            {
                await NetworkSystem.Instance.ReturnToSinglePlayer();
            }

            if (NetworkSystem.Instance is NetworkSystemPUN punNetwork)
            {
                await punNetwork.TryCreateRoom(roomCode, roomConfig);
            }
        }

        private static IEnumerator MakeRoomVisibleCoroutine()
        {
            const int maximumWaitSeconds = 20;
            int elapsedSeconds = 0;

            while (!PhotonNetwork.InRoom && elapsedSeconds < maximumWaitSeconds)
            {
                yield return new WaitForSeconds(1f);
                elapsedSeconds++;
            }

            if (!PhotonNetwork.InRoom)
            {
                yield break;
            }

            yield return new WaitForSeconds(4f);
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.CurrentRoom.IsVisible = true;
                PhotonNetwork.CurrentRoom.IsOpen = true;
            }
        }



        public class SubscriptionPatches
        {
            public static bool enabled;

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.IsLocalSubscribed))]
            public class IsLocalSubscribed
            {
                private static bool Prefix(ref bool __result)
                {
                    if (!enabled)
                        return true;

                    __result = true;
                    return false;
                }
            }

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionStatus))]
            public class LocalSubscriptionStatus
            {
                private static bool Prefix(ref SubscriptionManager.SubscriptionStatus __result)
                {
                    if (!enabled)
                        return true;

                    __result = SubscriptionManager.SubscriptionStatus.Active;
                    return false;
                }
            }

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionDetails))]
            public class LocalSubscriptionDetails
            {
                private static bool Prefix(ref SubscriptionManager.SubscriptionDetails __result)
                {
                    if (!enabled)
                        return true;

                    __result = new SubscriptionManager.SubscriptionDetails
                    {
                        active = true,
                        daysAccrued = int.MaxValue,
                        subscriptionFeatureSettings = new[] { true, true },
                        tier = int.MaxValue,
                        subscriptionActiveUntilDate = DateTime.MaxValue,
                        autoRenew = true,
                        autoRenewMonths = int.MaxValue
                    };
                    return false;
                }
            }
           
            
        }
    }
}