using System.Collections.Generic;
using UnityEngine;

namespace LaoDai.BattleSpirits.Visuals
{
    // Original low-poly interpretations. These are not extracted Bandai models.
    internal sealed class BattleSpiritGeometry
    {
        internal readonly GameObject root;
        internal readonly Transform leftWing, rightWing, head, tail;
        internal readonly GameObject breath;
        internal readonly bool boss;
        private readonly Renderer[] renderers;
        private readonly List<Mesh> meshRegistry;
        private readonly Mesh[] ownedMeshes;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private float previousGlow = -1;

        internal BattleSpiritGeometry(Transform parent, string cardId, Material skin, Material armor,
            Material joint, Material energy, List<Mesh> meshes, bool nexus = false)
        {
            meshRegistry = meshes; int firstMesh = meshes.Count;
            root = new GameObject("Spirit_" + cardId); root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            boss = cardId == "BS01-X01" || cardId == "BS01-097";
            bool machine = cardId == "BS01-097";
            if (nexus)
            {
                Part(root.transform, "NexusFoundation", PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(1.8f, 0.15f, 1.8f), armor);
                Part(root.transform, "NexusPillar", PrimitiveType.Cube, new Vector3(0, 1, 0), new Vector3(0.65f, 1.7f, 0.65f), skin);
                var diamond = Part(root.transform, "NexusCrystal", PrimitiveType.Cube, new Vector3(0, 2.15f, 0), new Vector3(0.66f, 0.66f, 0.66f), energy);
                diamond.transform.localRotation = Quaternion.Euler(30, 45, 30);
                for (int side = -1; side <= 1; side += 2)
                    Rod(root.transform, "NexusArch", new Vector3(side * 0.8f, 0.3f, 0), new Vector3(side * 0.45f, 1.8f, 0), 0.08f, armor);
                head = diamond.transform;
            }
            else if (boss || cardId.StartsWith("BS01-00", System.StringComparison.Ordinal))
            {
                Part(root.transform, "Chest", machine ? PrimitiveType.Cube : PrimitiveType.Sphere, new Vector3(0, 1.25f, 0), new Vector3(0.9f, 1.2f, 1.25f), skin);
                Part(root.transform, "ChestArmor", PrimitiveType.Cube, new Vector3(0, 1.32f, -0.4f), new Vector3(0.76f, 0.82f, 0.34f), armor);
                Rod(root.transform, "Neck", new Vector3(0, 1.6f, 0.25f), new Vector3(0, 2.05f, 0.65f), 0.21f, skin);
                head = Pivot(root.transform, "Head", new Vector3(0, 2.12f, 0.72f));
                Part(head, "Skull", machine ? PrimitiveType.Cube : PrimitiveType.Sphere, Vector3.zero, new Vector3(0.6f, 0.48f, 0.64f), skin);
                Part(head, "Snout", PrimitiveType.Cube, new Vector3(0, -0.08f, 0.37f), new Vector3(0.37f, 0.22f, 0.57f), armor);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.27f, 0.09f, 0.18f), new Vector3(0.09f, 0.1f, 0.17f), energy);
                    Rod(head, "Horn", new Vector3(side * 0.2f, 0.18f, -0.12f), new Vector3(side * 0.34f, 0.7f, -0.35f), 0.065f, armor);
                    var thigh = Part(root.transform, "HindLeg", PrimitiveType.Sphere, new Vector3(side * 0.48f, 0.65f, -0.32f), new Vector3(0.48f, 0.7f, 0.5f), skin);
                    Rod(root.transform, "Shin", new Vector3(side * 0.5f, 0.45f, -0.28f), new Vector3(side * 0.62f, 0.2f, 0.2f), 0.13f, armor);
                    Part(root.transform, "Foot", PrimitiveType.Cube, new Vector3(side * 0.62f, 0.12f, 0.35f), new Vector3(0.38f, 0.2f, 0.65f), armor);
                    Rod(root.transform, "ForeArm", new Vector3(side * 0.43f, 1.5f, 0.3f), new Vector3(side * 0.72f, 1.1f, 0.75f), 0.11f, skin);
                    for (int claw = 0; claw < 3; claw++)
                        Rod(root.transform, "Claw", new Vector3(side * 0.72f + (claw - 1) * 0.08f, 1.1f, 0.75f), new Vector3(side * 0.72f + (claw - 1) * 0.1f, 1f, 0.99f), 0.025f, armor);
                }
                leftWing = Wing(root.transform, -1, machine, skin, armor, joint, meshes);
                rightWing = Wing(root.transform, 1, machine, skin, armor, joint, meshes);
                tail = Pivot(root.transform, "Tail", new Vector3(0, 0.95f, -0.65f));
                Rod(tail, "TailBase", Vector3.zero, new Vector3(0, -0.35f, -0.8f), 0.2f, skin);
                Rod(tail, "TailTip", new Vector3(0, -0.35f, -0.8f), new Vector3(0.35f, -0.25f, -1.65f), 0.09f, armor);
                breath = Part(head, "ElementBreath", PrimitiveType.Sphere, new Vector3(0, -0.08f, 1.14f), new Vector3(0.4f, 0.35f, 1.5f), energy);
                breath.SetActive(false);
                for (int plate = 0; plate < 4; plate++)
                    Part(root.transform, "BackSpine", PrimitiveType.Cube, new Vector3(0, 1.7f - plate * 0.17f, -0.1f - plate * 0.25f), new Vector3(0.12f, 0.35f, 0.18f), armor);
            }
            else
            {
                // A distinct upright guardian silhouette for ordinary white/purple/yellow Spirits.
                Part(root.transform, "Torso", PrimitiveType.Cube, new Vector3(0, 1.05f, 0), new Vector3(0.8f, 0.8f, 0.45f), skin);
                head = Pivot(root.transform, "Head", new Vector3(0, 1.75f, 0));
                Part(head, "Helm", PrimitiveType.Cube, Vector3.zero, new Vector3(0.53f, 0.46f, 0.48f), armor);
                Part(head, "Visor", PrimitiveType.Cube, new Vector3(0, 0, 0.25f), new Vector3(0.42f, 0.09f, 0.04f), energy);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root.transform, "Shoulder", PrimitiveType.Sphere, new Vector3(side * 0.65f, 1.35f, 0), new Vector3(0.55f, 0.45f, 0.5f), armor);
                    Rod(root.transform, "Arm", new Vector3(side * 0.7f, 1.2f, 0), new Vector3(side * 0.8f, 0.64f, 0.15f), 0.15f, joint);
                    Part(root.transform, "Gauntlet", PrimitiveType.Cube, new Vector3(side * 0.8f, 0.58f, 0.2f), new Vector3(0.3f, 0.35f, 0.38f), armor);
                    Rod(root.transform, "Leg", new Vector3(side * 0.25f, 0.7f, 0), new Vector3(side * 0.32f, 0.16f, 0.05f), 0.16f, joint);
                    Part(root.transform, "Boot", PrimitiveType.Cube, new Vector3(side * 0.32f, 0.12f, 0.2f), new Vector3(0.37f, 0.22f, 0.56f), armor);
                }
                Part(root.transform, "CoreCrystal", PrimitiveType.Sphere, new Vector3(0, 1.12f, 0.25f), new Vector3(0.25f, 0.35f, 0.2f), energy);
                Rod(root.transform, "Blade", new Vector3(0.9f, 0.7f, 0.3f), new Vector3(0.9f, 1.8f, 0.4f), 0.06f, energy);
            }
            renderers = root.GetComponentsInChildren<Renderer>(true);
            ownedMeshes = meshes.GetRange(firstMesh, meshes.Count - firstMesh).ToArray();
        }

        internal void Dispose()
        {
            Object.Destroy(root);
            foreach (var mesh in ownedMeshes) { meshRegistry.Remove(mesh); Object.Destroy(mesh); }
        }

        internal void Animate(BattleVisualPose pose, float age, float clock, Vector3 position, float facing, bool exhausted, float layoutScale = 1f)
        {
            var sample = BattleVisualMotion.Sample(pose, age, clock);
            float size = boss ? 1.13f : 0.9f;
            root.transform.localPosition = position + new Vector3(0, sample.height, sample.advance * facing);
            root.transform.localRotation = Quaternion.Euler(sample.tilt, facing < 0 ? 180 : 0, exhausted ? -20 : 0);
            root.transform.localScale = Vector3.one * (sample.scale * size * layoutScale);
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(-8, 0, -sample.wing);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(-8, 0, sample.wing);
            if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(clock * 1.5f) * 9, 0);
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(clock * 1.7f) * 3, 0, 0);
            if (breath != null) breath.SetActive(pose == BattleVisualPose.Attack && age > 0.14f && age < 0.48f);
            if (Mathf.Abs(previousGlow - sample.glow) > 0.025f)
            {
                previousGlow = sample.glow; properties.SetFloat("_Glow", sample.glow * 0.7f);
                foreach (var renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(properties);
            }
        }

        private static Transform Wing(Transform parent, int side, bool machine, Material skin, Material armor, Material membrane, List<Mesh> meshes)
        {
            var root = Pivot(parent, side < 0 ? "LeftWing" : "RightWing", new Vector3(side * 0.36f, 1.6f, -0.1f));
            var elbow = new Vector3(side * 1.05f, 0.65f, -0.1f);
            var tip = new Vector3(side * (machine ? 2.8f : 2.3f), 0.5f, -0.4f);
            var lower = new Vector3(side * 1.7f, -0.38f, -0.75f);
            Rod(root, "WingSpar", Vector3.zero, elbow, 0.09f, armor);
            Rod(root, "WingFinger", elbow, tip, 0.07f, armor);
            Rod(root, "WingRib", elbow, lower, 0.045f, skin);
            var wing = new GameObject(machine ? "GoldWingPlate" : "WingMembrane");
            wing.layer = parent.gameObject.layer; wing.transform.SetParent(root, false);
            var mesh = new Mesh { name = "BS original wing mesh" };
            var inner = new Vector3(side * 0.45f, -0.35f, -0.45f);
            // Back faces need their own vertices. Sharing them with opposite
            // triangles cancels the recalculated normals and breaks shading.
            mesh.vertices = new[] { Vector3.zero, elbow, tip, lower, inner, Vector3.zero, elbow, tip, lower, inner };
            mesh.triangles = new[] { 0, 1, 4, 1, 2, 3, 1, 3, 4, 9, 6, 5, 8, 7, 6, 9, 8, 6 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            wing.AddComponent<MeshFilter>().sharedMesh = mesh;
            wing.AddComponent<MeshRenderer>().sharedMaterial = machine ? armor : membrane;
            if (machine)
                for (int blade = 0; blade < 3; blade++)
                    Part(root, "WingBlade", PrimitiveType.Cube, new Vector3(side * (1.25f + blade * 0.47f), 0.22f, -0.3f - blade * 0.1f), new Vector3(0.18f, 0.1f, 1.12f - blade * 0.16f), armor);
            return root;
        }

        internal static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var result = new GameObject(name); result.layer = parent.gameObject.layer;
            result.transform.SetParent(parent, false); result.transform.localPosition = localPosition;
            return result.transform;
        }

        internal static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape); part.name = name; part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>(); if (collider != null) Object.Destroy(collider);
            return part;
        }

        internal static GameObject Rod(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 difference = to - from;
            var rod = Part(parent, name, PrimitiveType.Cylinder, (from + to) * 0.5f,
                new Vector3(radius * 2, difference.magnitude * 0.5f, radius * 2), material);
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, difference);
            return rod;
        }
    }
}
