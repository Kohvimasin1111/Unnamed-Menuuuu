using BepInEx;
using GorillaExtensions;
using GorillaGameModes;
using GorillaNetworking;
using GorillaTagScripts;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Debug = UnityEngine.Debug;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Object = UnityEngine.Object;

namespace StupidTemplate.Classes.Scripts
{
    public static class Important
    {
        // =====================================================================
        //  Helpers (replace ii's NotificationManager / LogManager / CoroutineManager)
        // =====================================================================
        private static MonoBehaviour Host => GorillaTagger.Instance;

        private static MethodInfo notifyMethod;
        private static bool notifyResolved;

        public static void Notify(string message, int time = 4000)
        {
            Debug.Log("[Important] " + System.Text.RegularExpressions.Regex.Replace(message, "<.*?>", ""));
            try
            {
                if (!notifyResolved)
                {
                    notifyResolved = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type t = null;
                        try { t = asm.GetTypes().FirstOrDefault(x => x.Name == "NotifiLib"); } catch { }
                        if (t == null) continue;
                        notifyMethod = t.GetMethod("SendNotification", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                        if (notifyMethod != null) break;
                    }
                }
                notifyMethod?.Invoke(null, new object[] { message });
            }
            catch { }
        }

        private static void Log(string message) => Debug.Log("[Important] " + message);
        private static void LogError(string message) => Debug.LogError("[Important] " + message);

        private static GameObject GetObject(string path) => GameObject.Find(path);

        // =====================================================================
        //  Rooms / networking
        // =====================================================================
        public static Coroutine queueCoroutine;
        public static int reconnectDelay = 1;

        public static IEnumerator QueueRoomCoroutine(string roomName)
        {
            NetworkSystemPUN instance = (NetworkSystemPUN)NetworkSystem.Instance;

            instance.ReturnToSinglePlayer();
            yield return new WaitUntil(() => instance.netState == NetSystemState.Idle);
            yield return new WaitForSeconds(0.5f);

            while (!instance.InRoom)
            {
                PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomName, JoinType.Solo);
                yield return new WaitForSeconds(reconnectDelay);
            }
        }

        public static void QueueRoom(string roomName)
        {
            if (queueCoroutine != null)
                Host.StopCoroutine(queueCoroutine);

            queueCoroutine = Host.StartCoroutine(QueueRoomCoroutine(roomName));
        }

        public static void Reconnect()
        {
            string roomName = NetworkSystem.Instance.RoomName;

            NetworkSystem.Instance.ReturnToSinglePlayer();
            QueueRoom(roomName);
        }

        public static void CancelReconnect()
        {
            if (queueCoroutine != null)
                Host.StopCoroutine(queueCoroutine);

            NetworkSystem.Instance.netState = NetworkSystem.Instance.InRoom ? NetSystemState.InGame : NetSystemState.Idle;
        }

        public static void JoinRandom()
        {
            if (PhotonNetwork.InRoom)
            {
                NetworkSystem.Instance.ReturnToSinglePlayer();
                Host.StartCoroutine(JoinRandomDelay());
                return;
            }

            GorillaNetworkJoinTrigger trigger = PhotonNetworkController.Instance.currentJoinTrigger ?? GorillaComputer.instance.GetJoinTriggerForZone("forest");
            PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger);
        }

        public static IEnumerator JoinRandomDelay()
        {
            yield return new WaitForSeconds(1.5f);
            JoinRandom();
        }

        public static async Task ForceCreateRoom(string name, RoomConfig options)
        {
            if (NetworkSystem.Instance.InRoom)
                await NetworkSystem.Instance.ReturnToSinglePlayer();

            await (NetworkSystem.Instance as NetworkSystemPUN).TryCreateRoom(name, options);
        }

        public static bool instantCreate;
        public static void CreateRoom(string roomName, bool isPublic, JoinType roomJoinType = JoinType.Solo)
        {
            var netTrigger = PhotonNetworkController.Instance.currentJoinTrigger ?? GorillaComputer.instance.GetJoinTriggerForZone("forest");
            RoomConfig roomConfig = new RoomConfig
            {
                createIfMissing = true,
                isJoinable = true,
                isPublic = isPublic,
                MaxPlayers = RoomSystem.GetRoomSizeForCreate(netTrigger.zone, Enum.Parse<GameModeType>(GorillaComputer.instance.currentGameMode.Value, true), !isPublic, SubscriptionManager.IsLocalSubscribed()),
                CustomProps = new Hashtable
                {
                    { "gameMode", netTrigger.GetFullDesiredGameModeString() },
                    { "platform", PhotonNetworkController.Instance.platformTag },
                    { "queueName", GorillaComputer.instance.currentQueue },
                    { "language", GetCurrentLanguage() },
                    { "fan_club", SubscriptionManager.IsLocalSubscribed() ? "true" : "false" }
                }
            };

            PhotonNetworkController.Instance.currentJoinType = roomJoinType;

            if (roomJoinType == JoinType.JoinWithParty || roomJoinType == JoinType.ForceJoinWithParty)
                Task.Run(PhotonNetworkController.Instance.SendPartyFollowCommands);

            switch (roomJoinType)
            {
                case JoinType.JoinWithNearby:
                case JoinType.JoinWithElevator:
                    roomConfig.SetFriendIDs(PhotonNetworkController.Instance.FriendIDList);
                    break;
                case JoinType.JoinWithParty:
                case JoinType.ForceJoinWithParty:
                    roomConfig.SetFriendIDs(FriendshipGroupDetection.Instance.PartyMemberIDs.ToList());
                    break;
            }

            if (instantCreate)
            {
                (NetworkSystem.Instance as NetworkSystemPUN).internalState = NetworkSystemPUN.InternalState.Searching_Creating;
                _ = ForceCreateRoom(roomName, roomConfig);
            }
            else
                NetworkSystem.Instance.ConnectToRoom(roomName, roomConfig);
        }

        public static void BroadcastRoom(string roomName, bool create, string key, string shuffler)
        {
            string text = NetworkSystem.ShuffleRoomName(roomName, shuffler.Substring(2, 8), true) + "|" + NetworkSystem.ShuffleRoomName("ABCDEFGHIJKLMNPQRSTUVWXYZ123456789".Substring(NetworkSystem.Instance.currentRegionIndex, 1), shuffler[..2], true);

            BroadcastMyRoomRequest broadcastMyRoomRequest = new BroadcastMyRoomRequest
            {
                KeyToFollow = key,
                RoomToJoin = text,
                Set = create
            };

            GorillaServer.Instance.BroadcastMyRoom(broadcastMyRoomRequest, delegate { }, delegate { });
        }

        public static void ConnectToRegion(string region)
        {
            string currentRegion = PhotonNetwork.CloudRegion;
            if (!string.IsNullOrEmpty(currentRegion))
                currentRegion = currentRegion.Replace("/*", "");

            if (currentRegion != region)
                PhotonNetwork.ConnectToRegion(region);

            NetworkSystem.Instance.currentRegionIndex = Array.IndexOf(NetworkSystem.Instance.regionNames, region);

            NetworkSystemPUN punNetwork = (NetworkSystemPUN)NetworkSystem.Instance;
            for (int i = 0; i < punNetwork.regionData.Length; i++)
            {
                NetworkRegionInfo regionInfo = punNetwork.regionData[i];
                regionInfo.pingToRegion = Array.IndexOf(NetworkSystem.Instance.regionNames, regionInfo) == i ? 0 : 9999;
            }
        }

        private static readonly System.Random rng = new System.Random();

        public static string RandomString(int length = 8)
        {
            const string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
            char[] buffer = new char[length];
            for (int i = 0; i < length; i++)
                buffer[i] = chars[rng.Next(chars.Length)];
            return new string(buffer);
        }

        public static string RandomRoomName()
        {
            while (true)
            {
                string text = RandomString();
                if (GorillaComputer.instance.CheckAutoBanListForName(text))
                    return text;
            }
        }

        // =====================================================================
        //  Mic modes
        // =====================================================================
        public static string[] micModes = new string[] { "Default", "Open Talk", "Push To Talk", "Push To Mute" };
        public static int micModeIndex = 0;

        public static void CycleMicMode(bool positive)
        {
            micModeIndex += positive ? 1 : -1;

            if (micModeIndex > 3) micModeIndex = 0;
            if (micModeIndex < 0) micModeIndex = 3;

            Notify($"<color=grey>[</color><color=green>MIC</color><color=grey>]</color> Mic mode: {micModes[micModeIndex]}");
        }

        public static void RunMicMode()
        {
            if (micModeIndex == 0) return;
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.myRecorder == null || ControllerInputPoller.instance == null) return;

            var c = ControllerInputPoller.instance;
            bool pressingButton = c.leftControllerPrimaryButton || c.leftControllerSecondaryButton || c.rightControllerPrimaryButton || c.rightControllerSecondaryButton
                || (Keyboard.current != null && Keyboard.current.vKey.isPressed);
            bool targetState = GorillaTagger.Instance.myRecorder.TransmitEnabled;

            if (micModeIndex == 1) targetState = true;
            else if (micModeIndex == 2) targetState = pressingButton;
            else if (micModeIndex == 3) targetState = !pressingButton;

            if (GorillaTagger.Instance.myRecorder.TransmitEnabled != targetState)
                GorillaTagger.Instance.myRecorder.TransmitEnabled = targetState;
        }

        public static void ResetMic()
        {
            micModeIndex = 0;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.myRecorder != null)
                GorillaTagger.Instance.myRecorder.TransmitEnabled = true;
        }

        // =====================================================================
        //  Audio / performance
        // =====================================================================
        public static void FixAudioBug()
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 1024;
            AudioSettings.Reset(config);
            Notify("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Audio engine reset.");
        }

        private static float lastTime;
        public static void CapFPS(int fps)
        {
            float targetDelta = 1f / fps;
            float elapsed = Time.realtimeSinceStartup - lastTime;

            if (elapsed < targetDelta)
            {
                int sleepMs = Mathf.FloorToInt((targetDelta - elapsed) * 1000);
                if (sleepMs > 0)
                    Thread.Sleep(sleepMs);
            }

            lastTime = Time.realtimeSinceStartup;
        }

        public static void UncapFPS()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = int.MaxValue;
        }

        public static void RestoreFPS()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }

        // =====================================================================
        //  Map fixing
        // =====================================================================
        public static void FixMap()
        {
            if (ZoneManagement.instance == null)
            {
                Notify("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> The zone system is not loaded yet.");
                return;
            }

            Host.StartCoroutine(FixMapCoroutine());
        }

        private static IEnumerator FixMapCoroutine()
        {
            GTZone zone = ResolveMapZone();

            LogMapState("start");
            Notify($"<color=grey>[</color><color=cyan>MAP</color><color=grey>]</color> Reloading the world ({zone})...", 4000);

            try
            {
                ZoneManagement.SetActiveZones(new GTZone[0]);
                ZoneManagement.SetActiveZone(zone);
            }
            catch (Exception e)
            {
                LogError($"FixMap: zone reload failed: {e}");
                Notify($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Could not reload the map: {e.Message}");
                yield break;
            }

            yield return new WaitForSeconds(2f);

            if (MapIsVisible(zone))
            {
                LogMapState("zone-ok");
                Notify($"<color=grey>[</color><color=cyan>MAP</color><color=grey>]</color> The world is back ({zone}).", 4000);
                yield break;
            }

            LogMapState("zone-failed");

            if (ActivateZoneObjects(zone))
                Log($"FixMap: force-activated the {zone} root objects");

            try { ZoneManagement.SetActiveZone(zone); } catch { }

            yield return new WaitForSeconds(2f);

            bool fixedMap = MapIsVisible(zone);
            LogMapState(fixedMap ? "recovered" : "failed");

            Notify(fixedMap
                ? $"<color=grey>[</color><color=cyan>MAP</color><color=grey>]</color> The world is back ({zone})."
                : "<color=grey>[</color><color=yellow>MAP</color><color=grey>]</color> The world will not reload. Restart the game.", 8000);
        }

        private static GTZone ResolveMapZone()
        {
            try
            {
                GorillaNetworkJoinTrigger trigger = PhotonNetworkController.Instance != null ? PhotonNetworkController.Instance.currentJoinTrigger : null;
                if (trigger != null)
                    return trigger.zone;

                List<GTZone> activeZones = ZoneManagement.instance != null ? ZoneManagement.instance.activeZones : null;
                if (activeZones != null && activeZones.Count > 0)
                    return activeZones[0];
            }
            catch { }

            return GTZone.forest;
        }

        private static bool MapIsVisible(GTZone zone) =>
            IsZoneSceneLoaded(zone) && GetObject("Environment Objects/LocalObjects_Prefab") != null;

        private static bool IsZoneSceneLoaded(GTZone zone)
        {
            try { return ZoneManagement.instance != null && ZoneManagement.instance.IsSceneLoaded(zone); } catch { return false; }
        }

        private static bool ActivateZoneObjects(GTZone zone)
        {
            try
            {
                if (ZoneManagement.instance == null)
                    return false;

                GameObject[] roots = ZoneManagement.instance.GetZoneData(zone).rootGameObjects;
                if (roots == null || roots.Length == 0)
                    return false;

                foreach (GameObject root in roots)
                {
                    if (root != null && !root.activeSelf)
                        root.SetActive(true);
                }

                return true;
            }
            catch (Exception e)
            {
                LogError($"FixMap: could not activate the {zone} objects: {e.Message}");
                return false;
            }
        }

        // =====================================================================
        //  Better optimization (zone unloading)
        //  Note: ii also pairs this with a Harmony ZonePatch that stops the game
        //  re-loading zones. Without it, the game may re-activate zones on its own.
        // =====================================================================
        public static bool BetterOptimizationEnabled;
        private static float nextBetterOptCheckTime;
        private static GTZone lastOptimizedZone = GTZone.none;

        public static void EnableBetterOptimization()
        {
            BetterOptimizationEnabled = true;
            GTZone current = GetCurrentZone();
            lastOptimizedZone = current;
            UnloadInactiveMaps(current);
            RunOptimizationCleanup();
        }

        public static void DisableBetterOptimization()
        {
            BetterOptimizationEnabled = false;
            try
            {
                ZoneManagement.AddSceneToForceStayLoaded("City");
                ZoneManagement.SetActiveZone(GetCurrentZone());
            }
            catch { }
        }

        public static string GetCurrentLanguage()
        {
            try
            {
                const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }

                    foreach (var t in types)
                    {
                        if (t.Name != "LocalisationManager" && t.Name != "LocalizationManager") continue;

                        var p = t.GetProperty("CurrentLanguage", F);
                        if (p != null) return p.GetValue(null)?.ToString();

                        var f = t.GetField("CurrentLanguage", F);
                        if (f != null) return f.GetValue(null)?.ToString();
                    }
                }
            }
            catch { }

            Debug.Log("[Important] CurrentLanguage not found, defaulting to English");
            return "English";
        }

        public static void BetterOptimizationUpdate()
        {
            if (!BetterOptimizationEnabled)
                return;

            if (Time.time < nextBetterOptCheckTime)
                return;

            nextBetterOptCheckTime = Time.time + 1f;

            try
            {
                ZoneManagement.RemoveSceneFromForceStayLoaded("City");
                GTZone current = GetCurrentZone();
                if (current != lastOptimizedZone || (ZoneManagement.instance != null && ZoneManagement.instance.activeZones.Count > 1))
                {
                    lastOptimizedZone = current;
                    UnloadInactiveMaps(current);
                    RunOptimizationCleanup();
                }
            }
            catch { }
        }

        public static void OnNetworkTrigger(GorillaNetworkJoinTrigger trigger)
        {
            if (!BetterOptimizationEnabled || trigger == null)
                return;

            try
            {
                GTZone destZone = trigger.zone;
                if (destZone == GTZone.none && trigger.primaryTriggerForMyZone != null)
                    destZone = trigger.primaryTriggerForMyZone.zone;

                if (destZone == GTZone.none && !string.IsNullOrEmpty(trigger.networkZone))
                    Enum.TryParse<GTZone>(trigger.networkZone, true, out destZone);

                if (destZone == GTZone.none)
                    return;

                lastOptimizedZone = destZone;
                UnloadInactiveMaps(destZone);

                if (trigger.makeSureThisIsDisabled != null)
                {
                    foreach (var obj in trigger.makeSureThisIsDisabled)
                    {
                        if (obj != null && obj.activeSelf)
                            obj.SetActive(false);
                    }
                }

                if (trigger.makeSureThisIsEnabled != null)
                {
                    foreach (var obj in trigger.makeSureThisIsEnabled)
                    {
                        if (obj != null && !obj.activeSelf)
                            obj.SetActive(true);
                    }
                }

                RunOptimizationCleanup();
            }
            catch { }
        }

        public static void UnloadInactiveMaps(GTZone keepZone)
        {
            try
            {
                if (ZoneManagement.instance == null)
                    return;

                ZoneManagement.RemoveSceneFromForceStayLoaded("City");

                string keepSceneName = null;
                try
                {
                    var keepData = ZoneManagement.instance.GetZoneData(keepZone);
                    if (keepData != null)
                    {
                        keepData.active = true;
                        keepSceneName = keepData.sceneName;
                        if (keepData.rootGameObjects != null)
                        {
                            foreach (var root in keepData.rootGameObjects)
                            {
                                if (root != null && !root.activeSelf)
                                    root.SetActive(true);
                            }
                        }

                        if (!string.IsNullOrEmpty(keepSceneName) && !SceneManager.GetSceneByName(keepSceneName).isLoaded)
                            SceneManager.LoadSceneAsync(keepSceneName, LoadSceneMode.Additive);
                    }
                }
                catch { }

                foreach (GTZone z in Enum.GetValues(typeof(GTZone)))
                {
                    if (z == keepZone || z == GTZone.none)
                        continue;

                    try
                    {
                        var data = ZoneManagement.instance.GetZoneData(z);
                        if (data != null)
                        {
                            data.active = false;
                            if (data.rootGameObjects != null)
                            {
                                foreach (var root in data.rootGameObjects)
                                {
                                    if (root != null && root.activeSelf)
                                        root.SetActive(false);
                                }
                            }
                        }
                    }
                    catch { }
                }

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    if (!s.isLoaded || s.name == "GorillaTag" || (!string.IsNullOrEmpty(keepSceneName) && s.name == keepSceneName))
                        continue;

                    try
                    {
                        ZoneManagement.RemoveSceneFromForceStayLoaded(s.name);
                        SceneManager.UnloadSceneAsync(s, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    }
                    catch { }
                }

                try { ZoneManagement.SetActiveZone(keepZone); } catch { }
            }
            catch { }
        }

        public static void RunOptimizationCleanup()
        {
            try
            {
                Resources.UnloadUnusedAssets();
                GC.Collect();

                if (Camera.main != null && Camera.main.farClipPlane > 200f)
                    Camera.main.farClipPlane = 200f;

                QualitySettings.particleRaycastBudget = 64;
                QualitySettings.vSyncCount = 0;
            }
            catch { }
        }

        public static GTZone GetCurrentZone()
        {
            try
            {
                if (VRRig.LocalRig != null && VRRig.LocalRig.zoneEntity != null && VRRig.LocalRig.zoneEntity.currentZone != GTZone.none)
                    return VRRig.LocalRig.zoneEntity.currentZone;

                if (PhotonNetworkController.Instance != null && PhotonNetworkController.Instance.currentJoinTrigger != null && PhotonNetworkController.Instance.currentJoinTrigger.zone != GTZone.none)
                    return PhotonNetworkController.Instance.currentJoinTrigger.zone;

                if (ZoneManagement.instance != null && ZoneManagement.instance.activeZones != null && ZoneManagement.instance.activeZones.Count > 0)
                    return ZoneManagement.instance.activeZones[ZoneManagement.instance.activeZones.Count - 1];
            }
            catch { }

            return GTZone.forest;
        }

        // =====================================================================
        //  Debug dumps
        // =====================================================================
        public static void MapStateReport()
        {
            Host.StartCoroutine(MapStateReportRoutine());
        }

        private static IEnumerator MapStateReportRoutine()
        {
            yield return new WaitForSeconds(3f);
            LogMapState("boot-3s");

            yield return new WaitForSeconds(4f);
            LogMapState("boot-7s");

            yield return new WaitForSeconds(5f);
            LogMapState("boot-12s");

            yield return new WaitForSeconds(8f);
            LogMapState("boot-20s");

            yield return new WaitForSeconds(20f);
            LogMapState("boot-40s");
        }

        private static void LogMapState(string tag)
        {
            try
            {
                string scenes = "";
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    scenes += (i == 0 ? "" : ",") + SceneManager.GetSceneAt(i).name;

                ZoneManagement zoneManager = ZoneManagement.instance;
                List<GTZone> zones = zoneManager != null ? zoneManager.activeZones : null;
                string zoneList = zones == null || zones.Count == 0 ? "none" : string.Join(",", zones);

                Log($"MapState[{tag}]: scene={SceneManager.GetActiveScene().name} scenes={scenes} zones={zoneList} initialized={(zoneManager != null && zoneManager.Initialized)} localObjects={(GetObject("Environment Objects/LocalObjects_Prefab") != null)} treeRoom={(GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom") != null)} inRoom={(NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom)} netState={(NetworkSystem.Instance != null ? NetworkSystem.Instance.netState.ToString() : "none")} photon={PhotonNetwork.NetworkClientState} region={PhotonNetwork.CloudRegion}");
            }
            catch (Exception e)
            {
                LogError($"MapState[{tag}] failed: {e.Message}");
            }
        }

        public static void DumpWorldState()
        {
            Host.StartCoroutine(DumpWorldStateRoutine());
        }

        private static IEnumerator DumpWorldStateRoutine()
        {
            float startY = RigHeight();
            Log(WorldDumpLine("dump-a", startY));

            yield return new WaitForSeconds(1.5f);

            float endY = RigHeight();
            Log(WorldDumpLine("dump-b", endY));
            Log($"WorldDump: height changed by {(startY - endY):0.00} over 1.5s, positive means falling");

            Notify("World state written to the log", 6000);
        }

        private static float RigHeight()
        {
            try
            {
                return GorillaTagger.Instance != null && GorillaTagger.Instance.bodyCollider != null
                    ? GorillaTagger.Instance.bodyCollider.transform.position.y
                    : float.NaN;
            }
            catch { return float.NaN; }
        }

        private static string WorldDumpLine(string tag, float rigY)
        {
            try
            {
                Scene active = SceneManager.GetActiveScene();
                string scenes = "";

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    GameObject[] roots = scene.GetRootGameObjects();
                    string names = "";

                    for (int r = 0; r < roots.Length && r < 40; r++)
                        names += (r == 0 ? "" : ", ") + (roots[r].activeSelf ? roots[r].name : roots[r].name + "(off)");

                    scenes += $" [{scene.name} loaded={scene.isLoaded} roots={roots.Length}: {names}]";
                }

                ZoneManagement zoneManager = ZoneManagement.instance;
                List<GTZone> zones = zoneManager != null ? zoneManager.activeZones : null;
                string zoneInfo = "none";

                if (zones != null && zones.Count > 0)
                {
                    string parts = "";
                    int index = 0;

                    foreach (GTZone zone in zones)
                    {
                        bool loaded = false;
                        try { loaded = zoneManager.IsSceneLoaded(zone); } catch { }

                        int zoneRoots = -1;
                        try
                        {
                            GameObject[] roots = zoneManager.GetZoneData(zone).rootGameObjects;
                            zoneRoots = roots == null ? -1 : roots.Length;
                        }
                        catch { }

                        parts += (index++ == 0 ? "" : ", ") + $"{zone}(loaded={loaded} roots={zoneRoots})";
                    }

                    zoneInfo = parts;
                }

                bool masterConnected = false;
                try { masterConnected = GorillaComputer.instance != null && GorillaComputer.instance.isConnectedToMaster; } catch { }

                return $"WorldDump[{tag}]: rigY={rigY:0.00} scene={active.name} scenes={scenes} zones={zoneInfo} environment={FindEnvironment()} netState={(NetworkSystem.Instance != null ? NetworkSystem.Instance.netState.ToString() : "none")} photon={PhotonNetwork.NetworkClientState} region={PhotonNetwork.CloudRegion} masterConnected={masterConnected}";
            }
            catch (Exception e)
            {
                return $"WorldDump[{tag}] failed: {e}";
            }
        }

        private static string FindEnvironment()
        {
            try
            {
                List<string> found = new List<string>();
                int visited = 0;

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root == null)
                        continue;

                    if (root.name == "Environment Objects")
                        found.Add($"root Environment Objects active={root.activeSelf}");

                    WalkForEnvironment(root.transform, found, ref visited);
                }

                return found.Count == 0 ? "nothing named Environment Objects or LocalObjects_Prefab in the active scene" : string.Join(" | ", found);
            }
            catch (Exception e) { return $"scan failed: {e.Message}"; }
        }

        private static void WalkForEnvironment(Transform target, List<string> found, ref int visited)
        {
            if (visited > 60000 || found.Count >= 6)
                return;

            for (int i = 0; i < target.childCount; i++)
            {
                Transform child = target.GetChild(i);
                visited++;

                if (child.name == "LocalObjects_Prefab" || child.name == "TriggerZones_Prefab")
                    found.Add($"{child.name} activeInHierarchy={child.gameObject.activeInHierarchy} activeSelf={child.gameObject.activeSelf}");

                WalkForEnvironment(child, found, ref visited);
            }
        }

        public static void CopyPlayerPosition()
        {
            string Line(string label, Transform p) =>
                $"{label}\nnew Vector3({p.position.x}f, {p.position.y}f, {p.position.z}f);" +
                $"new Quaternion({p.rotation.x}f, {p.rotation.y}f, {p.rotation.z}f, {p.rotation.w}f);\n\n";

            GUIUtility.systemCopyBuffer =
                Line("Body", GorillaTagger.Instance.bodyCollider.transform) +
                Line("Head", GorillaTagger.Instance.headCollider.transform) +
                Line("Left Hand", VRRig.LocalRig.leftHand.rigTarget.transform) +
                Line("Right Hand", VRRig.LocalRig.rightHand.rigTarget.transform);
        }

        // =====================================================================
        //  Game / system utilities
        // =====================================================================
        public static void RestartGame()
        {
            string bat = Path.Combine(Path.GetTempPath(), "gt_restart.bat");
            File.WriteAllText(bat,
                "@echo off\r\n" +
                "echo Your game is restarting, please wait...\r\n" +
                ":WAIT_LOOP\r\n" +
                "tasklist /FI \"IMAGENAME eq Gorilla Tag.exe\" | find /I \"Gorilla Tag.exe\" >nul\r\n" +
                "if %ERRORLEVEL%==0 (\r\n  timeout /t 1 >nul\r\n  goto WAIT_LOOP\r\n)\r\n" +
                "start steam://run/1533390\r\n" +
                "exit\r\n");

            Process.Start(bat);
            Application.Quit();
        }

        public static void OpenGorillaTagFolder()
        {
            Process.Start(Directory.GetParent(Application.dataPath).FullName);
        }

        public static string serverLink = "https://discord.gg/YOUR_INVITE";
        public static void JoinDiscord() => Process.Start(serverLink);

        private static bool acceptedTOS;
public static void AcceptTOS()
{
    GameObject roomObject = GetObject("Miscellaneous Scripts/PrivateUIRoom_HandRays");
    if (roomObject == null) return;

    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    object Get(object o, string name)
    {
        var t = o.GetType();
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(o);
        Debug.Log("[Important] Member not found: " + name);
        return null;
    }

    void Invoke(object o, string name)
    {
        var m = o.GetType().GetMethods(F).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 0);
        if (m != null) m.Invoke(o, null);
        else Debug.Log("[Important] Method not found: " + name);
    }

    Component room = roomObject.GetComponent("PrivateUIRoom");
    Component rays = roomObject.GetComponent("HandRayController");

    if (!acceptedTOS && room != null && Get(room, "inOverlay") is bool inOverlay && inOverlay)
    {
        if (rays != null) Invoke(rays, "DisableHandRays");
        Invoke(room, "StopOverlay");

        try
        {
            GorillaTagger.Instance.tapHapticStrength = 0.5f;
            GorillaSnapTurn.LoadSettingsFromCache();
        }
        catch { }

        acceptedTOS = true;
    }

    if (roomObject.activeSelf)
        roomObject.SetActive(false);
}

        public static void RedeemShinyRocks()
        {
            Host.StartCoroutine(RedeemShinyRocksRoutine());
        }

        private static IEnumerator RedeemShinyRocksRoutine()
        {
            Task<GetPlayerData_Data> newSessionDataTask = KIDManager.TryGetPlayerData(true);

            while (!newSessionDataTask.IsCompleted)
                yield return null;
            if (newSessionDataTask.IsFaulted)
            {
                Notify("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to redeem shiny rocks.");
                yield break;
            }

            GetPlayerData_Data newSessionData = newSessionDataTask.Result;
            if (newSessionData.responseType == GetSessionResponseType.NOT_FOUND)
            {
                Task optInTask = KIDManager.Server_OptIn();

                while (!optInTask.IsCompleted)
                    yield return null;
                if (optInTask.IsFaulted)
                {
                    Notify("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to redeem shiny rocks.");
                    yield break;
                }

                Notify("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Successfully redeemed shiny rocks!");
                CosmeticsController.instance.GetCurrencyBalance();
            }
            else
                Notify("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You have already redeemed the shiny rocks.");
        }

        private static bool reportMenuToggle;
        public static void OculusReportMenu()
        {
            bool leftPrimary = ControllerInputPoller.instance.leftControllerPrimaryButton;
            if (leftPrimary && !reportMenuToggle)
            {
                GorillaMetaReport metaReporting = GetObject("Miscellaneous Scripts").transform.Find("MetaReporting").GetComponent<GorillaMetaReport>();
                metaReporting.gameObject.SetActive(true);
                metaReporting.enabled = true;

                metaReporting.StartOverlay();
            }
            reportMenuToggle = leftPrimary;
        }

        public static GameObject physicalQuitBox;
        public static void PhysicalQuitbox()
        {
            GameObject quitBox = GetObject("Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/QuitBox");
            if (quitBox == null) return;

            physicalQuitBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            physicalQuitBox.transform.position = quitBox.transform.position;
            physicalQuitBox.transform.rotation = quitBox.transform.rotation;
            physicalQuitBox.transform.localScale = quitBox.transform.localScale;

            quitBox.SetActive(false);
        }

        public static void DisablePhysicalQuitbox()
        {
            Object.Destroy(physicalQuitBox);
            GameObject quitBox = GetObject("Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/QuitBox");
            if (quitBox != null) quitBox.SetActive(true);
        }

        // =====================================================================
        //  Players
        // =====================================================================
        public static void BlockOnMute()
        {
            foreach (VRRig rig in VRRigCache.ActiveRigs.Where(rig => !rig.isLocal && rig.muted))
                rig.transform.position = rig.syncPos - (Vector3.up * 99999f);
        }

        public static void DisablePitchScaling()
        {
            foreach (var vrrig in VRRigCache.ActiveRigs.Where(vrrig => !vrrig.isLocal))
            {
                vrrig.voicePitchForRelativeScale = new AnimationCurve(
                    new Keyframe(0f, 1f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f)
                );
            }
        }

        public static void EnablePitchScaling()
        {
            foreach (var vrrig in VRRigCache.ActiveRigs.Where(vrrig => !vrrig.isLocal))
                vrrig.voicePitchForRelativeScale = VRRig.LocalRig.voicePitchForRelativeScale;
        }

        // ii also patches the loudness send; this only flips the rig flag
        public static void DisableMouthMovement() => VRRig.LocalRig.shouldSendSpeakingLoudness = false;
        public static void EnableMouthMovement() => VRRig.LocalRig.shouldSendSpeakingLoudness = true;

        private static bool lastSteam;
        private static System.Reflection.FieldInfo[] cosmeticStringFields;

        public static void SteamDetector()
        {
            // Find every string field on VRRig with "cosmetic" in its name (done once)
            if (cosmeticStringFields == null)
            {
                cosmeticStringFields = typeof(VRRig)
                    .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(f => f.FieldType == typeof(string) && f.Name.ToLower().Contains("cosmetic"))
                    .ToArray();

                Debug.Log("[Important] Steam detector watching fields: " + string.Join(", ", cosmeticStringFields.Select(f => f.Name)));
            }

            bool IsSteam(VRRig rig)
            {
                foreach (var f in cosmeticStringFields)
                {
                    string value = f.GetValue(rig) as string;
                    if (!string.IsNullOrEmpty(value) && value.Contains("S. FIRST LOGIN"))
                        return true;
                }
                return false;
            }

            VRRig steamRig = null;
            foreach (var rig in VRRigCache.ActiveRigs)
            {
                if (!rig.isLocal && IsSteam(rig)) { steamRig = rig; break; }
            }

            bool playerOnSteam = steamRig != null;

            if (playerOnSteam && !lastSteam)
            {
                string name = steamRig.OwningNetPlayer != null ? steamRig.OwningNetPlayer.NickName : "A player";
                Notify($"<color=grey>[</color><color=red>STEAM</color><color=grey>]</color> {name} is on Steam.");
            }

            lastSteam = playerOnSteam;
        }

        // =====================================================================
        //  Controls / input
        // =====================================================================
        public static void ForceEnableHands(bool enabled = true)
        {
            if (!XRSettings.isDeviceActive)
                return;

            ConnectedControllerHandler.Instance.overrideLeftEnable = enabled;
            ConnectedControllerHandler.Instance.overrideRightEnable = enabled;

            ConnectedControllerHandler.Instance.UpdateControllerStates();
        }

        public static void DisableForceHands() => ForceEnableHands(false);

        public static void PCControllerEmulation()
        {
            var c = ControllerInputPoller.instance;
            var input = UnityInput.Current;

            c.rightControllerPrimaryButton |= input.GetKey(KeyCode.E);
            c.rightControllerSecondaryButton |= input.GetKey(KeyCode.R);

            c.leftControllerPrimaryButton |= input.GetKey(KeyCode.F);
            c.leftControllerSecondaryButton |= input.GetKey(KeyCode.G);

            c.leftGrab |= input.GetKey(KeyCode.LeftBracket);
            c.leftControllerGripFloat += input.GetKey(KeyCode.LeftBracket) ? 1f : 0f;

            c.rightGrab |= input.GetKey(KeyCode.RightBracket);
            c.rightControllerGripFloat += input.GetKey(KeyCode.RightBracket) ? 1f : 0f;

            c.rightControllerTriggerButton |= input.GetKey(KeyCode.Equals);
            c.rightControllerIndexFloat += input.GetKey(KeyCode.Equals) ? 1f : 0f;

            c.leftControllerTriggerButton |= input.GetKey(KeyCode.Minus);
            c.leftControllerIndexFloat += input.GetKey(KeyCode.Minus) ? 1f : 0f;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private static bool wasDown;
        private static bool mouseUnlocked;

        // Click menu buttons with the mouse. Shift toggles the cursor.
        // This calls OnTriggerEnter on the template's Button script using the "buttonPresser" collider.
        public static void PCButtonClick()
        {
            if (Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame)
                mouseUnlocked = !mouseUnlocked;

            Cursor.lockState = mouseUnlocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = mouseUnlocked;

            bool isDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool clicked = isDown && !wasDown;
            wasDown = isDown;

            if (!clicked || Mouse.current == null) return;

            Camera cam = null;
            foreach (Camera c in Camera.allCameras)
                if (c.isActiveAndEnabled && c.stereoTargetEye == StereoTargetEyeMask.None && c.targetTexture == null) { cam = c; break; }

            cam = cam ?? Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            ray.origin += ray.direction * 0.1f;

            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;

                MonoBehaviour[] scripts = hit.collider.GetComponentsInParent<MonoBehaviour>();
                bool isBtn = false;

                foreach (var s in scripts)
                    if (s != null && (s.GetType().Name.Contains("Button") || s.GetType().Name.Contains("Pressable"))) { isBtn = true; break; }

                if (!isBtn) continue;

                GameObject presserObject = GameObject.Find("buttonPresser");
                Collider presser = presserObject != null ? presserObject.GetComponent<Collider>() : null;

                GameObject fake = null;
                if (presser == null)
                {
                    fake = new GameObject("fakePresser");
                    fake.transform.position = hit.point;
                    SphereCollider col = fake.AddComponent<SphereCollider>();
                    col.isTrigger = true;
                    fake.AddComponent<Rigidbody>().isKinematic = true;
                    fake.AddComponent<GorillaTriggerColliderHandIndicator>();
                    presser = col;
                }

                hit.collider.SendMessageUpwards("OnTriggerEnter", presser, SendMessageOptions.DontRequireReceiver);
                foreach (var s in scripts)
                    if (s != null) s.GetType().GetMethod("OnTriggerEnter", (BindingFlags)54)?.Invoke(s, new object[] { presser });

                if (fake != null) Object.Destroy(fake, 0.1f);
                break;
            }
        }

        public static void DisablePCButtonClick()
        {
            if (mouseUnlocked)
            {
                mouseUnlocked = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // =====================================================================
        //  First person camera
        // =====================================================================
#pragma warning disable CS0618
        private static Camera _tpc;
        private static Camera TPC
        {
            get
            {
                if (_tpc == null)
                {
                    GameObject go = GameObject.Find("Shoulder Camera");
                    if (go != null) _tpc = go.GetComponent<Camera>();
                }
                return _tpc;
            }
        }

        private static bool wasenabled = true;

        public static void EnableFPC()
        {
            if (TPC != null)
                wasenabled = TPC.gameObject.transform.Find("CM vcam1").GetComponent<CinemachineVirtualCamera>().enabled;
        }

        public static float zoomFOV = 35f;
        public static void MoveFPC()
        {
            if (TPC == null) return;

            float FOV = 90f;
            if (Keyboard.current.cKey.isPressed)
            {
                Vector2 scroll = Mouse.current.scroll.ReadValue();
                zoomFOV += -scroll.y * 5f;
                zoomFOV = Mathf.Clamp(zoomFOV, 10f, 90f);
                TPC.fieldOfView = Mathf.Lerp(TPC.fieldOfView, zoomFOV, 0.1f);
            }
            else
            {
                zoomFOV = 35f;
                TPC.fieldOfView = Mathf.Lerp(TPC.fieldOfView, FOV, 0.1f);
            }

            TPC.gameObject.transform.Find("CM vcam1").GetComponent<CinemachineVirtualCamera>().enabled = false;
            TPC.gameObject.transform.position = Keyboard.current.cKey.isPressed
                ? Vector3.Lerp(TPC.transform.position, GorillaTagger.Instance.headCollider.transform.position, 0.1f)
                : GorillaTagger.Instance.headCollider.transform.position;
            TPC.gameObject.transform.rotation = Quaternion.Lerp(TPC.transform.rotation, GorillaTagger.Instance.headCollider.transform.rotation, 0.075f);
        }

        public static void DisableFPC()
        {
            if (TPC != null)
            {
                TPC.GetComponent<Camera>().fieldOfView = 60f;
                TPC.gameObject.transform.Find("CM vcam1").GetComponent<CinemachineVirtualCamera>().enabled = wasenabled;
            }
        }
#pragma warning restore CS0618

        // =====================================================================
        //  Media keys (works with any player that listens for Windows media keys)
        // =====================================================================
        private enum VirtualKeyCodes : uint
        {
            NEXT_TRACK = 0xB0,
            PREVIOUS_TRACK = 0xB1,
            PLAY_PAUSE = 0xB3,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern void keybd_event(uint bVk, uint bScan, uint dwFlags, uint dwExtraInfo);
        private static void SendKey(VirtualKeyCodes key) => keybd_event((uint)key, 0, 0, 0);

        public static void PreviousTrack() => SendKey(VirtualKeyCodes.PREVIOUS_TRACK);
        public static void PauseTrack() => SendKey(VirtualKeyCodes.PLAY_PAUSE);
        public static void SkipTrack() => SendKey(VirtualKeyCodes.NEXT_TRACK);
    }
}