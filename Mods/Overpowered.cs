using ExitGames.Client.Photon;
using GorillaGameModes;
using GorillaNetworking;
using GorillaTag;
using GorillaTag.CosmeticSystem;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Classes.Scripts;
using StupidTemplate.Menu;
using StupidTemplate.Notifications;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Windows;
using Unnamed_Free.Patches;
using static StupidTemplate.Classes.GunLib;
using static StupidTemplate.Menu.Main;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using JoinType = GorillaNetworking.JoinType;
using Random = UnityEngine.Random;


namespace Unnamed_Free.Mods
{
    public class Overpowered
    {
        public static void ElevatorKickAll()
        {
            GRElevatorManager._instance.photonView.RPC("RemoteActivateTeleport", RpcTarget.Others, new object[] { GRElevatorManager._instance.currentLocation, GRElevatorManager.ElevatorLocation.GhostReactor, GRElevatorManager.LowestActorNumberInElevator() });
        }

        public static void ElevatorKickGun()
        {
            GunLib.StartGun(() =>
            {
                GRElevatorManager._instance.photonView.RPC("RemoteActivateTeleport", LockedPlayer.Creator.GetPlayerRef(), new object[] { GRElevatorManager._instance.currentLocation, GRElevatorManager.ElevatorLocation.GhostReactor, GRElevatorManager.LowestActorNumberInElevator() });
            }, true);
        }
        public static void STumpkickall()
        {
            GorillaComputer.instance.OnGroupJoinButtonPress(0, GorillaComputer.instance.friendJoinCollider);
        }



        private static float LagDelay;


        public static void LagGun()
        {
            GunLib.StartGun(() =>
            {
                if (Time.time > LagDelay)
                {
                    for (int i = 0; i < 900; i++)
                    {
                        PhotonNetwork.NetworkingClient.OpRaiseEvent(3, new Hashtable() { }, new RaiseEventOptions() { TargetActors = new int[] { LockedPlayer.creator.ActorNumber } }, SendOptions.SendUnreliable);
                    }
                    RPCProtection();
                    LagDelay = Time.time + 2.5f;
                }
            }, true);
        }

        public static void LagAll()
        {
            if (Time.time > LagDelay)
            {
                for (int i = 0; i < 900; i++)
                {
                    PhotonNetwork.NetworkingClient.OpRaiseEvent(3, new Hashtable() { }, new RaiseEventOptions() { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
                }
                RPCProtection();
                LagDelay = Time.time + 2.2f;
            }
        }
        public static void LagOnTouch()
        {
            if (Time.time > LagDelay)
            {
                foreach (VRRig vrrig in VRRigCache.ActiveRigs)
                {
                    if (vrrig != GorillaTagger.Instance.offlineVRRig &&
                        (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, vrrig.headMesh.transform.position) < 0.25f ||
                         Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, vrrig.headMesh.transform.position) < 0.25f ||
                         Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, vrrig.bodyTransform.position) < 0.25f ||
                         Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, vrrig.bodyTransform.position) < 0.25f))
                    {
                        PhotonNetwork.NetworkingClient.OpRaiseEvent(
                            3,
                            new Hashtable(),
                            new RaiseEventOptions()
                            {
                                TargetActors = new int[] { vrrig.Creator.ActorNumber }
                            },
                            SendOptions.SendUnreliable
                        );

                        RPCProtection();
                    }
                }

                LagDelay = Time.time + 2.2f;
            }
        }//lag on touch end

        // ===== Party fields =====
        // ===== Party fields =====
        public static float partyTime;
        public static bool partyKickReconnecting;
        public static bool waitForPlayerJoin;
        public static int amountPartying;
        public static string partyLastCode;

        private static float gunKickCooldown;

        // Finds the game's party ID list whatever it's actually named/typed
        private static System.Collections.IList GetPartyIdList()
        {
            var inst = FriendshipGroupDetection.Instance;
            if (inst == null) return null;

            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (string name in new[] { "myPartyMemberIDs", "PartyMemberIDs", "partyMemberIDs" })
            {
                object v = inst.GetType().GetField(name, F)?.GetValue(inst);
                if (v == null) v = inst.GetType().GetProperty(name, F)?.GetValue(inst);
                if (v is System.Collections.IList l && !l.IsReadOnly && !l.IsFixedSize) return l;
            }

            var names = inst.GetType().GetMembers(F)
                .Where(m => m.Name.ToLower().Contains("party"))
                .Select(m => m.MemberType + " " + m.Name);
            NotifiLib.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Party list not found, check the log.");
            Debug.Log("[Master] Party list not found. Members: " + string.Join(", ", names));
            return null;
        }

        // Kicks ONE player: everyone else in the party stays with you
        public static async void PartyKickPlayer(string userId)
        {
            if (Time.time < gunKickCooldown) return;

            if (!FriendshipGroupDetection.Instance.IsInParty)
            {
                NotifiLib.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You are not in a party.");
                gunKickCooldown = Time.time + 1f;
                return;
            }

            var ids = GetPartyIdList();
            if (ids == null || ids.Count <= 1)
            {
                gunKickCooldown = Time.time + 1f;
                return;
            }

            string myId = PhotonNetwork.LocalPlayer.UserId;

            bool found = false;
            foreach (var o in ids) if (o as string == userId) { found = true; break; }

            if (userId == myId || !found)
            {
                NotifiLib.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> That player is not in your party.");
                gunKickCooldown = Time.time + 1f;
                return;
            }

            gunKickCooldown = Time.time + 3f;

            partyLastCode = PhotonNetwork.CurrentRoom.Name;
            waitForPlayerJoin = false;

            // Back up the real list so it can be restored after
            var original = new System.Collections.Generic.List<object>();
            foreach (var o in ids) original.Add(o);

            try
            {
                // Keep only you + the target while the join command fires
                for (int i = ids.Count - 1; i >= 0; i--)
                {
                    string id = ids[i] as string;
                    if (id != myId && id != userId) ids.RemoveAt(i);
                }

                PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(Important.RandomRoomName(), JoinType.ForceJoinWithParty);
                partyTime = Time.time + 0.25f;
                partyKickReconnecting = false;
                amountPartying = 1;
                NotifiLib.SendNotification("<color=grey>[</color><color=purple>PARTY</color><color=grey>]</color> Kicking 1 party member, please be patient..");

                await System.Threading.Tasks.Task.Delay(1500);
            }
            finally
            {
                // Always restore the real party list
                ids.Clear();
                foreach (var o in original) ids.Add(o);
            }
        }

        // Point at a party member and pull the trigger: only that player gets kicked
        public static void PartyKickGun()
        {
            GunLib.StartGun(() =>
            {
                if (GunLib.LockedPlayer == null)
                    return;

                PartyKickPlayer(GunLib.LockedPlayer.creator.UserId);
            }, true);
        }



       

        public static bool previousPartyTrigger;
        private static float instantPartyDelay;
        public static void InstantParty()
        {
            if (Time.time > instantPartyDelay)
            {
                instantPartyDelay = Time.time + 0.1f;

                FriendshipGroupDetection.Instance.suppressPartyCreationUntilTimestamp = 0f;
                FriendshipGroupDetection.Instance.groupCreateAfterTimestamp = 0f;

                List<int> provisionalMembers = FriendshipGroupDetection.Instance.playersInProvisionalGroup;

                if (provisionalMembers.Count > 0)
                {
                    Color targetColor = GTColor.RandomHSV(FriendshipGroupDetection.Instance.braceletRandomColorHSVRanges);
                    FriendshipGroupDetection.Instance.myBraceletColor = targetColor;

                    List<int> members = new List<int> { PhotonNetwork.LocalPlayer.ActorNumber };
                    members.AddRange(from player in PhotonNetwork.PlayerListOthers where FriendshipGroupDetection.Instance.IsInMyGroup(player.UserId) || provisionalMembers.Contains(player.ActorNumber) select player.ActorNumber);

                    FriendshipGroupDetection.Instance.SendPartyFormedRPC(
                        FriendshipGroupDetection.PackColor(targetColor),
                        members.ToArray(),
                        false
                    );

                    RPCProtection();
                }
            }
        }
        private static float lagdelay;
        internal static void LagMethod(int p, float t, VRRig player)
        {
            if (Time.time <= lagdelay) return;
            for (int i = 0; i < p; i++) PhotonNetwork.NetworkingClient.LoadBalancingPeer.OpRaiseEvent(3, new ExitGames.Client.Photon.Hashtable(), new RaiseEventOptions { TargetActors = new int[] { player.Creator.ActorNumber } }, SendOptions.SendUnreliable);
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
            lagdelay = Time.time + t;
            RPCProtection();
        }
        public static void LagGunV2()
        {
            GunLib.StartGun(() =>
            {
                if (Time.time > LagDelay)
                {
                    LagMethod(1, 900f, LockedPlayer);
                    RPCProtection(); 
                    LagDelay = Time.time + 2.5f;
                }
            }, true);
        }
    }
}