using System.Collections.Generic;
using UnityEngine;

namespace WavyBoard.Rider
{
    /// <summary>Procedural placeholder bodyboard (rounded nose, crescent tail, thick rails). Replaced by the Blender model via BoardSpec.boardModel.</summary>
    public static class BodyboardMeshBuilder
    {
        public static Mesh Build(float length = 1.05f, float width = 0.55f, float thickness = 0.055f, int segments = 28)
        {
            var outline = new List<Vector2>();
            float hl = length * 0.5f, hw = width * 0.5f;
            // right side from tail to nose, then left side from nose to tail
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;             // 0 tail .. 1 nose
                float z = Mathf.Lerp(-hl, hl, t);
                outline.Add(new Vector2(HalfWidth(z, hl, hw), z));
            }
            for (int i = segments; i >= 0; i--)
            {
                float t = (float)i / segments;
                float z = Mathf.Lerp(-hl, hl, t);
                outline.Add(new Vector2(-HalfWidth(z, hl, hw), z));
            }
            // crescent tail: pull the tail center up
            int n = outline.Count;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // three rings: top, rail mid, bottom -> rounded rails
            float ht = thickness * 0.5f;
            int ringTop = 0, ringMid = n, ringBot = 2 * n;
            for (int r = 0; r < 3; r++)
            {
                float y = r == 0 ? ht : (r == 1 ? 0f : -ht);
                float inset = r == 1 ? 1.0f : 0.94f;
                for (int i = 0; i < n; i++)
                {
                    var o = outline[i];
                    float z = o.y;
                    float crescent = z < -hl * 0.75f ? (1f - Mathf.Abs(o.x) / Mathf.Max(0.01f, HalfWidth(z, hl, hw))) * (-(z + hl * 0.75f) / (hl * 0.25f)) * 0.06f : 0f;
                    verts.Add(new Vector3(o.x * inset, y, z + crescent));
                    norms.Add(Vector3.zero);
                    uvs.Add(new Vector2(o.x / width + 0.5f, z / length + 0.5f));
                }
            }
            // rail quads top->mid and mid->bot
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                AddQuad(tris, ringTop + i, ringTop + j, ringMid + j, ringMid + i);
                AddQuad(tris, ringMid + i, ringMid + j, ringBot + j, ringBot + i);
            }
            // top cap (fan from center) and bottom cap
            int topCenter = verts.Count; verts.Add(new Vector3(0f, ht, 0f)); norms.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
            int botCenter = verts.Count; verts.Add(new Vector3(0f, -ht, 0f)); norms.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(topCenter); tris.Add(ringTop + j); tris.Add(ringTop + i);
                tris.Add(botCenter); tris.Add(ringBot + i); tris.Add(ringBot + j);
            }
            var mesh = new Mesh { name = "Bodyboard" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float HalfWidth(float z, float hl, float hw)
        {
            float t = Mathf.Abs(z) / hl;
            float n = z >= 0f ? 2.6f : 3.4f; // rounder nose, squarer tail
            return hw * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(t, n)), 1f / n);
        }

        static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(a); tris.Add(d); tris.Add(c);
        }
    }
}
