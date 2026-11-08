using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace Unnamed_Free.Classes.Assets.Modules
{
    public class ModuleAssets
    {
        public static GameObject Robux;

        private static bool loadTried;
        private static float lastRainTime = -999f;
        private const float cooldown = 1f;
        private const float targetSize = 0.4f; // longest side of the model, in meters
        private const string LastRainKey = "ModuleAssets_LastRainDate";

        private static Texture2D billTex;

        // Bill model (exported from Blender), embedded so no file is needed
        private const string RobuxObj = @"
v -0.800000 -0.060000 0.450000
v -0.800000 0.060000 0.450000
v -0.800000 -0.060000 -0.450000
v -0.800000 0.060000 -0.450000
v 0.800000 -0.060000 0.450000
v 0.800000 0.060000 0.450000
v 0.800000 -0.060000 -0.450000
v 0.800000 0.060000 -0.450000
vn 0 1 0
vn 0 -1 0
vn 0 0 1
vn 1 0 0
vn 0 0 -1
vn -1 0 0
vt 0 0
vt 1 0
vt 1 1
vt 0 1
vt 0.015625 0.027778
f 2/1/1 6/2/1 8/3/1 4/4/1
f 1/2/2 3/3/2 7/4/2 5/1/2
f 1/5/3 5/5/3 6/5/3 2/5/3
f 5/5/4 7/5/4 8/5/4 6/5/4
f 7/5/5 3/5/5 4/5/5 8/5/5
f 3/5/6 1/5/6 2/5/6 4/5/6
";

        // Bill texture (32x18 PNG), embedded as base64
        private const string RobuxPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACAAAAASCAIAAAC1qksFAAAAAXNSR0IDN8dNUwAAAARnQU1BAACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAAADZlWElmTU0AKgAAABgAAABIAAAAAQAAAEgAAAABAAIBGgAFAAAAAQAAAAgBGwAFAAAAAQAAABAAAAAAkHjvvAAAAAlvRkZzAAAAAAAAAAAA2iq2zgAAAAlwSFlzAAALEgAACxIB0t1+/AAAAktJREFUOBG1lD1IHVEQhf1JE4kIYlAsIq+JiUgIWFkIspWNSEBQiIVYmFrFKooE0gna+VcE0ym8RrEwzTaC2hhsTEwICK8ICEYQEdt8y1kO42Kpy3I5M3PmzMzee7e6aaS56jGfJ4i3Js1/03MDmV5VHQIed2K+PRGYDKiOE0x8HoT3qvH16eXPmCAs/2a6Nzc4jgcOnk/ltaGkp5C1OFN2elYgTqCAcgppzhEo9CHR1aVZCgtLtkZsAti0hrQaxE+zNGg5wNr6Di8Adfm1wkQBjDqYLyETTz6BWibm3olFU7qS07owOQ2QYswyh3T2IJ9A3sijR5tSb3jRsDH6hRcAf3JhHnUANLTgs0rHADMvQLgwtZJhiC1R5bMWTGrAd0MA1HTSauvePLs+u/l++6ulpc75BvC2d1PMr++W+0t98gN4y8dbR8e/u96+NDmCi9uLStU/lLMJ2F7V1BCsGpmQ54jJ9+L4WSLOLhoPolFLI8s/Ppodng/pBOZqssgqDIgfypcDP9hqdzaZGI9jqqp2ripXimqVSR+KwvSBhhBxNgG7cZrcc3VVCRU4DIHo8PqYCrDSPrpDSeaACc232hxAtsnswclZpbPUpgD38DD90Z10YNIgfoTa2hvfJwPfDvZxcgMwSSmcCysANBmbnJ8ikEUB+imhpRyOBJiVM/Pn8ry3o6vp6XPUKawQUT84P06tIFhfys5nfooUJgZAXfWdE4G+GF8DTtwt5cK0U1n5rwJD9wLAF7MpLKoJ5sgfyXCcIv6d37UTHhD8B+aaVd3UG6tIAAAAAElFTkSuQmCC";

        public static void LoadAssets()
        {
            if (Robux != null || loadTried) return;
            loadTried = true;

            try
            {
                billTex = LoadTexture();

                Mesh mesh = ParseObj(RobuxObj);
                if (mesh != null)
                {
                    Robux = BuildModel(mesh);
                    Debug.Log("Robux OBJ loaded (" + mesh.vertexCount + " verts, texture: " + (billTex != null) + ")");
                }
                else Debug.LogError("Robux OBJ parsed but had no faces");
            }
            catch (Exception e)
            {
                Debug.LogError("Robux load error: " + e);
            }

            if (Robux == null)
            {
                Debug.Log("Using generated coin fallback");
                Robux = CreateFallbackCoin();
            }
        }

        private static Texture2D LoadTexture()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(t, Convert.FromBase64String(RobuxPngBase64)))
                return null;
            t.filterMode = FilterMode.Point;   // keeps the pixels crisp
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        private static Mesh ParseObj(string text)
        {
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uvs = new List<Vector2>();

            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outU = new List<Vector2>();
            var tris = new List<int>();
            bool hasN = false, hasU = false;

            var ci = CultureInfo.InvariantCulture;

            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length < 2 || line[0] == '#') continue;
                string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                switch (p[0])
                {
                    case "v":
                        pos.Add(new Vector3(-float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci)));
                        break;
                    case "vn":
                        nrm.Add(new Vector3(-float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci)));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(float.Parse(p[1], ci), float.Parse(p[2], ci)));
                        break;
                    case "f":
                        var corners = new List<int>();
                        for (int i = 1; i < p.Length; i++)
                        {
                            string[] idx = p[i].Split('/');
                            int vi = Resolve(int.Parse(idx[0], ci), pos.Count);
                            outV.Add(pos[vi]);

                            if (idx.Length > 1 && idx[1].Length > 0)
                            {
                                outU.Add(uvs[Resolve(int.Parse(idx[1], ci), uvs.Count)]);
                                hasU = true;
                            }
                            else outU.Add(Vector2.zero);

                            if (idx.Length > 2 && idx[2].Length > 0)
                            {
                                outN.Add(nrm[Resolve(int.Parse(idx[2], ci), nrm.Count)]);
                                hasN = true;
                            }
                            else outN.Add(Vector3.up);

                            corners.Add(outV.Count - 1);
                        }
                        // Triangulate as a fan; reverse winding because X is mirrored
                        for (int i = 1; i < corners.Count - 1; i++)
                        {
                            tris.Add(corners[0]);
                            tris.Add(corners[i + 1]);
                            tris.Add(corners[i]);
                        }
                        break;
                }
            }

            if (outV.Count == 0 || tris.Count == 0) return null;

            var mesh = new Mesh();
            mesh.name = "RobuxMesh";
            if (outV.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(outV);
            mesh.SetTriangles(tris, 0);
            if (hasU) mesh.SetUVs(0, outU);
            if (hasN) mesh.SetNormals(outN); else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // OBJ indices are 1-based, and negative means relative to the end
        private static int Resolve(int i, int count)
        {
            return i > 0 ? i - 1 : count + i;
        }

        private static GameObject BuildModel(Mesh mesh)
        {
            var go = new GameObject("RobuxModel");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().material = MakeGoldMaterial();

            // Auto-scale so the longest side equals targetSize
            Vector3 size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest > 0.0001f)
                go.transform.localScale = Vector3.one * (targetSize / longest);

            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            return go;
        }

        private static Material MakeGoldMaterial()
        {
            Shader shader = Shader.Find("Unlit/Texture")
                         ?? Shader.Find("GorillaTag/UberShader")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");

            // Last resort: borrow the shader from a primitive so we never pass null
            if (shader == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shader = temp.GetComponent<Renderer>().sharedMaterial.shader;
                Object.Destroy(temp);
            }

            var mat = new Material(shader);
            Color c = billTex != null ? Color.white : new Color(1f, 0.8f, 0.1f);

            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (billTex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", billTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", billTex);
            }
            return mat;
        }

        private static GameObject CreateFallbackCoin()
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "RobuxFallback";
            coin.transform.localScale = new Vector3(0.4f, 0.03f, 0.4f);
            coin.GetComponent<Renderer>().material = MakeGoldMaterial();
            coin.SetActive(false);
            Object.DontDestroyOnLoad(coin);
            return coin;
        }

        public static void RainRobuxDaily()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(LastRainKey, "") == today) return;

            if (RainRobux())
            {
                PlayerPrefs.SetString(LastRainKey, today);
                PlayerPrefs.Save();
            }
        }

        public static void ResetDaily()
        {
            PlayerPrefs.DeleteKey(LastRainKey);
            PlayerPrefs.Save();
        }

        public static bool RainRobux()
        {
            if (Time.time - lastRainTime < cooldown) return false;
            lastRainTime = Time.time;

            if (Robux == null) LoadAssets();
            if (Robux == null) return false;

            const int count = 100;
            const float radius = 15f;
            const float minHeight = 3f;
            const float maxHeight = 8f;
            const float lifetime = 2f;   // seconds until they disappear

            Vector3 center = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            int spawned = 0;

            for (int attempt = 0; attempt < count * 3 && spawned < count; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(2f, radius);
                Vector3 top = new Vector3(center.x + offset.x, center.y + 5f, center.z + offset.y);

                // Only spawn where there is actually ground underneath
                if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 pos = hit.point + Vector3.up * Random.Range(minHeight, maxHeight);
                GameObject robux = Object.Instantiate(Robux, pos, Random.rotation);
                robux.SetActive(true);

                if (robux.GetComponentInChildren<Collider>() == null)
                {
                    var box = robux.AddComponent<BoxCollider>();
                    var mf = robux.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = mf.sharedMesh.bounds.size;
                    }
                }

                var rb = robux.GetComponent<Rigidbody>();
                if (rb == null) rb = robux.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = 0.1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var coin = robux.AddComponent<RobuxCoin>();
                coin.groundY = hit.point.y;
                coin.life = lifetime;

                spawned++;
            }

            return spawned > 0;
        }
    }

    public class RobuxCoin : MonoBehaviour
    {
        public float groundY;
        public float life = 2f;

        private Rigidbody rb;
        private float age, settleTimer;
        private Vector3 baseScale;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            baseScale = transform.localScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }

            // Shrink away over the last 0.3 seconds
            const float shrinkTime = 0.3f;
            if (age > life - shrinkTime)
                transform.localScale = baseScale * Mathf.Clamp01((life - age) / shrinkTime);
        }

        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;

            // Fell through the map: put it back on the ground and freeze it
            if (transform.position.y < groundY - 0.3f)
            {
                transform.position = new Vector3(transform.position.x, groundY + 0.05f, transform.position.z);
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                return;
            }

            // Landed and stopped moving: freeze so it stays on the ground
            if (rb.velocity.sqrMagnitude < 0.05f)
            {
                settleTimer += Time.fixedDeltaTime;
                if (settleTimer > 0.5f) rb.isKinematic = true;
            }
            else settleTimer = 0f;
        }
    }
}