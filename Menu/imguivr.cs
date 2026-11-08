using ImGuiNET;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using UnityEngine;
using UnityEngine.XR;

namespace StupidTemplate.ImGuiUI
{
    // VR hand menu.
    //  - Hold the menu button (VRFlags.HandHeld): the menu sits exactly where the old 3D menu sat (Main.RecenterMenu).
    //  - Search: the menu flies out of your hand and floats in front of your face with the keyboard next to it.
    //  - Click by touching it with the small sphere on your other hand.
    //  - Laser: one setting for the aim + trigger click AND the white line. Off = only the sphere clicks.
    // ImGuiHost renders into Target.
    public class ImGuiVR : MonoBehaviour
    {
        // Texture: 24px margin + 380 panel + 6 gap + 380 keyboard + 24px margin
        public const int TexW = 814, TexH = 568;
        private const int NormalW = 428;               // 24 + 380 + 24: the part shown when search is closed
        private const float PanelCenterPx = 214f;      // x of the panel's center in the texture (24 + 190)
        private const float PxToWorld = 0.42f / 520f;  // the panel is as tall as the old menu (0.42)

        // Setting (toggled by the "Laser" button): aiming, trigger click and the line, all together
        public static bool Laser = false;

        private const float SphereOffsetY = -0.1f;     // same spot the old menu's reference sphere used
        private const float SphereScale = 0.01f;
        private const float PressDepth = 0.012f;       // sphere closer than this to the menu (in front) = pressed
        private const float PressBehind = 0.04f;       // ...but pushed through further than this = not pressed
        private const float HoverDepth = 0.08f;        // sphere this close in front = hovering

        // Floating search (same numbers as Main)
        private const float FloatDistance = 0.5f;
        private const float FloatHeightOffset = -0.03f;
        private const float FloatTweenTime = 0.55f;
        private const float FloatMaxDistance = 3f;

        public static RenderTexture Target;
        public static bool Active;
        public static float FloatBlend;                // 0 = in your hand, 1 = floating

        private bool initialized;
        private Transform leftHand, rightHand;
        private Transform quad;
        private GameObject quadObj, sphere;
        private Mesh quadMesh;
        private LineRenderer laser;
        private bool lastDown;
        private bool wasSearching;
        private float lastFrac = -1f;
        private Vector3 floatCenter;
        private Quaternion floatRot = Quaternion.identity;

        public static bool IsVR()
        {
            return XRSettings.isDeviceActive;
        }

        private void TryInit()
        {
            if (!IsVR() || GorillaTagger.Instance == null)
                return;

            leftHand = GorillaTagger.Instance.leftHandTransform;
            rightHand = GorillaTagger.Instance.rightHandTransform;
            if (leftHand == null || rightHand == null)
                return;

            Target = new RenderTexture(TexW, TexH, 24, RenderTextureFormat.ARGB32);
            Target.Create();

            // The menu quad (not parented, its pose is set every frame)
            quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quadObj.GetComponent<Collider>());
            quad = quadObj.transform;
            quadMesh = quadObj.GetComponent<MeshFilter>().mesh;

            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader");
            Material m = new Material(sh);
            m.mainTexture = Target;
            quadObj.GetComponent<Renderer>().material = m;
            quadObj.SetActive(false);

            // The poke sphere (same place and size as Main.CreateReference)
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(Settings.rightHanded ? leftHand : rightHand, false);
            sphere.transform.localPosition = new Vector3(0f, SphereOffsetY, 0f);
            sphere.transform.localScale = new Vector3(SphereScale, SphereScale, SphereScale);
            sphere.GetComponent<Renderer>().material.color = Settings.backgroundColor.colors[0].color;
            ColorChanger changer = sphere.AddComponent<ColorChanger>();
            changer.colors = Settings.backgroundColor;
            sphere.SetActive(false);

            // Laser line (only shown when Laser is on)
            GameObject l = new GameObject("ImGuiLaser");
            laser = l.AddComponent<LineRenderer>();
            laser.material = new Material(Shader.Find("Sprites/Default"));
            laser.startWidth = laser.endWidth = 0.004f;
            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.enabled = false;

            Active = true;
            VRFlags.VRActive = true;
            initialized = true;
            Debug.Log("ImGuiVR: initialized");
        }

        private static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        // Remembers a spot in the air in front of your face (same idea as Main.CaptureFloatPose).
        private void CaptureFloatPose()
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;

            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = Vector3.forward;
            flat.Normalize();

            floatCenter = head.position + flat * FloatDistance + Vector3.up * FloatHeightOffset;
            floatRot = Quaternion.LookRotation(flat, Vector3.up);   // the quad's visible side faces you
        }

        // Updates the UVs so only the left part of the texture is shown when search is closed
        private void SetVisibleFraction(float frac)
        {
            if (Mathf.Approximately(frac, lastFrac))
                return;

            lastFrac = frac;
            Vector2[] uv = quadMesh.uv;
            // Unity's quad: 0 = bottom left, 1 = bottom right, 2 = top left, 3 = top right
            uv[0] = new Vector2(0f, 0f);
            uv[1] = new Vector2(frac, 0f);
            uv[2] = new Vector2(0f, 1f);
            uv[3] = new Vector2(frac, 1f);
            quadMesh.uv = uv;
        }

        void LateUpdate()
        {
            if (!initialized)
            {
                TryInit();
                return;
            }

            bool rh = Settings.rightHanded;
            Transform pointHand = rh ? leftHand : rightHand;
            XRNode pointNode = rh ? XRNode.LeftHand : XRNode.RightHand;

            if (sphere.transform.parent != pointHand)
                sphere.transform.SetParent(pointHand, false);

            bool drawing = ImGuiHost.DrawingVR;                 // hand menu is on screen (also while closing)
            bool interactive = drawing && ImGuiHost.Visible;    // and not closing
            bool searching = ImGuiMenu.SearchOpen;

            // ----- Floating search blend -----
            bool want = drawing && searching;

            if (want && !wasSearching && FloatBlend <= 0.001f)
                CaptureFloatPose();

            if (want && FloatBlend >= 1f &&
                Vector3.Distance(GorillaTagger.Instance.headCollider.transform.position, floatCenter) > FloatMaxDistance)
            {
                ImGuiMenu.CloseSearch();   // walked away from it, it comes back to your hand
                want = false;
            }

            wasSearching = want;
            FloatBlend = drawing ? Mathf.MoveTowards(FloatBlend, want ? 1f : 0f, Time.deltaTime / FloatTweenTime) : 0f;

            // ----- Pose: exactly where the old menu was (Main.RecenterMenu), panel center = hand + 0.05 along the menu's X -----
            Vector3 menuPos;
            Quaternion R;
            if (!rh)
            {
                menuPos = leftHand.position;
                R = leftHand.rotation;
            }
            else
            {
                menuPos = rightHand.position;
                R = Quaternion.Euler(rightHand.rotation.eulerAngles + new Vector3(0f, 0f, 180f));
            }

            Vector3 handPanelPos = menuPos + R * new Vector3(0.05f, 0f, 0f);
            Quaternion handQuadRot = R * Quaternion.LookRotation(Vector3.left, Vector3.forward);

            bool wide = searching || FloatBlend > 0.001f;       // keyboard is part of the picture
            float frac = wide ? 1f : NormalW / (float)TexW;
            float offsetWide = (TexW * 0.5f - PanelCenterPx) * PxToWorld;
            float offset = wide ? offsetWide : 0f;

            Vector3 panelPos = handPanelPos;
            Quaternion rot = handQuadRot;

            if (FloatBlend > 0f)
            {
                float t = SmootherStep(FloatBlend);
                Vector3 floatPanel = floatCenter - (floatRot * Vector3.right) * offsetWide;
                panelPos = Vector3.Lerp(handPanelPos, floatPanel, t);
                rot = Quaternion.Slerp(handQuadRot, floatRot, t);
            }

            SetVisibleFraction(frac);
            quad.rotation = rot;
            quad.position = panelPos + (rot * Vector3.right) * offset;
            quad.localScale = new Vector3(TexW * frac * PxToWorld, TexH * PxToWorld, 1f);

            quadObj.SetActive(drawing);
            sphere.SetActive(interactive);
            laser.enabled = interactive && Laser;

            ImGuiIOPtr io = ImGui.GetIO();

            if (!interactive)
            {
                if (lastDown)
                {
                    io.AddMouseButtonEvent(0, false);
                    lastDown = false;
                }
                return;
            }

            Vector2 mouse = new Vector2(-9999f, -9999f);
            bool down = false;
            bool pokeHover = false;

            // ----- Poke: touch the menu with the sphere -----
            Vector3 local = quad.InverseTransformPoint(sphere.transform.position);
            float u = local.x + 0.5f;
            float v = local.y + 0.5f;
            float front = -local.z;     // distance in front of the menu (the visible side is -Z)

            if (u >= 0f && u <= 1f && v >= 0f && v <= 1f && front > -PressBehind && front < HoverDepth)
            {
                pokeHover = true;
                mouse = new Vector2(u * TexW * frac, (1f - v) * TexH);
                down = front < PressDepth;
            }

            // ----- Aim + trigger (only when Laser is on and the sphere isn't over the menu) -----
            Ray ray = new Ray(pointHand.position, pointHand.forward);
            Vector3 end = ray.origin + ray.direction * 1.5f;

            if (Laser && !pokeHover)
            {
                Plane plane = new Plane(-quad.forward, quad.position);
                float dist;
                if (plane.Raycast(ray, out dist) && dist > 0f && dist < 2f)
                {
                    Vector3 lp = quad.InverseTransformPoint(ray.GetPoint(dist));
                    float lu = lp.x + 0.5f;
                    float lv = lp.y + 0.5f;

                    if (lu >= 0f && lu <= 1f && lv >= 0f && lv <= 1f)
                    {
                        mouse = new Vector2(lu * TexW * frac, (1f - lv) * TexH);
                        end = ray.GetPoint(dist);
                        down = ControllerInputPoller.TriggerFloat(pointNode) > 0.5f;
                    }
                }
            }

            if (laser.enabled)
            {
                laser.SetPosition(0, ray.origin);
                laser.SetPosition(1, end);
            }

            io.AddMousePosEvent(mouse.x, mouse.y);

            if (down != lastDown)
                io.AddMouseButtonEvent(0, down);
            lastDown = down;
        }
    }
}