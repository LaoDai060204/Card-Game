using System.Collections.Generic;
using UnityEngine;

namespace MDPro3.BattleSpirits.Visuals
{
    internal static class BattleGeometry
    {
        internal static Mesh Quad()
        {
            var mesh = new Mesh { name = "Battle Spirits shared paper plane" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Mesh Frame(float width, float depth, float thickness, float radius)
        {
            const int steps = 10;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var corner = 0; corner < 4; corner++)
            {
                var angle = corner * Mathf.PI * .5f;
                var cx = (corner == 0 || corner == 3 ? 1 : -1) * (width * .5f - radius);
                var cz = (corner < 2 ? 1 : -1) * (depth * .5f - radius);
                for (var i = 0; i <= steps; i++)
                {
                    var a = angle + i * Mathf.PI * .5f / steps;
                    var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    vertices.Add(new Vector3(cx, 0, cz) + direction * radius);
                    vertices.Add(new Vector3(cx, 0, cz) + direction * (radius - thickness));
                }
            }
            var pairs = vertices.Count / 2;
            for (var i = 0; i < pairs; i++)
            {
                var a = i * 2;
                var b = ((i + 1) % pairs) * 2;
                triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            var mesh = new Mesh { name = "Battle Spirits silver rounded frame" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Mesh Motes()
        {
            const int count = 24;
            var vertices = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var triangles = new int[count * 6];
            var random = new System.Random(20261115);
            for (var i = 0; i < count; i++)
            {
                var seed = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                for (var j = 0; j < 4; j++)
                {
                    var p = new Vector2(j == 0 || j == 3 ? 0 : 1, j < 2 ? 0 : 1);
                    vertices[i * 4 + j] = new Vector3((p.x - .5f) * .065f, (p.y - .5f) * .065f, 0);
                    uv[i * 4 + j] = p;
                    colors[i * 4 + j] = seed;
                }
                var v = i * 4;
                var t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Battle Spirits fixed 24 mote pool" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0, 1.5f, 0), new Vector3(5, 6, 5));
            return mesh;
        }
    }
}
