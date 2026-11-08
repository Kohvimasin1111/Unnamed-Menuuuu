using System.Collections.Generic;
using UnityEngine;

namespace StupidTemplate.Menu
{
    /// <summary>
    /// Swaps the mesh of a cube primitive for a rounded-corner box.
    /// The cube's collider and material stay untouched, so ColorChanger and the
    /// button triggers keep working exactly as before.
    /// The face you see is the local Y/Z plane (same as the template's buttons),
    /// and the box is extruded along local X.
    /// </summary>
    public static class RoundedMesh
    {
        private static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <param name="radius">Corner radius in WORLD units (menu buttons are only ~0.03 tall, so use ~0.01).</param>
        public static void Apply(GameObject obj, float radius, int segments = 8)
        {
            MeshFilter filter = obj.GetComponent<MeshFilter>();
            if (filter == null)
                return;

            // The object is non-uniformly scaled, so the corner has to be stretched
            // the opposite way in mesh space to end up circular in world space.
            Vector3 scale = obj.transform.lossyScale;
            float sizeY = Mathf.Abs(scale.y);
            float sizeZ = Mathf.Abs(scale.z);

            radius = Mathf.Min(radius, 0.5f * Mathf.Min(sizeY, sizeZ));

            // Meshes are cached because the menu is rebuilt on every click.
            string key = sizeY.ToString("F4") + "|" + sizeZ.ToString("F4") + "|" + radius.ToString("F4") + "|" + segments;
            if (!cache.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = Build(radius / sizeY, radius / sizeZ, segments);
                cache[key] = mesh;
            }

            filter.sharedMesh = mesh;
        }

        private static Mesh Build(float radiusY, float radiusZ, int segments)
        {
            // Outline of the face, counter-clockwise. Vector2.x = local Y, Vector2.y = local Z.
            Vector2[] corners =
            {
                new Vector2( 0.5f - radiusY,  0.5f - radiusZ),
                new Vector2(-0.5f + radiusY,  0.5f - radiusZ),
                new Vector2(-0.5f + radiusY, -0.5f + radiusZ),
                new Vector2( 0.5f - radiusY, -0.5f + radiusZ)
            };

            List<Vector2> outline = new List<Vector2>();
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (c * 90f + 90f * i / segments) * Mathf.Deg2Rad;
                    outline.Add(new Vector2(
                        corners[c].x + radiusY * Mathf.Cos(angle),
                        corners[c].y + radiusZ * Mathf.Sin(angle)));
                }
            }

            int count = outline.Count;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // Front (+X) and back (-X) faces
            foreach (float x in new[] { 0.5f, -0.5f })
            {
                Vector3 normal = new Vector3(Mathf.Sign(x), 0f, 0f);

                int center = vertices.Count;
                vertices.Add(new Vector3(x, 0f, 0f));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));

                int rim = vertices.Count;
                for (int i = 0; i < count; i++)
                {
                    vertices.Add(new Vector3(x, outline[i].x, outline[i].y));
                    normals.Add(normal);
                    uvs.Add(outline[i] + new Vector2(0.5f, 0.5f));
                }

                for (int i = 0; i < count; i++)
                    AddTriangle(vertices, triangles, center, rim + i, rim + (i + 1) % count, normal);
            }

            // Edge walls
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % count];
                Vector2 edge = b - a;
                Vector3 normal = new Vector3(0f, edge.y, -edge.x).normalized;

                int start = vertices.Count;
                vertices.Add(new Vector3(0.5f, a.x, a.y));
                vertices.Add(new Vector3(0.5f, b.x, b.y));
                vertices.Add(new Vector3(-0.5f, b.x, b.y));
                vertices.Add(new Vector3(-0.5f, a.x, a.y));
                for (int k = 0; k < 4; k++)
                {
                    normals.Add(normal);
                    uvs.Add(Vector2.zero);
                }

                AddTriangle(vertices, triangles, start, start + 1, start + 2, normal);
                AddTriangle(vertices, triangles, start, start + 2, start + 3, normal);
            }

            Mesh mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Adds a triangle and flips its winding if needed so it faces 'facing'.
        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 facing)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(cross, facing) < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}