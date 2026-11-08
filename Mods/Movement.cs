using GorillaLocomotion;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using UnityEngine;
using UnityEngine.XR;
using Unnamed_Free.Mods;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods
{
    public class Movement
    {
        public static void Fly()
        {
            if (ControllerInputPoller.instance.rightControllerPrimaryButton)
            {
                GTPlayer.Instance.transform.position += GorillaTagger.Instance.headCollider.transform.forward * Time.deltaTime * Settings.Movement.flySpeed;
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            }
        }

        public static GameObject platl;
        public static GameObject platr;

        // How far below your hand the platform spawns (bigger = lower)
        const float PlatformDrop = 0.08f;

        public static void Platforms()
        {
            // Left
            if (ControllerInputPoller.instance.leftGrab)
            {
                if (platl == null)
                {
                    platl = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    platl.transform.localScale = new Vector3(0.025f, 0.3f, 0.4f);
                    platl.transform.position = TrueLeftHand().position + Vector3.down * PlatformDrop;
                    platl.transform.rotation = TrueLeftHand().rotation;

                    FixStickyColliders(platl);

                    ColorChanger colorChanger = platl.AddComponent<ColorChanger>();
                    colorChanger.colors = StupidTemplate.Settings.backgroundColor;
                }
            }
            else if (platl != null)
            {
                UnityEngine.Object.Destroy(platl);
                platl = null;
            }

            // Right
            if (ControllerInputPoller.instance.rightGrab)
            {
                if (platr == null)
                {
                    platr = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    platr.transform.localScale = new Vector3(0.025f, 0.3f, 0.4f);
                    platr.transform.position = TrueRightHand().position + Vector3.down * PlatformDrop;
                    platr.transform.rotation = TrueRightHand().rotation;

                    FixStickyColliders(platr);

                    ColorChanger colorChanger = platr.AddComponent<ColorChanger>();
                    colorChanger.colors = StupidTemplate.Settings.backgroundColor;
                }
            }
            else if (platr != null)
            {
                UnityEngine.Object.Destroy(platr);
                platr = null;
            }
        }

        public static bool previousTeleportTrigger;
        public static void TeleportGun()
        {
            if (ControllerInputPoller.instance.rightGrab)
            {
                var GunData = RenderGun();
                GameObject NewPointer = GunData.NewPointer;

                if (ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f && !previousTeleportTrigger)
                {
                    GTPlayer.Instance.TeleportTo(NewPointer.transform.position + Vector3.up, GTPlayer.Instance.transform.rotation);
                    GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
                }

                previousTeleportTrigger = ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f;
            }
        }
        public static float flyPitch;

        public static void WASDFly()
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;

            // Mouse look: hold right mouse button
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                GTPlayer.Instance.Turn(delta.x * 0.15f);
                flyPitch = Mathf.Clamp(flyPitch - delta.y * 0.15f, -89f, 89f);
            }

            // Tilt the camera to match (yaw stays whatever it is)
            if (Camera.main != null)
                Camera.main.transform.rotation = Quaternion.Euler(flyPitch, head.eulerAngles.y, 0f);

            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 forward = Quaternion.AngleAxis(flyPitch, right) * flat;

            Vector3 direction = Vector3.zero;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.W)) direction += forward;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.S)) direction -= forward;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.D)) direction += right;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.A)) direction -= right;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.Space)) direction += Vector3.up;
            if (BepInEx.UnityInput.Current.GetKey(KeyCode.LeftControl)) direction -= Vector3.up;

            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

            if (direction != Vector3.zero)
                GTPlayer.Instance.transform.position += direction.normalized * Time.deltaTime * Settings.Movement.flySpeed;
        }

        static bool ghostMonkeyFrozen;
        static bool ghostMonkeyLastA;

        // Press A (right controller primary) once to freeze your rig, press A again to snap back into it.
        public static void GhostMonkey()
        {
            // Ghost setup: creates the component once and turns Ghostview on
            if (Ghost.Ghost.Instance == null)
                new GameObject("GhostObject").AddComponent<Ghost.Ghost>();
            Ghost.Ghost.SetGhostview(true);

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            bool a = ControllerInputPoller.instance.rightControllerPrimaryButton;

            // Only react on the frame A goes down, not while it's held
            if (a && !ghostMonkeyLastA)
            {
                ghostMonkeyFrozen = !ghostMonkeyFrozen;

                me.enabled = !ghostMonkeyFrozen;
                Master.Serialize();
            }

            ghostMonkeyLastA = a;
        }

        static bool invisMonkeyActive;
        static bool invisMonkeyLastA;

        // Press A (right controller primary) once to send your rig out of the map, press A again to come back.
        public static void InvisMonkey()
        {
            // Ghost setup: creates the component once and turns Ghostview on
            if (Ghost.Ghost.Instance == null)
                new GameObject("GhostObject").AddComponent<Ghost.Ghost>();
            Ghost.Ghost.SetGhostview(true);

            VRRig me = GorillaTagger.Instance.offlineVRRig;
            bool a = ControllerInputPoller.instance.rightControllerPrimaryButton;

            // Only react on the frame A goes down, not while it's held
            if (a && !invisMonkeyLastA)
            {
                invisMonkeyActive = !invisMonkeyActive;

                if (invisMonkeyActive)
                {
                    // Let the ghost save your pose while the rig is still on
                    Ghost.Ghost.Prepare();
                    me.enabled = false;
                }
                else
                {
                    me.enabled = true;
                    Master.Serialize();
                }
            }

            invisMonkeyLastA = a;

            // Keep the rig far outside the map while invisible
            if (invisMonkeyActive)
            {
                me.enabled = false;
                me.transform.position = new Vector3(99999f, 99999f, 99999f);
                Master.Serialize();
            }
        }

        // ===== Spider Web (paste inside Global) =====
        static GameObject[] webBall = new GameObject[2];
        static GameObject[] webGlow = new GameObject[2];
        static LineRenderer[] webLine = new LineRenderer[2];
        static Vector3[] webAnchor = new Vector3[2];
        static bool[] webHooked = new bool[2];
        static bool[] webLastTrigger = new bool[2];
        static float[] webRopeLen = new float[2];
        static float[] webShotTime = new float[2];
        static float[] webProgress = new float[2];

        const float WebMaxRange = 70f;
        const float WebReelSpeed = 7f;
        const float WebReelAccel = 22f;
        const float WebReleaseBoost = 1.12f;
        const float WebMinRope = 1.5f;
        const float WebShootTime = 0.10f;
        const int WebSegments = 28;
        const float WebWaveAmp = 0.35f;
        const float WebWaveFreq = 3f;
        const float WebWaveSpeed = 22f;
        const float WebWaveDecay = 3.5f;
        const float WebWidth = 0.014f;

        public static void SpiderWeb()
        {
            WebUpdate(0, true, GorillaTagger.Instance.leftHandTransform, StupidTemplate.Menu.Main.leftGrip, StupidTemplate.Menu.Main.leftTrigger);
            WebUpdate(1, false, GorillaTagger.Instance.rightHandTransform, StupidTemplate.Menu.Main.rightGrip, StupidTemplate.Menu.Main.rightTrigger);
        }

        static void WebUpdate(int i, bool left, Transform hand, bool grip, bool trigger)
        {
            bool pressed = trigger && !webLastTrigger[i];
            webLastTrigger[i] = trigger;

            if (webHooked[i] && !trigger)
            {
                webHooked[i] = false;
                GorillaTagger.Instance.rigidbody.linearVelocity *= WebReleaseBoost;
                GorillaTagger.Instance.StartVibration(left, 0.25f, 0.06f);
            }

            if (!webHooked[i] && !grip)
            {
                WebHide(i);
                return;
            }

            WebCreate(i);

            if (!webHooked[i])
            {
                webLine[i].enabled = false;

                RaycastHit hit;
                if (Physics.Raycast(hand.position, hand.forward, out hit, WebMaxRange, StupidTemplate.Menu.Main.NoInvisLayerMask()))
                {
                    float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 9f);
                    webBall[i].SetActive(true);
                    webGlow[i].SetActive(true);
                    webBall[i].transform.position = hit.point;
                    webGlow[i].transform.position = hit.point;
                    webBall[i].transform.localScale = Vector3.one * 0.16f * pulse;
                    webGlow[i].transform.localScale = Vector3.one * 0.34f * pulse;

                    if (pressed)
                    {
                        webHooked[i] = true;
                        webAnchor[i] = hit.point;
                        webProgress[i] = 0f;
                        webShotTime[i] = Time.time;
                        webRopeLen[i] = Vector3.Distance(GorillaTagger.Instance.headCollider.transform.position, hit.point);
                        GorillaTagger.Instance.StartVibration(left, 0.5f, 0.1f);
                    }
                }
                else
                {
                    webBall[i].SetActive(false);
                    webGlow[i].SetActive(false);
                }
                return;
            }

            webBall[i].transform.position = webAnchor[i];
            webGlow[i].transform.position = webAnchor[i];
            webBall[i].transform.localScale = Vector3.one * 0.2f;
            webGlow[i].transform.localScale = Vector3.one * 0.4f;

            webProgress[i] = Mathf.MoveTowards(webProgress[i], 1f, Time.deltaTime / WebShootTime);
            WebDraw(i, hand.position);

            if (webProgress[i] < 1f) return;

            Rigidbody rb = GorillaTagger.Instance.rigidbody;
            Vector3 toAnchor = webAnchor[i] - GorillaTagger.Instance.headCollider.transform.position;
            float dist = toAnchor.magnitude;
            Vector3 dir = toAnchor / Mathf.Max(dist, 0.001f);

            webRopeLen[i] = Mathf.MoveTowards(webRopeLen[i], WebMinRope, WebReelSpeed * Time.deltaTime);
            webRopeLen[i] = Mathf.Min(webRopeLen[i], dist);

            Vector3 vel = rb.linearVelocity;

            if (dist >= webRopeLen[i] - 0.05f)
            {
                float outward = Vector3.Dot(vel, -dir);
                if (outward > 0f)
                    vel += dir * outward;
            }

            vel += dir * WebReelAccel * Time.deltaTime;
            rb.linearVelocity = vel;
        }

        static void WebDraw(int i, Vector3 start)
        {
            Vector3 tip = Vector3.Lerp(start, webAnchor[i], webProgress[i]);
            Vector3 d = tip - start;
            float len = d.magnitude;
            Vector3 fwd = d / Mathf.Max(len, 0.001f);
            Vector3 side = Vector3.Cross(fwd, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, fwd).normalized;

            float age = Time.time - webShotTime[i];
            float amp = WebWaveAmp * Mathf.Exp(-age * WebWaveDecay) * Mathf.Min(1f, len / 10f);

            webLine[i].enabled = true;
            webLine[i].positionCount = WebSegments;
            webLine[i].startColor = Color.white;
            webLine[i].endColor = Color.white;

            for (int s = 0; s < WebSegments; s++)
            {
                float t = s / (float)(WebSegments - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float phase = t * Mathf.PI * 2f * WebWaveFreq - age * WebWaveSpeed;
                Vector3 offset = (side * Mathf.Sin(phase) + up * Mathf.Cos(phase)) * amp * envelope;
                webLine[i].SetPosition(s, Vector3.Lerp(start, tip, t) + offset);
            }
        }

        static void WebCreate(int i)
        {
            if (webBall[i] == null)
            {
                webBall[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(webBall[i].GetComponent<Collider>());
                Renderer r = webBall[i].GetComponent<Renderer>();
                r.material.shader = Shader.Find("GUI/Text Shader");
                r.material.color = Color.white;
            }

            if (webGlow[i] == null)
            {
                webGlow[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(webGlow[i].GetComponent<Collider>());
                Renderer r = webGlow[i].GetComponent<Renderer>();
                r.material.shader = Shader.Find("GUI/Text Shader");
                r.material.color = new Color(1f, 1f, 1f, 0.28f);
            }

            if (webLine[i] == null)
            {
                GameObject line = new GameObject("WebLine" + i);
                webLine[i] = line.AddComponent<LineRenderer>();
                webLine[i].material = new Material(Shader.Find("GUI/Text Shader"));
                webLine[i].startWidth = WebWidth;
                webLine[i].endWidth = WebWidth * 0.6f;
                webLine[i].useWorldSpace = true;
                webLine[i].numCapVertices = 4;
                webLine[i].numCornerVertices = 4;
                webLine[i].enabled = false;
            }
        }

        static void WebHide(int i)
        {
            if (webBall[i] != null) webBall[i].SetActive(false);
            if (webGlow[i] != null) webGlow[i].SetActive(false);
            if (webLine[i] != null) webLine[i].enabled = false;
        }

        public static void SpiderWebOff()
        {
            for (int i = 0; i < 2; i++)
            {
                webHooked[i] = false;
                webLastTrigger[i] = false;
                if (webBall[i] != null) UnityEngine.Object.Destroy(webBall[i]);
                if (webGlow[i] != null) UnityEngine.Object.Destroy(webGlow[i]);
                if (webLine[i] != null) UnityEngine.Object.Destroy(webLine[i].gameObject);
                webBall[i] = null;
                webGlow[i] = null;
                webLine[i] = null;
            }
        }


        private static Vector3 rigOffset;

        public static bool HoldRig()
        {
            VRRig rig = GorillaTagger.Instance.offlineVRRig;

            if (!rightGrip)
            {
                rig.enabled = true;
                return false;
            }

            if (rig.enabled)
            {
                rigOffset = rig.transform.position - GorillaTagger.Instance.bodyCollider.transform.position;
                rig.enabled = false;
            }

            rig.transform.position = GorillaTagger.Instance.bodyCollider.transform.position + rigOffset;
            rig.transform.rotation = Quaternion.Euler(0f, GorillaTagger.Instance.headCollider.transform.eulerAngles.y, 0f);
            rig.head.rigTarget.transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;
            return true;
        }
        // ---------- Speed Boost (4 levels, cycled with a button) ----------
        // Multiplies your normal jump values. 1f = no boost.
        private static readonly float[] speedLevels = { 1.1f, 1.2f, 1.35f, 1.5f };
        private static readonly string[] speedLevelNames = { "Mosa", "Fast", "Faster", "Ultra Fast" };
        public static int speedLevel = 0; // 0 = level 1

        private static float origMaxJumpSpeed;
        private static float origJumpMultiplier;

        // runs once when toggled ON
        public static void SpeedBoostOn()
        {
            if (GTPlayer.Instance == null) return;
            origMaxJumpSpeed = GTPlayer.Instance.maxJumpSpeed;
            origJumpMultiplier = GTPlayer.Instance.jumpMultiplier;
        }

        // runs every frame while ON
        public static void SpeedBoost()
        {
            if (GTPlayer.Instance == null) return;

            float mult = speedLevels[speedLevel];
            GTPlayer.Instance.maxJumpSpeed = origMaxJumpSpeed * mult;
            GTPlayer.Instance.jumpMultiplier = origJumpMultiplier * mult;
        }

        // runs once when toggled OFF
        public static void SpeedBoostOff()
        {
            if (GTPlayer.Instance == null) return;
            GTPlayer.Instance.maxJumpSpeed = origMaxJumpSpeed;
            GTPlayer.Instance.jumpMultiplier = origJumpMultiplier;
        }

        // the cycle button
        public static void CycleSpeedBoost()
        {
            speedLevel = (speedLevel + 1) % speedLevels.Length;

            ButtonInfo button = StupidTemplate.Menu.Main.GetIndex("Speed Boost Level");
            if (button != null)
                button.overlapText = "Speed Level: " + (speedLevel + 1) + " (" + speedLevelNames[speedLevel] + ")";
        }
    }
}