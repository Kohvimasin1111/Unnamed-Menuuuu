using System.Collections.Generic;
using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using GorillaTagScripts;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.ImGuiUI;
using StupidTemplate.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using static StupidTemplate.Settings;

namespace StupidTemplate.Classes
{
    public static class GunLib
    {
        public static VRRig LockedPlayer;
        public static GorillaRopeSwing LockedRope;

        private static GameObject pointer;
        private static LineRenderer line;

        private const int BeamSegments = 32;
        private const float WanderRadius = 0.12f;
        private const float WanderSpeed = 1.8f;
        private const float MaxDistance = 512f;
        private const float AimAssistAngle = 8f;
        private const float RopeAimAssistDistance = 1.5f; // how close the aim line has to be to a rope
        private const float TargetHeightOffset = 0.2f;
        private const float TwirlRadius = 0.06f;
        private const float TwirlSpeed = 8f;
        private const float ZigzagAmplitude = 0.08f;

        // ---------- Settings ----------
        public static bool gunLeft = false;      // true = gun on left hand
        public static bool trigger = false;      // true while trigger is held on a shooting gun

        public static int beamStyle = 1;
        public static readonly string[] beamStyleNames = { "Normal", "Curved", "Twirly", "Zigzag", "Morphine", "Flare", "Tentacle" };
        public const string BeamStyleButtonText = "Gun Line Style";

        public static float gunLineWidth = 0.025f;
        private static int lineSizeIndex = 12;
        private static readonly float[] LineScales = { 0.005f, 0.01f, 0.015f, 0.02f, 0.025f, 0.03f, 0.04f, 0.05f };
        public const string LineSizeButtonText = "Gun Line Size";

        public static float sphereSize = 0.12f;
        private static int sphereSizeIndex = 3;
        private static readonly float[] SphereScales = { 0.04f, 0.06f, 0.09f, 0.12f, 0.16f, 0.2f };
        public const string SphereSizeButtonText = "Gun Sphere Size";

        private static Vector3 wanderOffset;
        private static Vector3 targetWanderOffset;

        // PC cursor handling: the cursor is freed while aiming so the ray follows the mouse
        private static bool gunUnlockedCursor;
        private static CursorLockMode gunSavedLock;
        private static bool gunSavedVisible;

        // Morphine rope state (local gun)
        private const int RopePoints = 60;
        private static readonly Vector3[] ropePos = new Vector3[RopePoints];
        private static readonly Vector3[] ropeVel = new Vector3[RopePoints];
        private static bool ropeReady = false;

        public class GunLibData
        {
            public bool isShooting;
            public bool isTriggered;
            public bool isLocked;
            public VRRig lockedPlayer;
            public GorillaRopeSwing lockedRope;
            public Vector3 hitPosition;
            public RaycastHit RaycastHit;
        }

        private static readonly GunLibData data = new GunLibData();

        // ===== Menu cycle methods =====
        public static void CycleBeamStyle()
        {
            beamStyle = (beamStyle + 1) % beamStyleNames.Length;
            ropeReady = false;

            ButtonInfo button = Main.GetIndex(BeamStyleButtonText);
            if (button != null)
                button.overlapText = "Gun Line: " + beamStyleNames[beamStyle];

            SyncGunProperties();
        }

        public static void CycleLineSize()
        {
            lineSizeIndex = (lineSizeIndex + 1) % LineScales.Length;
            gunLineWidth = LineScales[lineSizeIndex];

            ButtonInfo button = Main.GetIndex(LineSizeButtonText);
            if (button != null)
                button.overlapText = "Gun Line Size: " + gunLineWidth;

            SyncGunProperties();
        }

        public static void CycleSphereSize()
        {
            sphereSizeIndex = (sphereSizeIndex + 1) % SphereScales.Length;
            sphereSize = SphereScales[sphereSizeIndex];

            ButtonInfo button = Main.GetIndex(SphereSizeButtonText);
            if (button != null)
                button.overlapText = "Gun Sphere Size: " + sphereSize;

            SyncGunProperties();
        }

        // ===== Juul-style API =====
        public static bool IsXRDeviceActive() => XRSettings.isDeviceActive;
        public static Vector3 GetPointerPos() => pointer != null ? pointer.transform.position : Vector3.zero;
        public static void CleanupPointer() => GunCleanUp();

        public static void StartPointerSystem(System.Action action, bool lockOn) => StartGun(action, lockOn);
        public static void GunPreview() => StartGun(() => { }, false);
        public static void GunPreviewLock() => StartGun(() => { }, true);

        // Runs the action every frame while you aim and hold trigger.
        // lockOn = true: only runs while a player is locked.
        public static void StartGun(System.Action action, bool lockOn)
        {
            GunLibData gun = ShootLock();

            trigger = gun.isShooting && gun.isTriggered;

            if (!gun.isShooting || !gun.isTriggered)
                return;

            if (lockOn && LockedPlayer == null)
                return;

            action?.Invoke();
        }

        // ===== Targeting helpers =====
        private static bool IsValidTarget(VRRig rig)
        {
            if (rig == null || rig.gameObject == null || !rig.gameObject.activeInHierarchy)
                return false;

            if (GorillaTagger.Instance != null && rig == GorillaTagger.Instance.offlineVRRig)
                return false;

            return true;
        }

        private static Vector3 GetTargetPoint(VRRig rig)
        {
            return rig.transform.position + Vector3.up * TargetHeightOffset;
        }

        private static VRRig FindTarget(Ray ray, bool hitSomething, RaycastHit hit)
        {
            if (hitSomething && hit.collider != null)
            {
                VRRig direct = hit.collider.GetComponentInParent<VRRig>();
                if (IsValidTarget(direct))
                    return direct;
            }

            if (GorillaParent.instance == null || VRRigCache.ActiveRigs == null)
                return null;

            VRRig best = null;
            float bestAngle = AimAssistAngle;

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!IsValidTarget(rig))
                    continue;

                Vector3 toTarget = GetTargetPoint(rig) - ray.origin;
                float distance = toTarget.magnitude;

                if (distance > MaxDistance || distance < 0.01f)
                    continue;

                if (hitSomething && hit.distance + 0.75f < distance)
                    continue;

                float angle = Vector3.Angle(ray.direction, toTarget);

                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = rig;
                }
            }

            return best;
        }

        // finds the rope you're pointing at, and the point on it closest to your aim line
        private static GorillaRopeSwing FindRope(Ray ray, bool hitSomething, RaycastHit hit, out Vector3 point)
        {
            point = Vector3.zero;

            if (hitSomething && hit.collider != null)
            {
                GorillaRopeSwing direct = hit.collider.GetComponentInParent<GorillaRopeSwing>();
                if (direct != null)
                {
                    point = hit.point;
                    return direct;
                }
            }

            if (RopeSwingManager.instance == null) return null;

            GorillaRopeSwing best = null;
            float bestDist = RopeAimAssistDistance;

            foreach (GorillaRopeSwing rope in RopeSwingManager.instance.ropes.Values)
            {
                if (rope == null) continue;

                // ropes hang down from their root, so check every segment
                foreach (Transform t in rope.GetComponentsInChildren<Transform>())
                {
                    Vector3 toPoint = t.position - ray.origin;
                    if (Vector3.Dot(toPoint, ray.direction) < 0f) continue;
                    if (toPoint.magnitude > MaxDistance) continue;

                    float off = Vector3.Cross(ray.direction, toPoint).magnitude;
                    if (off < bestDist)
                    {
                        bestDist = off;
                        best = rope;
                        point = t.position;
                    }
                }
            }

            return best;
        }

        // ===== PC aiming helpers =====
        private static bool IsRendering(Camera c)
        {
            return c != null && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null;
        }

        // The ray has to come from the camera that is really drawing your view,
        // otherwise the pointer lands next to the cursor instead of under it.
        private static Camera GetAimCamera()
        {
            Camera tpc = Main.TPC;
            if (IsRendering(tpc))
                return tpc;

            if (GorillaTagger.Instance != null && GorillaTagger.Instance.mainCamera != null)
            {
                Camera main = GorillaTagger.Instance.mainCamera.GetComponent<Camera>();
                if (IsRendering(main))
                    return main;
            }

            Camera fallback = Camera.main;
            if (IsRendering(fallback))
                return fallback;

            // last resort: the enabled screen camera drawn last
            Camera best = null;
            foreach (Camera c in Camera.allCameras)
            {
                if (!IsRendering(c)) continue;
                if (best == null || c.depth > best.depth)
                    best = c;
            }
            return best;
        }

        // Keeps the cursor free in LateUpdate (after the game's own scripts) while aiming
        private class CursorKeeper : MonoBehaviour
        {
            public static bool Hold;
            private static CursorKeeper instance;

            public static void Ensure()
            {
                if (instance != null) return;

                GameObject go = new GameObject("GunLibCursorKeeper");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CursorKeeper>();
            }

            private void LateUpdate()
            {
                if (!Hold || ImGuiHost.Visible)
                    return;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // ===== Main gun =====
        // mode 0 = players (default), mode 1 = ropes only
        public static GunLibData ShootLock(int mode = 0)
        {
            if (GorillaTagger.Instance == null || GTPlayer.Instance == null)
            {
                GunCleanUp();
                return data;
            }

            Transform beamOrigin = gunLeft
                ? GorillaTagger.Instance.leftHandTransform
                : GorillaTagger.Instance.rightHandTransform;
            Ray ray;

            if (XRSettings.isDeviceActive)
            {
                if (ControllerInputPoller.instance == null)
                {
                    GunCleanUp();
                    return data;
                }

                data.isShooting = gunLeft
                    ? ControllerInputPoller.instance.leftGrab
                    : ControllerInputPoller.instance.rightGrab;
                data.isTriggered = (gunLeft
                    ? ControllerInputPoller.instance.leftControllerIndexFloat
                    : ControllerInputPoller.instance.rightControllerIndexFloat) > 0.1f;

                ray = new Ray(beamOrigin.position, beamOrigin.forward);
            }
            else
            {
                Camera camera = GetAimCamera();

                if (camera == null || Mouse.current == null)
                {
                    GunCleanUp();
                    return data;
                }

                // don't shoot while clicking menu rows
                data.isShooting = Mouse.current.rightButton.isPressed && !ImGuiHost.Visible;
                data.isTriggered = Mouse.current.leftButton.isPressed;

                // free the cursor while aiming so the ray follows it
                if (data.isShooting && !ImGuiHost.Visible)
                {
                    if (!gunUnlockedCursor)
                    {
                        gunSavedLock = Cursor.lockState;
                        gunSavedVisible = Cursor.visible;
                        gunUnlockedCursor = true;
                    }

                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;

                    CursorKeeper.Ensure();
                    CursorKeeper.Hold = true;
                }

                ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            }

            if (!data.isShooting)
            {
                GunCleanUp();
                return data;
            }

            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, MaxDistance, Main.NoInvisLayerMask());
            data.RaycastHit = hit;

            Vector3 hitPosition = hitSomething ? hit.point : ray.origin + ray.direction * MaxDistance;

            // ---------- ROPE MODE ----------
            if (mode == 1)
            {
                LockedPlayer = null;

                if (!data.isTriggered)
                    LockedRope = null;

                GorillaRopeSwing hoveredRope = null;
                Vector3 ropePoint = hitPosition;

                if (LockedRope == null)
                {
                    hoveredRope = FindRope(ray, hitSomething, hit, out Vector3 foundPoint);
                    if (hoveredRope != null)
                    {
                        ropePoint = foundPoint;
                        if (data.isTriggered)
                            LockedRope = hoveredRope;
                    }
                }
                else
                {
                    // keep the pointer glued to the locked rope
                    float closest = float.MaxValue;
                    foreach (Transform t in LockedRope.GetComponentsInChildren<Transform>())
                    {
                        float d = Vector3.Cross(ray.direction, t.position - ray.origin).magnitude;
                        if (d < closest)
                        {
                            closest = d;
                            ropePoint = t.position;
                        }
                    }
                }

                data.lockedPlayer = null;
                data.lockedRope = LockedRope;
                data.isLocked = LockedRope != null;
                data.hitPosition = ropePoint;

                UpdatePointer(ropePoint, data.isLocked);
                UpdateBeam(beamOrigin.position, ropePoint);
                SetGunActive(true);

                return data;
            }

            // ---------- PLAYER MODE ----------
            LockedRope = null;
            data.lockedRope = null;

            if (LockedPlayer != null && !IsValidTarget(LockedPlayer))
                LockedPlayer = null;

            if (!data.isTriggered)
                LockedPlayer = null;

            VRRig hovered = null;

            if (LockedPlayer == null)
            {
                hovered = FindTarget(ray, hitSomething, hit);

                if (data.isTriggered && hovered != null)
                    LockedPlayer = hovered;
            }

            if (LockedPlayer != null)
                hitPosition = GetTargetPoint(LockedPlayer);
            else if (hovered != null)
                hitPosition = GetTargetPoint(hovered);

            data.lockedPlayer = LockedPlayer;
            data.isLocked = LockedPlayer != null;
            data.hitPosition = hitPosition;

            UpdatePointer(hitPosition, data.isLocked);
            UpdateBeam(beamOrigin.position, hitPosition);
            SetGunActive(true);

            return data;
        }

        // ===== Visuals =====
        private static Shader FindShader(params string[] names)
        {
            foreach (string n in names)
            {
                Shader s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        private static GameObject MakePointer(string name)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = name;

            Collider collider = p.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = p.GetComponent<Renderer>();
            Shader shader = FindShader("GUI/Text Shader", "Sprites/Default");
            if (renderer != null && shader != null)
                renderer.material = new Material(shader);

            return p;
        }

        private static LineRenderer MakeLine(string name)
        {
            GameObject lineObject = new GameObject(name);

            LineRenderer lr = lineObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = BeamSegments + 1;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            Shader shader = FindShader("Sprites/Default", "GUI/Text Shader");
            if (shader != null)
                lr.material = new Material(shader);

            return lr;
        }

        private static void CreateVisuals()
        {
            if (pointer == null)
                pointer = MakePointer("GunLibPointer");

            if (line == null)
                line = MakeLine("GunLibBeam");
        }

        private static void ApplyGradient(LineRenderer lr)
        {
            Color startColor = backgroundColor.GetCurrentColor();
            Color endColor = backgroundColor.GetCurrentColor(0.5f);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) }
            );
            lr.colorGradient = gradient;
        }

        private static void UpdatePointer(Vector3 position, bool isLocked)
        {
            CreateVisuals();

            pointer.transform.position = position;
            pointer.transform.localScale = Vector3.one * sphereSize;

            Color pointerColor = isLocked ? buttonColors[1].GetCurrentColor() : buttonColors[0].GetCurrentColor();

            Renderer renderer = pointer.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
                renderer.material.color = pointerColor;
        }

        private static void UpdateBeam(Vector3 start, Vector3 end)
        {
            CreateVisuals();
            ApplyGradient(line);
            DrawStyle(line, beamStyle, start, end, true);
            line.enabled = true;
        }

        // Draws any style on any LineRenderer (used by your gun and by other players' guns)
        private static void DrawStyle(LineRenderer lr, int style, Vector3 start, Vector3 end, bool isLocal)
        {
            // default widths, styles below override them
            lr.startWidth = gunLineWidth;
            lr.endWidth = gunLineWidth * 0.5f;

            switch (style)
            {
                case 0: DrawStraight(lr, start, end); break;
                case 2: DrawTwirly(lr, start, end); break;
                case 3: DrawZigzag(lr, start, end); break;
                case 4:
                    if (isLocal) DrawMorphine(lr, start, end);
                    else DrawWavy(lr, start, end);
                    break;
                case 5:
                    lr.startWidth = gunLineWidth * 0.5f;
                    lr.endWidth = gunLineWidth * 3f;
                    DrawStraight(lr, start, end);
                    break;
                case 6: DrawTentacle(lr, start, end); break;
                default: DrawCurved(lr, start, end); break;
            }
        }

        private static void GetSideVectors(Vector3 direction, out Vector3 side, out Vector3 up)
        {
            Vector3 dir = direction.normalized;

            side = Vector3.Cross(dir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.001f)
                side = Vector3.Cross(dir, Vector3.right).normalized;

            up = Vector3.Cross(dir, side).normalized;
        }

        private static void DrawStraight(LineRenderer lr, Vector3 start, Vector3 end)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        private static void DrawCurved(LineRenderer lr, Vector3 start, Vector3 end)
        {
            lr.positionCount = BeamSegments + 1;

            wanderOffset = Vector3.Lerp(wanderOffset, targetWanderOffset, Time.deltaTime * WanderSpeed);

            if (Vector3.Distance(wanderOffset, targetWanderOffset) < 0.02f)
                targetWanderOffset = Random.insideUnitSphere * WanderRadius;

            Vector3 direction = end - start;
            GetSideVectors(direction, out Vector3 side, out Vector3 up);

            for (int i = 0; i <= BeamSegments; i++)
            {
                float t = (float)i / BeamSegments;

                Vector3 point = Vector3.Lerp(start, end, t);
                float curve = Mathf.Sin(t * Mathf.PI);

                point += side * (wanderOffset.x * curve);
                point += Vector3.up * (wanderOffset.y * curve);
                point += direction.normalized * (wanderOffset.z * curve);

                lr.SetPosition(i, point);
            }
        }

        private static void DrawTwirly(LineRenderer lr, Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            GetSideVectors(direction, out Vector3 side, out Vector3 up);

            float turns = Mathf.Clamp(distance * 1.2f, 4f, 30f);
            int segments = Mathf.Clamp(Mathf.RoundToInt(turns * 10f), 60, 400);

            lr.positionCount = segments + 1;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float envelope = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 6f);
                float angle = t * turns * Mathf.PI * 2f - Time.time * TwirlSpeed;

                Vector3 point = Vector3.Lerp(start, end, t);
                point += (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * (TwirlRadius * envelope);

                lr.SetPosition(i, point);
            }
        }

        private static void DrawZigzag(LineRenderer lr, Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            GetSideVectors(direction, out Vector3 side, out Vector3 up);

            int zigs = Mathf.Clamp(Mathf.RoundToInt(distance * 2f), 6, 40);
            int points = zigs * 2 + 1;

            lr.positionCount = points;

            for (int i = 0; i < points; i++)
            {
                float t = (float)i / (points - 1);
                float envelope = (i == 0 || i == points - 1) ? 0f : 1f;
                float sign = (i % 2 == 0) ? -1f : 1f;

                Vector3 point = Vector3.Lerp(start, end, t);
                point += side * (sign * ZigzagAmplitude * envelope);

                lr.SetPosition(i, point);
            }
        }

        // Floppy rope that lags behind your hand (local gun only)
        private static void DrawMorphine(LineRenderer lr, Vector3 start, Vector3 end)
        {
            lr.positionCount = RopePoints;

            if (!ropeReady)
            {
                for (int i = 0; i < RopePoints; i++)
                {
                    ropePos[i] = Vector3.Lerp(start, end, (float)i / (RopePoints - 1));
                    ropeVel[i] = Vector3.zero;
                }
                ropeReady = true;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.033f);

            ropePos[0] = start;
            ropePos[RopePoints - 1] = end;

            for (int i = 1; i < RopePoints - 1; i++)
            {
                float t = (float)i / (RopePoints - 1);
                Vector3 straight = Vector3.Lerp(start, end, t);

                Vector3 spring = (ropePos[i - 1] + ropePos[i + 1]) * 0.5f - ropePos[i];
                Vector3 pull = (straight - ropePos[i]) * 0.5f;

                float weight = Mathf.Sin(t * Mathf.PI);
                Vector3 drift = new Vector3(
                    Mathf.Sin(Time.time * 3f + i * 0.2f),
                    Mathf.Cos(Time.time * 2.5f + i * 0.15f),
                    Mathf.Sin(Time.time * 3.5f + i * 0.25f)) * (0.35f * weight);

                ropeVel[i] += (spring * 60f + pull * 20f + drift) * dt;
                ropeVel[i] *= 0.92f;
                ropePos[i] += ropeVel[i] * dt;
            }

            for (int i = 0; i < RopePoints; i++)
                lr.SetPosition(i, ropePos[i]);
        }

        // Simple wave version of Morphine (used for other players' guns)
        private static void DrawWavy(LineRenderer lr, Vector3 start, Vector3 end)
        {
            lr.positionCount = RopePoints;

            for (int i = 0; i < RopePoints; i++)
            {
                float t = (float)i / (RopePoints - 1);
                float weight = Mathf.Sin(t * Mathf.PI);

                Vector3 p = Vector3.Lerp(start, end, t);
                p += new Vector3(
                    Mathf.Sin(Time.time * 3f + i * 0.2f),
                    Mathf.Cos(Time.time * 2.5f + i * 0.15f),
                    Mathf.Sin(Time.time * 3.5f + i * 0.25f)) * (0.03f * weight);

                lr.SetPosition(i, p);
            }
        }

        private static void DrawTentacle(LineRenderer lr, Vector3 start, Vector3 end)
        {
            int points = 100;
            lr.positionCount = points;

            Vector3 direction = end - start;
            float length = direction.magnitude;
            GetSideVectors(direction, out Vector3 right, out Vector3 up);
            Vector3 forward = direction.normalized;

            lr.startWidth = gunLineWidth;
            lr.endWidth = gunLineWidth * 0.15f;

            for (int i = 0; i < points; i++)
            {
                float t = (float)i / (points - 1);
                Vector3 basePos = Vector3.Lerp(start, end, t);

                float sine = Mathf.Sin(t * Mathf.PI * 3f + Time.time * 3f);
                float cosine = Mathf.Cos(t * Mathf.PI * 2.5f + Time.time * 2.5f);
                float second = Mathf.Sin(t * Mathf.PI * 5f + Time.time * 4f) * 0.5f;
                float third = Mathf.Cos(t * Mathf.PI * 6f + Time.time * 3.5f) * 0.3f;

                Vector3 offset = up * (sine * 0.1f * (1 - t))
                               + right * (cosine * 0.08f * (1 - t))
                               + forward * (second * 0.06f * t);

                offset += up * (third * 0.04f * Mathf.Sin(t * Mathf.PI));
                offset += right * (Mathf.Sin(t * Mathf.PI) * 0.1f * length * 0.1f);
                offset += right * (Mathf.Sin(t * Mathf.PI * 8f + Time.time * 6f) * 0.02f);

                // pin both ends so it starts at your hand and ends on the pointer
                float envelope = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 8f);
                lr.SetPosition(i, basePos + offset * envelope);
            }
        }

        public static void GunCleanUp()
        {
            LockedPlayer = null;
            LockedRope = null;

            data.isShooting = false;
            data.isTriggered = false;
            data.isLocked = false;
            data.lockedPlayer = null;
            data.lockedRope = null;
            data.hitPosition = Vector3.zero;
            data.RaycastHit = default;

            if (pointer != null)
            {
                Object.Destroy(pointer);
                pointer = null;
            }

            if (line != null)
            {
                Object.Destroy(line.gameObject);
                line = null;
            }

            wanderOffset = Vector3.zero;
            targetWanderOffset = Vector3.zero;
            ropeReady = false;
            trigger = false;

            CursorKeeper.Hold = false;

            // give the cursor back to the game once we stop aiming
            if (gunUnlockedCursor && !ImGuiHost.Visible)
            {
                Cursor.lockState = gunSavedLock;
                Cursor.visible = gunSavedVisible;
            }
            gunUnlockedCursor = false;

            SetGunActive(false);
        }

        // =====================================================
        //  Gun sharing: shows gun lines between menu users
        //  (only cosmetic, uses Photon custom properties)
        // =====================================================
        public static bool shareMyGun = false;
        private static bool lastSyncedActive = false;

        private class RemoteGun
        {
            public GameObject pointer;
            public LineRenderer line;
        }

        private static readonly Dictionary<int, RemoteGun> remoteGuns = new Dictionary<int, RemoteGun>();

        private static void SetGunActive(bool active)
        {
            if (active == lastSyncedActive) return;
            lastSyncedActive = active;
            SyncGunProperties();
        }

        public static void SyncGunProperties()
        {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null) return;

            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props["Gun_Active"] = shareMyGun && lastSyncedActive;
            props["Gun_Style"] = beamStyle;
            props["Gun_Width"] = gunLineWidth;
            props["Gun_Sphere"] = sphereSize;
            props["Gun_Left"] = gunLeft;

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        // Call every frame (toggle button) to see other people's guns
        public static void UpdateNetworkGuns()
        {
            if (VRRigCache.ActiveRigs == null) return;

            HashSet<int> seen = new HashSet<int>();

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!IsValidTarget(rig) || rig.isOfflineVRRig) continue;

                Player player = null;
                try { player = rig.OwningNetPlayer.GetPlayerRef(); } catch { }
                if (player == null || player.IsLocal) continue;

                var props = player.CustomProperties;
                if (!props.ContainsKey("Gun_Active") || !(bool)props["Gun_Active"]) continue;

                int style = props.ContainsKey("Gun_Style") ? (int)props["Gun_Style"] : 1;
                float width = props.ContainsKey("Gun_Width") ? (float)props["Gun_Width"] : 0.025f;
                float sphere = props.ContainsKey("Gun_Sphere") ? (float)props["Gun_Sphere"] : 0.12f;
                bool left = props.ContainsKey("Gun_Left") && (bool)props["Gun_Left"];

                Transform hand = left ? rig.leftHandTransform : rig.rightHandTransform;
                if (hand == null) continue;

                Vector3 origin = hand.position;
                Vector3 end = Physics.Raycast(origin, hand.forward, out RaycastHit hit, MaxDistance, Main.NoInvisLayerMask())
                    ? hit.point
                    : origin + hand.forward * MaxDistance;

                int id = player.ActorNumber;
                seen.Add(id);

                if (!remoteGuns.TryGetValue(id, out RemoteGun gun) || gun.line == null || gun.pointer == null)
                {
                    gun = new RemoteGun { pointer = MakePointer("RemoteGunPointer"), line = MakeLine("RemoteGunBeam") };
                    remoteGuns[id] = gun;
                }

                gun.pointer.transform.position = end;
                gun.pointer.transform.localScale = Vector3.one * sphere;

                Renderer r = gun.pointer.GetComponent<Renderer>();
                if (r != null && r.material != null)
                    r.material.color = buttonColors[0].GetCurrentColor();

                float oldWidth = gunLineWidth;
                gunLineWidth = width;
                ApplyGradient(gun.line);
                DrawStyle(gun.line, style, origin, end, false);
                gunLineWidth = oldWidth;
            }

            // remove guns of players who stopped shooting or left
            List<int> remove = new List<int>();
            foreach (var kvp in remoteGuns)
                if (!seen.Contains(kvp.Key)) remove.Add(kvp.Key);

            foreach (int id in remove)
            {
                RemoteGun g = remoteGuns[id];
                if (g.pointer != null) Object.Destroy(g.pointer);
                if (g.line != null) Object.Destroy(g.line.gameObject);
                remoteGuns.Remove(id);
            }
        }

        public static void ClearNetworkGuns()
        {
            foreach (var kvp in remoteGuns)
            {
                if (kvp.Value.pointer != null) Object.Destroy(kvp.Value.pointer);
                if (kvp.Value.line != null) Object.Destroy(kvp.Value.line.gameObject);
            }
            remoteGuns.Clear();
        }

        // ===== DrawLine helper (looks like the gun beam, call every frame) =====
        private static Material drawLineMaterial;

        // Uses the gun's colors and the line style picked in the menu
        public static void DrawLine(Vector3 start, Vector3 end)
        {
            LineRenderer lr = CreateTempLine();
            if (lr == null) return;

            ApplyGradient(lr);
            DrawStyle(lr, beamStyle, start, end, false);
        }

        // Same gun look, but with your own color
        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            LineRenderer lr = CreateTempLine();
            if (lr == null) return;

            Color endColor = color;
            endColor.a *= 0.5f;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(endColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(endColor.a, 1f) }
            );
            lr.colorGradient = gradient;

            DrawStyle(lr, beamStyle, start, end, false);
        }

        private static LineRenderer CreateTempLine()
        {
            if (drawLineMaterial == null)
            {
                // same shader order the gun beam uses
                Shader shader = FindShader("Sprites/Default", "GUI/Text Shader");
                if (shader == null) return null;
                drawLineMaterial = new Material(shader);
            }

            GameObject lineObject = new GameObject("TempLine");

            LineRenderer lr = lineObject.AddComponent<LineRenderer>();
            lr.sharedMaterial = drawLineMaterial;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            Object.Destroy(lineObject, Time.deltaTime);
            return lr;
        }

    }
}