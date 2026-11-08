using GorillaLocomotion;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using static StupidTemplate.Classes.RigManager;
using static StupidTemplate.Menu.Main;
using Object = UnityEngine.Object;
using System.Collections;

namespace StupidTemplate.Mods
{
    public class Safety
    {
        public static VRRig reportRig;
        public static void AntiReport(System.Action<VRRig, Vector3> onReport)
        {
            if (!NetworkSystem.Instance.InRoom) return;

            if (reportRig != null)
            {
                onReport?.Invoke(reportRig, reportRig.transform.position);
                reportRig = null;
                return;
            }

            foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
            {
                if (line.linePlayer != NetworkSystem.Instance.LocalPlayer) continue;
                Transform report = line.reportButton.gameObject.transform;

                foreach (var vrrig in from vrrig in VRRigCache.ActiveRigs where !vrrig.isLocal let D1 = Vector3.Distance(vrrig.rightHandTransform.position, report.position) let D2 = Vector3.Distance(vrrig.leftHandTransform.position, report.position) where D1 < 0.35f || D2 < 0.35f select vrrig)
                    onReport?.Invoke(vrrig, report.transform.position);
            }
        }

        private static float _lastResetTime;
        public static void AntiRPCBan()
        {
            if (PhotonNetwork.InRoom && Time.time >= _lastResetTime + 0.7f)
            {
                _lastResetTime = Time.time;
                MonkeAgent[] agents = Object.FindObjectsOfType<MonkeAgent>();
                foreach (MonkeAgent agent in agents)
                {
                    agent.rpcErrorMax = int.MaxValue;
                    agent.logErrorMax = int.MaxValue;
                    agent.lastCheck = 0f;
                }
                FlushAndReplayNetworkTraffic();
            }
        }

        public static float antiReportDelay;
        public static void AntiReportDisconnect()
        {
            AntiReport((vrrig, position) =>
            {
                NetworkSystem.Instance.ReturnToSinglePlayer();

                if (!(Time.time > antiReportDelay)) return;
                antiReportDelay = Time.time + 1f;
                NotifiLib.SendNotification("<color=grey>[</color><color=purple>ANTI-REPORT</color><color=grey>]</color> " + GetPlayerFromVRRig(vrrig).NickName + " attempted to report you, you have been disconnected.");
            });
        }
        

        

            public static void FlushAndReplayNetworkTraffic()
            {
                ApplyUnlimitedNetworkLimits();
                try
                {
                    ReplayCapturedPacketBurst();
                }
                catch (Exception)
                {
                }
                PhotonNetwork.SendAllOutgoingCommands();
        }
        private static byte[] CapturedOutgoingPacket;
        public static void ReplayCapturedPacketBurst()
        {
            LoadBalancingPeer peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
            Type peerType = peer.GetType();
            MethodInfo sendReliable = peerType.GetMethod("SendReliable", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo sendUnreliable = peerType.GetMethod("SendUnreliable", BindingFlags.Instance | BindingFlags.NonPublic);
            if (CapturedOutgoingPacket != null)
            {
                MethodInfo sender = sendReliable ?? sendUnreliable;
                if (sender != null)
                {
                    for (int i = 0; i < 1300; i++)
                    {
                        sender.Invoke(peer, new object[] { CapturedOutgoingPacket });
                    }
                }
            }
            FieldInfo queueField = peerType.GetField("outgoingStreamQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            if (queueField?.GetValue(peer) is IList queue && queue.Count > 0)
            {
                CapturedOutgoingPacket = queue[queue.Count - 1] as byte[];
            }
        }
        public static void ApplyUnlimitedNetworkLimits()
        {
            try
            {
                MonkeAgent agent = MonkeAgent.instance;
                agent.rpcErrorMax = int.MaxValue;
                agent.rpcCallLimit = int.MaxValue;
                agent.logErrorMax = int.MaxValue;
                PhotonNetwork.MaxResendsBeforeDisconnect = int.MaxValue;
                PhotonNetwork.QuickResends = int.MaxValue;
            }
            catch (Exception)
            {
            }
        }
    }
}