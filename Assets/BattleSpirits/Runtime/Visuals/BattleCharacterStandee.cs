using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LaoDai.BattleSpirits.Visuals
{
    // Thin physical cardstock based on a separately authored transparent
    // character cutout. This code reads alpha for geometry; it never edits the
    // source card or invents character artwork.
    internal sealed class StandeeTemplate
    {
        internal readonly string id;
        internal readonly Texture2D texture;
        internal readonly Mesh mesh;
        internal readonly float width, height;
        internal readonly int edgeSegments;
        internal StandeeTemplate(string id, Texture2D texture, Mesh mesh, float width, float height, int edgeSegments)
        { this.id = id; this.texture = texture; this.mesh = mesh; this.width = width; this.height = height; this.edgeSegments = edgeSegments; }
    }

    internal sealed class BattleStandeeLibrary : IDisposable
    {
        internal const float Thickness = .036f;
        internal const int MaximumMaskEdge = 256;
        private const byte AlphaThreshold = 32;
        private readonly Dictionary<string, List<StandeeTemplate>> templates = new Dictionary<string, List<StandeeTemplate>>(StringComparer.Ordinal);
        internal readonly Material Material;
        internal readonly Mesh baseMesh;
        private bool disposed;
        internal int MeshCount { get; private set; }

        internal BattleStandeeLibrary(Shader shader)
        {
            if (shader == null) return;
            Material = new Material(shader) { name = "BS_SharedPaperStandee" };
            Material.SetColor("_BaseColor", Color.white); Material.SetFloat("_Opacity", 1);
            Material.SetFloat("_Glow", 0); Material.SetFloat("_Cutoff", AlphaThreshold / 255f);
            baseMesh = BuildBase();
        }

        internal bool TryCreate(string cardId, Texture2D cutout, Transform parent, Color accent, out BattleCharacterStandee standee)
        {
            standee = null;
            if (disposed || Material == null || baseMesh == null || parent == null || cutout == null || string.IsNullOrEmpty(cardId)) return false;
            if (!templates.TryGetValue(cardId, out var versions)) versions = null;
            StandeeTemplate template = null;
            if (versions != null)
                foreach (var item in versions)
                    if (ReferenceEquals(item.texture, cutout)) { template = item; break; }
            if (template == null)
            {
                if (!TryBuild(cardId, cutout, out template)) return false;
                if (versions == null) { versions = new List<StandeeTemplate>(); templates.Add(cardId, versions); }
                // Keep older versions until field disposal. Another visible
                // copy may still borrow that mesh/texture during an async load.
                versions.Add(template); MeshCount++;
            }
            standee = new BattleCharacterStandee(parent, template, baseMesh, Material, accent);
            return true;
        }

        private static bool TryBuild(string cardId, Texture2D texture, out StandeeTemplate template)
        {
            template = null;
            int sourceWidth = texture.width, sourceHeight = texture.height;
            if (sourceWidth < 4 || sourceHeight < 4) return false;
            Color32[] pixels;
            try { pixels = texture.GetPixels32(); }
            catch (Exception) { return false; } // A non-readable texture is not a cutout mesh source.
            if (pixels == null || pixels.Length != sourceWidth * sourceHeight) return false;
            int minX = sourceWidth, minY = sourceHeight, maxX = -1, maxY = -1, visible = 0;
            for (int y = 0; y < sourceHeight; y++)
                for (int x = 0; x < sourceWidth; x++)
                    if (pixels[y * sourceWidth + x].a >= AlphaThreshold)
                    {
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); visible++;
                    }
            // Fully opaque card rectangles and empty images must not silently
            // appear as a rectangular standee with the frame/background intact.
            if (visible < 16 || visible >= pixels.Length * .99f) return false;
            int cropWidth = maxX - minX + 1, cropHeight = maxY - minY + 1;
            float factor = Math.Min(1f, MaximumMaskEdge / (float)Math.Max(cropWidth, cropHeight));
            int columns = Math.Max(1, (int)Math.Ceiling(cropWidth * factor));
            int rows = Math.Max(1, (int)Math.Ceiling(cropHeight * factor));
            var alpha = SampleAlpha(columns, rows);
            while (CountSegments(alpha, columns, rows) > 9000)
            {
                // Keep pathological noisy cutouts within the same geometry
                // budget. Only tracing density changes; face art stays native.
                columns = Math.Max(1, columns / 2); rows = Math.Max(1, rows / 2);
                alpha = SampleAlpha(columns, rows);
            }
            float[] SampleAlpha(int maskWidth, int maskHeight)
            {
                // A transparent sample ring closes silhouettes that touch a
                // source border. Samples lie at cell centres, not cell corners.
                var sampled = new float[(maskWidth + 2) * (maskHeight + 2)];
                for (int row = 1; row <= maskHeight; row++)
                    for (int column = 1; column <= maskWidth; column++)
                    {
                        float x = minX + (column - .5f) * cropWidth / maskWidth - .5f;
                        float y = minY + (row - .5f) * cropHeight / maskHeight - .5f;
                        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
                        float tx = x - x0, ty = y - y0;
                        float bottom = Read(x0, y0) * (1 - tx) + Read(x0 + 1, y0) * tx;
                        float top = Read(x0, y0 + 1) * (1 - tx) + Read(x0 + 1, y0 + 1) * tx;
                        sampled[row * (maskWidth + 2) + column] = bottom * (1 - ty) + top * ty;
                    }
                return sampled;
            }
            float Read(int x, int y) => x >= 0 && y >= 0 && x < sourceWidth && y < sourceHeight ? pixels[y * sourceWidth + x].a : 0;
            int CountSegments(float[] values, int maskWidth, int maskHeight)
            {
                int count = 0, stride = maskWidth + 2;
                for (int row = 0; row <= maskHeight; row++)
                    for (int column = 0; column <= maskWidth; column++)
                    {
                        bool a = values[row * stride + column] >= AlphaThreshold, b = values[row * stride + column + 1] >= AlphaThreshold;
                        bool c = values[(row + 1) * stride + column + 1] >= AlphaThreshold, d = values[(row + 1) * stride + column] >= AlphaThreshold;
                        int crossings = (a != b ? 1 : 0) + (b != c ? 1 : 0) + (c != d ? 1 : 0) + (d != a ? 1 : 0);
                        count += crossings / 2;
                    }
                return count;
            }
            float aspect = cropWidth / (float)cropHeight;
            float height = Math.Min(3.4f, 2.8f / aspect), width = height * aspect;
            float half = Thickness * .5f;
            var geometry = new StandeeMesh();
            var face = new Color(1, 1, 1, 1);
            var edge = new Color(.19f, .21f, .25f, 0);
            float u0 = minX / (float)sourceWidth, v0 = minY / (float)sourceHeight;
            float u1 = (maxX + 1) / (float)sourceWidth, v1 = (maxY + 1) / (float)sourceHeight;
            var uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            geometry.Quad(new Vector3(-width * .5f, 0, -half), new Vector3(width * .5f, 0, -half),
                new Vector3(width * .5f, height, -half), new Vector3(-width * .5f, height, -half), new Vector3(0, 0, -1), face, uv);
            // Same UVs on the reverse preserve the physical silhouette. From
            // behind the paper the illustration is naturally mirrored.
            geometry.Quad(new Vector3(-width * .5f, 0, half), new Vector3(width * .5f, 0, half),
                new Vector3(width * .5f, height, half), new Vector3(-width * .5f, height, half), new Vector3(0, 0, 1), face, uv);
            int edges = 0;
            var intersections = new Vector2[4];
            var present = new bool[4];
            int stride = columns + 2;
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column <= columns; column++)
                {
                    float a = alpha[row * stride + column], b = alpha[row * stride + column + 1];
                    float c = alpha[(row + 1) * stride + column + 1], d = alpha[(row + 1) * stride + column];
                    int count = 0;
                    Crossing(0, a, b, new Vector2(0, 0), new Vector2(1, 0));
                    Crossing(1, b, c, new Vector2(1, 0), new Vector2(1, 1));
                    Crossing(2, d, c, new Vector2(0, 1), new Vector2(1, 1));
                    Crossing(3, a, d, new Vector2(0, 0), new Vector2(0, 1));
                    if (count == 2)
                    {
                        int first = -1;
                        for (int i = 0; i < 4; i++) if (present[i]) { if (first < 0) first = i; else Segment(first, i); }
                    }
                    else if (count == 4)
                    {
                        // Resolve diagonal cells using the centre value, keeping
                        // holes as well as disconnected thin character parts.
                        bool centreSolid = (a + b + c + d) * .25f >= AlphaThreshold;
                        if ((a >= AlphaThreshold) == centreSolid) { Segment(0, 1); Segment(2, 3); }
                        else { Segment(0, 3); Segment(1, 2); }
                    }
                    void Crossing(int index, float from, float to, Vector2 p0, Vector2 p1)
                    {
                        present[index] = (from >= AlphaThreshold) != (to >= AlphaThreshold);
                        if (!present[index]) return;
                        float t = (AlphaThreshold - from) / (to - from);
                        intersections[index] = p0 + (p1 - p0) * t; count++;
                    }
                    void Segment(int first, int second)
                    {
                        var p0 = intersections[first]; var p1 = intersections[second];
                        float mx = (p0.x + p1.x) * .5f, my = (p0.y + p1.y) * .5f;
                        var outward = Normalize(new Vector3(-((b - a) * (1 - my) + (c - d) * my) * columns / width,
                            -((d - a) * (1 - mx) + (c - b) * mx) * rows / height, 0));
                        float x0 = -width * .5f + width * (column - .5f + p0.x) / columns;
                        float y0 = height * (row - .5f + p0.y) / rows;
                        float x1 = -width * .5f + width * (column - .5f + p1.x) / columns;
                        float y1 = height * (row - .5f + p1.y) / rows;
                        Wall(x0, y0, x1, y1, outward);
                    }
                }
            void Wall(float x0, float y0, float x1, float y1, Vector3 normal)
            {
                geometry.Quad(new Vector3(x0, y0, -half), new Vector3(x1, y1, -half),
                    new Vector3(x1, y1, half), new Vector3(x0, y0, half), normal, edge);
                edges++;
            }
            var mesh = geometry.Build("BS_CharacterStandee_" + cardId);
            template = new StandeeTemplate(cardId, texture, mesh, width, height, edges);
            return true;
        }

        private static Mesh BuildBase()
        {
            const int segments = 48;
            var geometry = new StandeeMesh();
            // A broad oval plinth with beveled top/bottom and a fine bright rim.
            Ring(1.25f, .70f, .025f, 1.30f, .74f, .055f, .62f);
            Ring(1.30f, .74f, .055f, 1.30f, .74f, .155f, .72f);
            Ring(1.30f, .74f, .155f, 1.245f, .695f, .195f, .90f);
            Ring(1.245f, .695f, .198f, 1.205f, .665f, .198f, 1.95f);
            var top = new Color(1, 1, 1, 0);
            for (int i = 0; i < segments; i++)
            {
                double a = i * Math.PI * 2 / segments, b = (i + 1) * Math.PI * 2 / segments;
                geometry.Triangle(new Vector3(0, .195f, 0), Point(1.205f, .665f, .195f, a), Point(1.205f, .665f, .195f, b), Vector3.up, top);
            }
            return geometry.Build("BS_SharedOvalStandeeBase");
            void Ring(float rx0, float rz0, float y0, float rx1, float rz1, float y1, float shade)
            {
                var color = new Color(shade, shade, shade, 0);
                bool horizontal = Math.Abs(y1 - y0) < .0001f;
                for (int i = 0; i < segments; i++)
                {
                    double a = i * Math.PI * 2 / segments, b = (i + 1) * Math.PI * 2 / segments;
                    var p0 = Point(rx0, rz0, y0, a); var p1 = Point(rx0, rz0, y0, b);
                    var p2 = Point(rx1, rz1, y1, b); var p3 = Point(rx1, rz1, y1, a);
                    var outward = new Vector3((float)Math.Cos((a + b) * .5), 0, (float)Math.Sin((a + b) * .5));
                    var along = p1 - p0; var rise = p3 - p0;
                    var normal = Normalize(new Vector3(along.y * rise.z - along.z * rise.y,
                        along.z * rise.x - along.x * rise.z, along.x * rise.y - along.y * rise.x));
                    if (normal.x * outward.x + normal.z * outward.z < 0) normal *= -1;
                    geometry.Quad(p0, p1, p2, p3, horizontal ? Vector3.up : normal, color);
                }
            }
            Vector3 Point(float rx, float rz, float y, double angle) => new Vector3(rx * (float)Math.Cos(angle), y, rz * (float)Math.Sin(angle));
        }

        private static Vector3 Normalize(Vector3 value)
        { double length = Math.Sqrt((double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z); return length > 1e-9 ? value * (float)(1 / length) : Vector3.up; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var versions in templates.Values) foreach (var template in versions) Object.Destroy(template.mesh);
            templates.Clear(); MeshCount = 0;
            if (baseMesh != null) Object.Destroy(baseMesh);
            if (Material != null) Object.Destroy(Material);
            // Session cache owns every borrowed cutout texture.
        }

        private sealed class StandeeMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            private int Vertex(Vector3 p, Vector3 n, Color c, Vector2 tex)
            { int index = vertices.Count; vertices.Add(p); normals.Add(n); colors.Add(c); uv.Add(tex); return index; }
            private static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            private static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color, Vector2[] faceUv = null)
            {
                int first = Vertex(a, normal, color, faceUv == null ? Vector2.zero : faceUv[0]);
                Vertex(b, normal, color, faceUv == null ? Vector2.zero : faceUv[1]); Vertex(c, normal, color, faceUv == null ? Vector2.zero : faceUv[2]); Vertex(d, normal, color, faceUv == null ? Vector2.zero : faceUv[3]);
                if (Dot(Cross(b - a, c - a), normal) >= 0)
                { triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3); }
                else { triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2); }
            }
            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
            {
                int first = Vertex(a, normal, color, Vector2.zero); Vertex(b, normal, color, Vector2.zero); Vertex(c, normal, color, Vector2.zero);
                triangles.Add(first);
                if (Dot(Cross(b - a, c - a), normal) >= 0) { triangles.Add(first + 1); triangles.Add(first + 2); }
                else { triangles.Add(first + 2); triangles.Add(first + 1); }
            }
            internal Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                // The sidewall-grid budget keeps even pathological masks below
                // UInt16's vertex limit; no platform-specific index format is needed.
                mesh.vertices = vertices.ToArray(); mesh.normals = normals.ToArray(); mesh.colors = colors.ToArray();
                mesh.uv = uv.ToArray(); mesh.triangles = triangles.ToArray(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }

    internal sealed class BattleCharacterStandee : IDisposable
    {
        internal readonly GameObject root;
        internal readonly StandeeTemplate template;
        internal readonly MeshRenderer paperRenderer, baseRenderer;
        internal readonly Transform paper;
        private readonly MaterialPropertyBlock paperProperties = new MaterialPropertyBlock(), baseProperties = new MaterialPropertyBlock();
        private bool disposed;
        private float previousOpacity = -1, previousGlow = -1;
        internal bool Visible
        {
            get => !disposed && paperRenderer.enabled && baseRenderer.enabled;
            set { if (!disposed) paperRenderer.enabled = baseRenderer.enabled = value; }
        }

        internal BattleCharacterStandee(Transform parent, StandeeTemplate template, Mesh baseMesh, Material material, Color accent)
        {
            this.template = template;
            root = new GameObject("CharacterStandee_" + template.id); root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            paper = BattleSpiritGeometry.Pivot(root.transform, "PaperCharacter", new Vector3(0, .20f, 0));
            paper.gameObject.AddComponent<MeshFilter>().sharedMesh = template.mesh;
            paperRenderer = paper.gameObject.AddComponent<MeshRenderer>(); paperRenderer.sharedMaterial = material;
            var plinth = BattleSpiritGeometry.Pivot(root.transform, "OvalCardBase", Vector3.zero);
            plinth.gameObject.AddComponent<MeshFilter>().sharedMesh = baseMesh;
            baseRenderer = plinth.gameObject.AddComponent<MeshRenderer>(); baseRenderer.sharedMaterial = material;
            paperRenderer.shadowCastingMode = baseRenderer.shadowCastingMode = ShadowCastingMode.Off;
            paperRenderer.receiveShadows = baseRenderer.receiveShadows = false;
            paperProperties.SetTexture("_MainTex", template.texture); paperProperties.SetColor("_BaseColor", Color.white);
            baseProperties.SetColor("_BaseColor", new Color(Math.Max(.06f, accent.r * .42f), Math.Max(.06f, accent.g * .42f), Math.Max(.06f, accent.b * .42f), 1));
            Animate(BattleVisualPose.Idle, 0, 0, 1, false);
        }

        internal void Animate(BattleVisualPose pose, float age, float clock, float facing, bool exhausted)
        {
            if (disposed) return;
            if (float.IsNaN(age) || float.IsInfinity(age)) age = 0;
            if (float.IsNaN(clock) || float.IsInfinity(clock)) clock = 0;
            clock %= 10000f;
            var sample = BattleVisualMotion.Sample(pose, age, clock);
            float duration = Math.Max(.001f, BattleVisualMotion.Duration(pose));
            float progress = Math.Max(0, Math.Min(1, age / duration));
            float opacity = pose == BattleVisualPose.Death ? 1 - progress : 1;
            root.transform.localPosition = new Vector3(0, .055f + Math.Max(0, sample.height * .12f),
                1.05f + Math.Max(-.22f, Math.Min(.60f, sample.advance * .20f)) * (facing < 0 ? -1 : 1));
            root.transform.localRotation = Quaternion.Euler(0, facing < 0 ? 7 : -7, 0);
            root.transform.localScale = Vector3.one * Math.Max(.01f, sample.scale);
            paper.localRotation = Quaternion.Euler(14 + sample.tilt * .30f + (exhausted ? 4 : 0),
                (float)Math.Sin(clock * 1.2) * 1.1f, (float)Math.Sin(clock * 1.7) * .65f);
            if (Math.Abs(previousOpacity - opacity) > .002f || Math.Abs(previousGlow - sample.glow) > .01f)
            {
                previousOpacity = opacity; previousGlow = sample.glow;
                paperProperties.SetFloat("_Opacity", opacity); paperProperties.SetFloat("_Glow", sample.glow * .30f);
                baseProperties.SetFloat("_Opacity", opacity); baseProperties.SetFloat("_Glow", sample.glow * .40f);
                paperRenderer.SetPropertyBlock(paperProperties); baseRenderer.SetPropertyBlock(baseProperties);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; root.SetActive(false); Object.Destroy(root);
            // Library owns geometry/material; session owns the source texture.
        }
    }
}
