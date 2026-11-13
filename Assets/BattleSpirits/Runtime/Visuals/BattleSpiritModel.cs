using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LaoDai.BattleSpirits.Visuals
{
    // Original, artwork-guided fan interpretations. These are real colored
    // skinned meshes, not official/extracted models or textured card planes.
    internal sealed class SpiritModelTemplate
    {
        internal readonly string id, profileName;
        internal readonly Mesh mesh;
        internal readonly Vector3[] joints;
        internal SpiritModelTemplate(string id, string profileName, Mesh mesh, Vector3[] joints)
        { this.id = id; this.profileName = profileName; this.mesh = mesh; this.joints = joints; }
    }

    internal sealed class BattleSpiritModelLibrary : IDisposable
    {
        private readonly Dictionary<string, SpiritModelTemplate> templates = new Dictionary<string, SpiritModelTemplate>(StringComparer.Ordinal);
        private bool disposed;
        internal readonly Material Material;
        internal int MeshCount => templates.Count;
        internal static IReadOnlyList<string> SupportedIds => BattleSpiritModelProfiles.Ids;

        internal BattleSpiritModelLibrary(Shader shader)
        {
            if (shader == null) return;
            Material = new Material(shader) { name = "BS_SharedSpiritVertexColor" };
            Material.SetColor("_BaseColor", Color.white);
            Material.SetFloat("_Opacity", 1); Material.SetFloat("_Glow", 0);
        }

        internal bool TryCreate(string cardId, Transform parent, out BattleSpiritModel model)
        {
            model = null;
            if (disposed || Material == null || parent == null || string.IsNullOrEmpty(cardId)) return false;
            if (!templates.TryGetValue(cardId, out var template))
            {
                if (!BattleSpiritModelProfiles.TryBuild(cardId, out template)) return false;
                templates.Add(cardId, template);
            }
            model = new BattleSpiritModel(parent, template, Material); return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var template in templates.Values) Object.Destroy(template.mesh);
            templates.Clear();
            if (Material != null) Object.Destroy(Material);
        }
    }

    internal sealed class BattleSpiritModel : IDisposable
    {
        internal const int JointCount = 10;
        private static readonly string[] JointNames = { "Body", "Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg", "Tail", "LeftWing", "RightWing", "Accessory" };
        internal readonly GameObject root;
        internal readonly SkinnedMeshRenderer Renderer;
        internal readonly Transform[] bones;
        internal readonly SpiritModelTemplate template;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private bool disposed;
        private float previousOpacity = -1, previousGlow = -1;

        internal BattleSpiritModel(Transform parent, SpiritModelTemplate template, Material material)
        {
            this.template = template;
            root = new GameObject("SpiritModel_" + template.id); root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            bones = new Transform[JointCount];
            for (int index = 0; index < bones.Length; index++)
            {
                var joint = new GameObject("Rig_" + JointNames[index]); joint.layer = root.layer;
                joint.transform.SetParent(root.transform, false); joint.transform.localPosition = template.joints[index];
                bones[index] = joint.transform;
            }
            if (template.mesh.bindposes == null || template.mesh.bindposes.Length != bones.Length)
            {
                var bind = new Matrix4x4[bones.Length];
                for (int index = 0; index < bind.Length; index++)
                    bind[index] = bones[index].worldToLocalMatrix * root.transform.localToWorldMatrix;
                template.mesh.bindposes = bind;
            }
            Renderer = root.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = template.mesh; Renderer.sharedMaterial = material;
            Renderer.bones = bones; Renderer.rootBone = root.transform;
            Renderer.quality = SkinQuality.Bone1;
            Renderer.updateWhenOffscreen = false;
            Renderer.shadowCastingMode = ShadowCastingMode.Off; Renderer.receiveShadows = false;
            // Bone rotations can extend beyond the rest pose. A fixed generous
            // local bound prevents clipping without recalculating it each frame.
            var bounds = template.mesh.bounds;
            Renderer.localBounds = new Bounds(bounds.center, bounds.size + new Vector3(2, 2, 2));
            Animate(BattleVisualPose.Idle, 0, 0, 1, false);
        }

        internal void Animate(BattleVisualPose pose, float age, float clock, float facing, bool exhausted)
        {
            if (disposed) return;
            if (float.IsNaN(age) || float.IsInfinity(age)) age = 0;
            if (float.IsNaN(clock) || float.IsInfinity(clock)) clock = 0;
            clock %= 10000f;
            var sample = BattleVisualMotion.Sample(pose, age, clock);
            float wave = (float)Math.Sin((double)clock * 1.7), slow = (float)Math.Sin((double)clock * 1.1);
            float duration = Math.Max(.001f, BattleVisualMotion.Duration(pose));
            float progress = Math.Max(0, Math.Min(1, age / duration));
            float opacity = pose == BattleVisualPose.Death ? 1 - progress : 1;
            root.transform.localPosition = new Vector3(0, .10f + Math.Max(-.035f, sample.height * .16f),
                1.12f + Math.Max(-.35f, Math.Min(.85f, sample.advance * .28f)) * (facing < 0 ? -1 : 1));
            root.transform.localRotation = Quaternion.Euler(sample.tilt * .34f + (exhausted ? 7 : 0), facing < 0 ? 10 : -10, 0);
            root.transform.localScale = Vector3.one * Math.Max(.01f, sample.scale);
            bones[0].localRotation = Quaternion.identity;
            bones[1].localRotation = Quaternion.Euler(wave * 2 - sample.glow * 5, slow * 3, 0);
            bones[2].localRotation = Quaternion.Euler(-sample.advance * 7, 0, -sample.wing * .45f - wave * 2);
            bones[3].localRotation = Quaternion.Euler(-sample.advance * 7, 0, sample.wing * .45f + wave * 2);
            bones[4].localRotation = Quaternion.Euler(slow * 1.2f + sample.glow * 3, 0, 0);
            bones[5].localRotation = Quaternion.Euler(-slow * 1.2f + sample.glow * 3, 0, 0);
            bones[6].localRotation = Quaternion.Euler(0, slow * 9, wave * 2);
            bones[7].localRotation = Quaternion.Euler(0, 0, -sample.wing * .55f);
            bones[8].localRotation = Quaternion.Euler(0, 0, sample.wing * .55f);
            bones[9].localRotation = Quaternion.Euler(wave * 1.5f, 0, sample.tilt * .12f);
            if (Math.Abs(previousOpacity - opacity) > .002f || Math.Abs(previousGlow - sample.glow) > .01f)
            {
                previousOpacity = opacity; previousGlow = sample.glow;
                properties.SetFloat("_Opacity", opacity); properties.SetFloat("_Glow", sample.glow * .55f);
                Renderer.SetPropertyBlock(properties);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; root.SetActive(false); Object.Destroy(root);
            // The field library owns the shared rest mesh and material. Another
            // copy of this card may still be visible when this instance dies.
        }
    }

    // Model-space mesh primitives are combined into one vertex-colored mesh.
    // Each vertex belongs to a named rig joint; no per-part Renderer or collider
    // is created. Rounded surfaces retain analytic normals and flat accessories
    // retain their hard edges.
    internal sealed class SpiritMeshBuilder
    {
        private readonly string id;
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<BoneWeight> weights = new List<BoneWeight>();
        private readonly Vector3[] joints = new Vector3[BattleSpiritModel.JointCount];
        internal SpiritMeshBuilder(string id) { this.id = id; }
        internal void Bone(int index, Vector3 rest) { joints[index] = rest; }

        private static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        private static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        private static Vector3 Unit(Vector3 value)
        {
            double length = Math.Sqrt((double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z);
            return length < 1e-9 ? new Vector3(0, 1, 0) : value * (float)(1 / length);
        }
        private int Vertex(int bone, Vector3 position, Vector3 normal, Color color)
        {
            int index = positions.Count;
            positions.Add(position); normals.Add(Unit(normal)); colors.Add(color);
            weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
            return index;
        }
        private void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }

        internal void Ellipsoid(int bone, Vector3 center, Vector3 radius, Color color, int segments = 12, int rings = 8, Quaternion? rotation = null)
        {
            segments = Math.Max(6, segments); rings = Math.Max(4, rings);
            Quaternion turn = rotation ?? Quaternion.identity;
            int start = positions.Count, row = segments + 1;
            for (int y = 0; y <= rings; y++)
            {
                double phi = Math.PI * y / rings;
                float sin = (float)Math.Sin(phi), cos = (float)Math.Cos(phi);
                for (int x = 0; x <= segments; x++)
                {
                    double theta = Math.PI * 2 * x / segments;
                    var unit = new Vector3(sin * (float)Math.Cos(theta), cos, sin * (float)Math.Sin(theta));
                    var point = new Vector3(unit.x * radius.x, unit.y * radius.y, unit.z * radius.z);
                    var normal = new Vector3(unit.x / Math.Max(.001f, radius.x), unit.y / Math.Max(.001f, radius.y), unit.z / Math.Max(.001f, radius.z));
                    Vertex(bone, center + turn * point, turn * normal, color);
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < segments; x++)
                {
                    int a = start + y * row + x, b = a + 1, c = a + row, d = c + 1;
                    if (y > 0) Tri(a, b, c);
                    if (y < rings - 1) Tri(b, d, c);
                }
        }

        internal void Tube(int bone, Vector3 from, Vector3 to, float radiusA, float radiusB, Color color, int segments = 10)
        {
            var difference = to - from;
            float length = (float)Math.Sqrt(Dot(difference, difference));
            if (length < .0001f || Math.Max(radiusA, radiusB) <= 0) return;
            var direction = difference * (1 / length);
            var u = Unit(Cross(direction, Math.Abs(direction.y) > .9f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)));
            var v = Cross(direction, u);
            int start = positions.Count;
            for (int ring = 0; ring < 2; ring++)
                for (int index = 0; index <= segments; index++)
                {
                    double angle = index * Math.PI * 2 / segments;
                    var radial = u * (float)Math.Cos(angle) + v * (float)Math.Sin(angle);
                    Vertex(bone, (ring == 0 ? from : to) + radial * (ring == 0 ? radiusA : radiusB),
                        radial + direction * ((radiusA - radiusB) / length), color);
                }
            for (int index = 0; index < segments; index++)
            {
                int a = start + index, b = a + 1, c = a + segments + 1, d = c + 1;
                if (radiusA > 0) Tri(a, b, c);
                if (radiusB > 0) Tri(b, d, c);
            }
            if (radiusA > 0) Cap(from, radiusA, direction * -1, true);
            if (radiusB > 0) Cap(to, radiusB, direction, false);
            void Cap(Vector3 center, float radius, Vector3 normal, bool reverse)
            {
                int middle = Vertex(bone, center, normal, color), first = positions.Count;
                for (int index = 0; index <= segments; index++)
                {
                    double angle = index * Math.PI * 2 / segments;
                    Vertex(bone, center + (u * (float)Math.Cos(angle) + v * (float)Math.Sin(angle)) * radius, normal, color);
                }
                for (int index = 0; index < segments; index++)
                    if (reverse) Tri(middle, first + index + 1, first + index);
                    else Tri(middle, first + index, first + index + 1);
            }
        }

        internal void Capsule(int bone, Vector3 from, Vector3 to, float radius, Color color, int segments = 10)
        {
            Tube(bone, from, to, radius, radius, color, segments);
            Ellipsoid(bone, from, new Vector3(radius, radius, radius), color, segments, 6);
            Ellipsoid(bone, to, new Vector3(radius, radius, radius), color, segments, 6);
        }

        internal void Plate(int bone, Vector3[] outline, float depth, Color color)
        {
            if (outline == null || outline.Length < 3) return;
            depth = Math.Max(.018f, depth);
            var faceNormal = Vector3.zero;
            for (int index = 0; index < outline.Length; index++)
            {
                var a = outline[index]; var next = outline[(index + 1) % outline.Length];
                faceNormal += new Vector3((a.y - next.y) * (a.z + next.z),
                    (a.z - next.z) * (a.x + next.x), (a.x - next.x) * (a.y + next.y));
            }
            faceNormal = Unit(faceNormal);
            int drop = Math.Abs(faceNormal.x) > Math.Abs(faceNormal.y)
                ? Math.Abs(faceNormal.x) > Math.Abs(faceNormal.z) ? 0 : 2
                : Math.Abs(faceNormal.y) > Math.Abs(faceNormal.z) ? 1 : 2;
            Vector2 Project(Vector3 p) => drop == 0 ? new Vector2(p.y, p.z) : drop == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            double Cross2(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            double area = 0;
            for (int i = 0; i < outline.Length; i++)
            { var a = Project(outline[i]); var b = Project(outline[(i + 1) % outline.Length]); area += a.x * b.y - b.x * a.y; }
            double sign = area >= 0 ? 1 : -1;
            var remaining = new List<int>();
            int first = positions.Count;
            for (int i = 0; i < outline.Length; i++)
            { remaining.Add(i); Vertex(bone, outline[i] - faceNormal * (depth * .5f), faceNormal * -1, color); }
            int back = positions.Count;
            for (int i = 0; i < outline.Length; i++) Vertex(bone, outline[i] + faceNormal * (depth * .5f), faceNormal, color);
            // Ear clipping handles concave fins in arbitrary planes; a center
            // fan plus fixed Z normals inverted the sawfish's tail faces.
            while (remaining.Count >= 3)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    var pa = Project(outline[a]); var pb = Project(outline[b]); var pc = Project(outline[c]);
                    if (Cross2(pa, pb, pc) * sign <= 1e-9) continue;
                    bool contains = false;
                    foreach (int other in remaining)
                    {
                        if (other == a || other == b || other == c) continue;
                        var point = Project(outline[other]);
                        if (Cross2(pa, pb, point) * sign >= -1e-8 && Cross2(pb, pc, point) * sign >= -1e-8
                            && Cross2(pc, pa, point) * sign >= -1e-8) { contains = true; break; }
                    }
                    if (contains) continue;
                    Tri(first + a, first + c, first + b); Tri(back + a, back + b, back + c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) throw new InvalidOperationException("Invalid model plate in " + id);
            }
            for (int index = 0; index < outline.Length; index++)
            {
                Vector3 a = outline[index], b = outline[(index + 1) % outline.Length];
                Vector3 normal = Unit(Cross(b - a, faceNormal));
                int side = positions.Count;
                Vertex(bone, a - faceNormal * (depth * .5f), normal, color);
                Vertex(bone, b - faceNormal * (depth * .5f), normal, color);
                Vertex(bone, a + faceNormal * (depth * .5f), normal, color);
                Vertex(bone, b + faceNormal * (depth * .5f), normal, color);
                Tri(side, side + 1, side + 2); Tri(side + 1, side + 3, side + 2);
            }
        }

        internal void Gem(int bone, Vector3 center, Vector3 radius, Color color)
        {
            var points = new[] { new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1) };
            for (int top = 0; top < 2; top++)
                for (int side = 0; side < 4; side++)
                {
                    Vector3 a = Scale(points[top]), b = Scale(points[2 + side]), c = Scale(points[2 + (side + 1) % 4]);
                    var normal = Unit(Cross(b - a, c - a));
                    bool reverse = Dot(normal, (a + b + c) * (1f / 3)) < 0;
                    if (reverse) normal *= -1;
                    int first = Vertex(bone, center + a, normal, color);
                    Vertex(bone, center + b, normal, color); Vertex(bone, center + c, normal, color);
                    if (reverse) Tri(first, first + 2, first + 1); else Tri(first, first + 1, first + 2);
                }
            Vector3 Scale(Vector3 point) => new Vector3(point.x * radius.x, point.y * radius.y, point.z * radius.z);
        }

        internal SpiritModelTemplate Build(string profileName)
        {
            if (positions.Count == 0) throw new InvalidOperationException("An empty spirit mesh was generated: " + id);
            Vector3 min = positions[0], max = positions[0];
            foreach (var point in positions)
            {
                min = new Vector3(Math.Min(min.x, point.x), Math.Min(min.y, point.y), Math.Min(min.z, point.z));
                max = new Vector3(Math.Max(max.x, point.x), Math.Max(max.y, point.y), Math.Max(max.z, point.z));
            }
            var size = max - min;
            float scale = Math.Min(1, Math.Min(2.8f / Math.Max(.001f, size.x), Math.Min(3.6f / Math.Max(.001f, size.y), 2.8f / Math.Max(.001f, size.z))));
            var offset = new Vector3((min.x + max.x) * .5f, min.y, (min.z + max.z) * .5f);
            for (int index = 0; index < positions.Count; index++) positions[index] = (positions[index] - offset) * scale;
            var rig = new Vector3[joints.Length];
            for (int index = 0; index < rig.Length; index++) rig[index] = (joints[index] - offset) * scale;
            var mesh = new Mesh { name = "BS_OriginalSkinned_" + id };
            mesh.vertices = positions.ToArray(); mesh.normals = normals.ToArray(); mesh.colors = colors.ToArray();
            mesh.triangles = triangles.ToArray(); mesh.boneWeights = weights.ToArray(); mesh.RecalculateBounds();
            return new SpiritModelTemplate(id, profileName, mesh, rig);
        }
    }
}
