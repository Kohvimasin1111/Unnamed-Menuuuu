using System.Reflection;
using HarmonyLib;
using UnityEngine;
using static StupidTemplate.Settings;

namespace Ghost
{
    public class Ghost : MonoBehaviour
    {
        public static Ghost Instance { get; private set; }
        public static bool Ghostview = true;

        public static void SetGhostview(bool state)
        {
            Ghostview = state;
        }

        // 0 = invisible, 1 = solid
        private const float SphereAlpha = 0.6f;

        // Sphere diameter in meters
        private const float SphereSize = 0.12f;

        // Kept so existing calls still work
        public static void Prepare()
        {
            if (Instance == null)
                new GameObject("GhostObject").AddComponent<Ghost>();
        }

        // ---------- Hook into TagSelf (so Gamemode.cs needs no changes) ----------
        private static bool patched;

        private static void PatchHooks()
        {
            if (patched)
                return;
            patched = true;

            try
            {
                Harmony harmony = new Harmony("ghost.hooks");

                MethodInfo tagSelf = typeof(StupidTemplate.Mods.Gamemode).GetMethod("TagSelf", BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(tagSelf, new HarmonyMethod(typeof(Ghost).GetMethod("TagSelfPrefix", BindingFlags.NonPublic | BindingFlags.Static)));

                Debug.Log("Ghost: hooks installed");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Ghost patch failed: " + e);
            }
        }

        private static void TagSelfPrefix()
        {
            Prepare();
        }

        private GameObject leftSphere;
        private GameObject rightSphere;
        private Material sphereMaterial;

        private void Awake()
        {
            Instance = this;
            PatchHooks();
        }

        private GameObject CreateSphere(string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.localScale = Vector3.one * SphereSize;

            // No collider so it can't interfere with grabbing or the menu
            Collider col = sphere.GetComponent<Collider>();
            if (col != null)
                DestroyImmediate(col);

            Renderer r = sphere.GetComponent<Renderer>();
            r.sharedMaterial = sphereMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            return sphere;
        }

        private void EnsureSpheres()
        {
            if (sphereMaterial == null)
            {
                Shader shader = Shader.Find("GUI/Text Shader");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                sphereMaterial = new Material(shader);
            }

            if (leftSphere == null)
                leftSphere = CreateSphere("GhostLeftHand");
            if (rightSphere == null)
                rightSphere = CreateSphere("GhostRightHand");
        }

        private void DestroySpheres()
        {
            if (leftSphere != null)
                Destroy(leftSphere);
            if (rightSphere != null)
                Destroy(rightSphere);
            leftSphere = null;
            rightSphere = null;
        }

        private void LateUpdate()
        {
            if (!Ghostview || GorillaTagger.Instance == null)
            {
                DestroySpheres();
                return;
            }

            VRRig rig = GorillaTagger.Instance.offlineVRRig;

            // Rig is on: your normal hands are visible, so hide the spheres
            if (rig.enabled)
            {
                if (leftSphere != null && leftSphere.activeSelf)
                    leftSphere.SetActive(false);
                if (rightSphere != null && rightSphere.activeSelf)
                    rightSphere.SetActive(false);
                return;
            }

            // Rig is disabled (tag self): show the spheres on your real hands
            EnsureSpheres();

            if (!leftSphere.activeSelf)
                leftSphere.SetActive(true);
            if (!rightSphere.activeSelf)
                rightSphere.SetActive(true);

            leftSphere.transform.position = GorillaTagger.Instance.leftHandTransform.position;
            rightSphere.transform.position = GorillaTagger.Instance.rightHandTransform.position;

            // Menu color, slightly transparent
            Color color = backgroundColor.GetCurrentColor();
            color.a = SphereAlpha;
            sphereMaterial.color = color;
        }

        private void OnDestroy()
        {
            DestroySpheres();
            if (sphereMaterial != null)
                Destroy(sphereMaterial);
        }
    }

    // Starts Ghost by itself so it exists before Tag Self runs
    [HarmonyPatch(typeof(GorillaLocomotion.GTPlayer), "LateUpdate")]
    public class GhostBootstrap
    {
        public static void Prefix()
        {
            if (Ghost.Instance == null && GorillaTagger.Instance != null)
                new GameObject("GhostObject").AddComponent<Ghost>();
        }
    }
}