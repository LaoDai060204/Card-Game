using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaoDai.BattleSpirits.Visuals
{
    // Original stylized mesh interpretations of the printed illustrations.
    // These are hand-authored anatomy profiles, not extracted official models.
    internal static class BattleSpiritModelProfiles
    {
        private static readonly string[] ids = {
            "26RBS01-003", "26RBS01-019", "26RBS01-064", "26RBS01-078",
            "26RBS02-002", "26RBS02-020", "26RBS02-025", "26RBS02-043", "26RBS02-051",
            "26RBS03-021", "26RBS03-025", "26RBS03-052", "26RBS03-061", "26RBS03-065",
            "26RBS03-066", "26RBS03-068", "26RBS03-071", "26RBS03-072", "26RBS03-074"
        };
        private static readonly IReadOnlyList<string> supportedIds = Array.AsReadOnly(ids);
        internal static IReadOnlyList<string> Ids => supportedIds;

        internal static bool TryBuild(string cardId, out SpiritModelTemplate template)
        {
            template = null;
            if (cardId == null || Array.IndexOf(ids, cardId) < 0) return false;
            var b = new SpiritMeshBuilder(cardId);
            string name;
            switch (cardId)
            {
                case "26RBS01-003": CloudDragon(b); name = "Scud Beast · cloud dragon"; break;
                case "26RBS01-019": SkeletonBull(b); name = "Bull Crayten · skeletal bull"; break;
                case "26RBS01-064": Sorcerer(b); name = "Sorceryd · crescent staff knight"; break;
                case "26RBS01-078": Satyr(b); name = "Eripides · ivy horn musician"; break;
                case "26RBS02-002": FlameRabbit(b); name = "Lop Yara · flame rabbit"; break;
                case "26RBS02-020": Werewolf(b); name = "Dooboo · sword pierced werewolf"; break;
                case "26RBS02-025": Jellyfish(b); name = "Shizerin · emerald jellyfish"; break;
                case "26RBS02-043": ArmoredBull(b); name = "Pharacattle · crystal armored bull"; break;
                case "26RBS02-051": Marionette(b); name = "Swordonette · wooden rapier puppet"; break;
                case "26RBS03-021": Troll(b); name = "Bailfall · crystal troll"; break;
                case "26RBS03-025": Sawfish(b); name = "Forinon · jeweled sawfish"; break;
                case "26RBS03-052": Salamander(b); name = "Inktooth · salamander swordsman"; break;
                case "26RBS03-061": Pumpkin(b); name = "Bochack · pumpkin axe scarecrow"; break;
                case "26RBS03-065": LeafCat(b); name = "Rakakal · leaf garland cat"; break;
                case "26RBS03-066": HangingPossum(b); name = "Spearish · hanging blue possum"; break;
                case "26RBS03-068": RatSentinel(b); name = "Alwary · rat horn sentinel"; break;
                case "26RBS03-071": Anteater(b); name = "Myrmecotira · raised claw anteater"; break;
                case "26RBS03-072": Beetle(b); name = "Grillol · armored cricket beetle"; break;
                case "26RBS03-074": Worm(b); name = "Glazboil · armored four jaw worm"; break;
                default: return false;
            }
            template = b.Build(name); return true;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private static Color C(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
        private static void E(SpiritMeshBuilder b, int bone, float x, float y, float z, float rx, float ry, float rz, Color c, int segments = 12, int rings = 8)
            => b.Ellipsoid(bone, V(x, y, z), V(rx, ry, rz), c, segments, rings);
        private static void T(SpiritMeshBuilder b, int bone, Vector3 a, Vector3 z, float ra, float rz, Color c)
            => b.Tube(bone, a, z, ra, rz, c, 10);
        private static void L(SpiritMeshBuilder b, int bone, Vector3 a, Vector3 z, float radius, Color c)
            => b.Capsule(bone, a, z, radius, c, 10);
        private static void G(SpiritMeshBuilder b, int bone, float x, float y, float z, float rx, float ry, float rz, Color c)
            => b.Gem(bone, V(x, y, z), V(rx, ry, rz), c);
        private static void Eye(SpiritMeshBuilder b, int bone, Vector3 p, float r, Color glow)
        {
            E(b, bone, p.x, p.y, p.z, r * 1.25f, r * .9f, r * .55f, C(0x111827), 8, 6);
            E(b, bone, p.x, p.y, p.z - r * .44f, r * .67f, r * .68f, r * .3f, glow, 8, 6);
            E(b, bone, p.x - r * .18f, p.y + r * .23f, p.z - r * .7f, r * .19f, r * .21f, r * .12f, C(0xffffff), 6, 4);
        }
        private static void Horn(SpiritMeshBuilder b, int bone, Vector3 a, Vector3 m, Vector3 z, float radius, Color c)
        { T(b, bone, a, m, radius, radius * .65f, c); T(b, bone, m, z, radius * .65f, .006f, c); }
        private static void Leaf(SpiritMeshBuilder b, int bone, Vector3 p, float width, float height, Color c, float angle = 0)
        {
            var q = Quaternion.Euler(0, 0, angle);
            b.Plate(bone, new[] { p + q * V(0, height, 0), p + q * V(width, 0, 0), p + q * V(0, -height * .8f, 0), p + q * V(-width, 0, 0) }, .035f, c);
        }
        private static void Claws(SpiritMeshBuilder b, int bone, Vector3 p, Color c, float length = .23f)
        {
            for (int i = -1; i <= 1; i++)
                T(b, bone, p + V(i * .085f, 0, 0), p + V(i * .11f, -.04f, -length), .035f, .005f, c);
        }
        private static void UprightBones(SpiritMeshBuilder b, float head = 2.3f, float shoulders = 1.8f, float hip = .9f)
        {
            b.Bone(0, V(0, 1.4f, 0)); b.Bone(1, V(0, head, -.02f));
            b.Bone(2, V(-.48f, shoulders, 0)); b.Bone(3, V(.48f, shoulders, 0));
            b.Bone(4, V(-.23f, hip, 0)); b.Bone(5, V(.23f, hip, 0));
            b.Bone(6, V(0, hip, .28f)); b.Bone(7, V(-.3f, shoulders, .2f)); b.Bone(8, V(.3f, shoulders, .2f));
            b.Bone(9, V(.65f, shoulders - .2f, -.1f));
        }
        private static void QuadBones(SpiritMeshBuilder b, float height = 1.2f)
        {
            b.Bone(0, V(0, height, 0)); b.Bone(1, V(0, height + .25f, -.7f));
            b.Bone(2, V(-.42f, height, -.4f)); b.Bone(3, V(.42f, height, -.4f));
            b.Bone(4, V(-.42f, height, .5f)); b.Bone(5, V(.42f, height, .5f));
            b.Bone(6, V(0, height, .7f)); b.Bone(7, V(-.3f, height + .2f, .1f)); b.Bone(8, V(.3f, height + .2f, .1f));
            b.Bone(9, V(0, height + .4f, 0));
        }
        private static void QuadLegs(SpiritMeshBuilder b, Color upper, Color lower, bool hooves = true)
        {
            for (int sign = -1; sign <= 1; sign += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    int bone = end < 0 ? sign < 0 ? 2 : 3 : sign < 0 ? 4 : 5;
                    float z = end * .48f;
                    L(b, bone, V(sign * .43f, 1.15f, z), V(sign * .5f, .55f, z + .1f), .17f, upper);
                    L(b, bone, V(sign * .5f, .56f, z + .1f), V(sign * .54f, .17f, z - .06f), .1f, lower);
                    E(b, bone, sign * .54f, .13f, z - .14f, .19f, .13f, .26f, hooves ? C(0x282335) : lower, 10, 6);
                }
        }

        private static void CloudDragon(SpiritMeshBuilder b)
        {
            QuadBones(b); Color skin = C(0x302a50), cloud = C(0x514165), fire = C(0xffb52d), orange = C(0xf56827);
            E(b, 0, 0, 1.05f, .05f, .48f, .42f, .8f, skin);
            E(b, 1, 0, 1.47f, -.66f, .4f, .35f, .5f, cloud);
            E(b, 1, 0, 1.34f, -1.03f, .32f, .18f, .36f, skin);
            E(b, 1, 0, 1.30f, -1.30f, .26f, .12f, .04f, fire);
            Eye(b, 1, V(-.25f, 1.56f, -1.03f), .09f, fire); Eye(b, 1, V(.25f, 1.56f, -1.03f), .09f, fire);
            QuadLegs(b, skin, cloud, false);
            Horn(b, 1, V(-.22f, 1.72f, -.6f), V(-.43f, 2.05f, -.32f), V(-.55f, 2.11f, -.06f), .11f, skin);
            Horn(b, 1, V(.22f, 1.72f, -.6f), V(.43f, 2.05f, -.32f), V(.55f, 2.11f, -.06f), .11f, skin);
            Horn(b, 6, V(0, 1.14f, .68f), V(.34f, 1.35f, 1.23f), V(.83f, 1.02f, 1.62f), .23f, skin);
            for (int i = 0; i < 5; i++)
            {
                float z = -.25f + i * .32f;
                E(b, 0, (i % 2 == 0 ? -.08f : .11f), 1.48f, z, .2f, .18f, .21f, cloud, 10, 6);
                G(b, 0, 0, 1.65f, z, .11f, .15f, .12f, orange);
                T(b, 0, V(-.30f, 1.35f, z), V(-.66f, 1.64f, z + .11f), .12f, .005f, skin);
                T(b, 0, V(.30f, 1.35f, z), V(.66f, 1.64f, z + .11f), .12f, .005f, skin);
            }
            for (int i = -1; i <= 1; i++)
                T(b, 1, V(i * .14f, 1.28f, -1.25f), V(i * .18f, 1.4f + Math.Abs(i) * .12f, -1.75f), .09f, .005f, i == 0 ? fire : orange);
        }
        private static void SkeletonBull(SpiritMeshBuilder b)
        {
            QuadBones(b); Color dark = C(0x302f42), ivory = C(0xded9c8), bone = C(0x9795a3), purple = C(0xdd6dff);
            E(b, 0, 0, 1.12f, .1f, .40f, .36f, .74f, dark);
            QuadLegs(b, dark, bone);
            E(b, 1, 0, 1.39f, -.76f, .32f, .32f, .45f, ivory);
            E(b, 1, 0, 1.18f, -1.03f, .23f, .26f, .32f, ivory);
            E(b, 1, 0, 1.17f, -1.31f, .14f, .11f, .04f, dark);
            Eye(b, 1, V(-.19f, 1.45f, -1.07f), .08f, purple); Eye(b, 1, V(.19f, 1.45f, -1.07f), .08f, purple);
            for (int s = -1; s <= 1; s += 2)
            {
                Horn(b, 1, V(s * .24f, 1.58f, -.66f), V(s * .70f, 1.91f, -.62f), V(s * .86f, 2.1f, -.92f), .13f, C(0x1e1928));
                G(b, 0, s * .4f, 1.32f, -.30f, .14f, .19f, .10f, purple);
                for (int i = 0; i < 5; i++)
                    L(b, 0, V(s * .1f, 1.49f, -.2f + i * .19f), V(s * .39f, 1.12f, -.17f + i * .19f), .043f, ivory);
            }
            for (int i = 0; i < 6; i++)
            { E(b, 0, 0, 1.5f, -.24f + i * .18f, .1f, .075f, .09f, ivory, 8, 5); T(b, 0, V(0, 1.52f, -.24f + i * .18f), V(0, 1.75f, -.24f + i * .18f), .075f, .004f, bone); }
            Horn(b, 6, V(0, 1.2f, .8f), V(.18f, .94f, 1.23f), V(.33f, 1.22f, 1.45f), .08f, ivory);
        }
        private static void Sorcerer(SpiritMeshBuilder b)
        {
            UprightBones(b); Color silver = C(0xc6cedd), dark = C(0x343c55), gold = C(0xe8bc4f), gem = C(0xffc742);
            E(b, 0, 0, 1.52f, 0, .43f, .57f, .28f, dark);
            E(b, 0, 0, 1.58f, -.19f, .36f, .43f, .18f, silver);
            G(b, 0, 0, 1.68f, -.37f, .16f, .21f, .07f, gold);
            E(b, 1, 0, 2.25f, 0, .29f, .35f, .28f, silver);
            b.Plate(1, new[] { V(-.25f, 2.35f, -.27f), V(.25f, 2.35f, -.27f), V(.19f, 2.11f, -.34f), V(-.19f, 2.11f, -.34f) }, .05f, dark);
            Eye(b, 1, V(-.1f, 2.28f, -.32f), .042f, gem); Eye(b, 1, V(.1f, 2.28f, -.32f), .042f, gem);
            T(b, 1, V(0, 2.52f, .04f), V(0, 2.93f, .15f), .12f, .005f, gold);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, arm, s * .54f, 1.96f, 0, .24f, .22f, .27f, gold);
                L(b, arm, V(s * .56f, 1.85f, 0), V(s * .75f, 1.33f, -.13f), .14f, silver);
                E(b, arm, s * .76f, 1.25f, -.13f, .18f, .2f, .18f, dark);
                L(b, leg, V(s * .24f, 1.04f, 0), V(s * .33f, .57f, -.06f), .18f, dark);
                E(b, leg, s * .33f, .57f, -.13f, .19f, .24f, .18f, gold);
                L(b, leg, V(s * .33f, .51f, -.02f), V(s * .34f, .18f, -.18f), .15f, silver);
                E(b, leg, s * .34f, .13f, -.27f, .20f, .12f, .34f, dark);
            }
            L(b, 9, V(.89f, .12f, -.19f), V(.89f, 2.62f, -.19f), .045f, gold);
            E(b, 9, .89f, 2.62f, -.19f, .22f, .22f, .22f, gem);
            Horn(b, 9, V(.60f, 2.51f, -.19f), V(.53f, 2.82f, -.19f), V(.72f, 3.04f, -.19f), .075f, gold);
            Horn(b, 9, V(1.18f, 2.51f, -.19f), V(1.25f, 2.82f, -.19f), V(1.06f, 3.04f, -.19f), .075f, gold);
        }
        private static void Satyr(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.48f, 1.91f, .98f); Color skin = C(0xf1d6c7), hair = C(0xece8dc), green = C(0x527b35), leaf = C(0x7eaa48), horn = C(0x493830);
            E(b, 0, 0, 1.74f, 0, .29f, .53f, .22f, skin);
            E(b, 1, 0, 2.46f, -.04f, .25f, .32f, .24f, skin);
            E(b, 1, 0, 2.54f, .08f, .29f, .32f, .23f, hair);
            Eye(b, 1, V(-.095f, 2.47f, -.257f), .035f, C(0x51c4cb)); Eye(b, 1, V(.095f, 2.47f, -.257f), .035f, C(0x51c4cb));
            for (int s = -1; s <= 1; s += 2)
            {
                Horn(b, 1, V(s * .16f, 2.72f, .02f), V(s * .40f, 2.95f, .28f), V(s * .63f, 2.91f, .63f), .13f, horn);
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                L(b, arm, V(s * .30f, 1.99f, -.01f), V(s * .40f, 1.73f, -.28f), .09f, skin);
                L(b, arm, V(s * .40f, 1.73f, -.28f), V(s * .24f, 2.20f, -.48f), .075f, skin);
                E(b, leg, s * .20f, .90f, .02f, .22f, .63f, .25f, hair);
                L(b, leg, V(s * .20f, .60f, .02f), V(s * .22f, .19f, -.06f), .085f, hair);
                E(b, leg, s * .22f, .12f, -.13f, .12f, .14f, .20f, horn);
                for (int i = 0; i < 4; i++) Leaf(b, 0, V(s * (.11f + i * .075f), 1.35f - i * .045f, -.21f), .11f, .22f, i % 2 == 0 ? green : leaf, s * (10 + i * 12));
                Leaf(b, 0, V(s * .18f, 1.95f, -.21f), .12f, .22f, green, s * 25);
            }
            for (int i = 0; i < 5; i++) E(b, 1, .18f + i * .07f, 2.35f - i * .16f, .20f + i * .07f, .12f - i * .011f, .16f, .12f, hair, 10, 6);
            L(b, 9, V(0, 2.30f, -.28f), V(-.47f, 2.29f, -.66f), .05f, C(0xe8bf61));
            T(b, 9, V(-.47f, 2.29f, -.66f), V(-.58f, 2.29f, -.76f), .07f, .15f, C(0xcda244));
            G(b, 0, .27f, 1.92f, -.22f, .09f, .12f, .06f, C(0x54d4f2));
        }
        private static void FlameRabbit(SpiritMeshBuilder b)
        {
            UprightBones(b, 1.70f, 1.12f, .48f); Color orange = C(0xf1a13d), cream = C(0xffe4ac), fire = C(0xffc93e), ruby = C(0xdf3032);
            E(b, 0, 0, .86f, .05f, .50f, .60f, .42f, orange);
            E(b, 0, 0, .85f, -.28f, .37f, .43f, .15f, cream);
            E(b, 1, 0, 1.72f, -.04f, .58f, .50f, .42f, orange);
            E(b, 1, -.3f, 1.51f, -.34f, .24f, .19f, .18f, cream); E(b, 1, .3f, 1.51f, -.34f, .24f, .19f, .18f, cream);
            Eye(b, 1, V(-.23f, 1.83f, -.41f), .09f, C(0x402a28)); Eye(b, 1, V(.23f, 1.83f, -.41f), .09f, C(0x402a28));
            G(b, 1, 0, 2.05f, -.38f, .13f, .20f, .08f, ruby);
            E(b, 1, 0, 1.57f, -.51f, .075f, .06f, .055f, C(0xa94c3b), 8, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                int ear = s < 0 ? 7 : 8; b.Bone(ear, V(s * .39f, 2.05f, 0));
                L(b, ear, V(s * .40f, 2.02f, 0), V(s * 1.03f, 1.82f, .02f), .22f, orange);
                E(b, ear, s * 1.08f, 1.58f, .01f, .24f, .43f, .17f, orange);
                E(b, ear, s * 1.08f, 1.60f, -.14f, .12f, .28f, .055f, cream, 10, 6);
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, arm, s * .46f, .93f, -.11f, .17f, .25f, .21f, orange);
                E(b, leg, s * .31f, .23f, -.17f, .24f, .21f, .38f, cream);
                T(b, leg, V(s * .42f, .34f, -.2f), V(s * .48f, .70f, -.21f), .13f, .005f, fire);
            }
            E(b, 6, 0, .61f, .48f, .24f, .25f, .24f, cream);
        }
        private static void Werewolf(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.38f, 1.91f, .93f); Color fur = C(0x3e405a), light = C(0x797b94), purple = C(0xd85dff), steel = C(0xaebbc7);
            E(b, 0, 0, 1.63f, .02f, .36f, .56f, .24f, fur);
            E(b, 1, 0, 2.40f, -.04f, .29f, .35f, .30f, fur);
            E(b, 1, 0, 2.27f, -.35f, .18f, .13f, .29f, light);
            Eye(b, 1, V(-.15f, 2.45f, -.30f), .065f, purple); Eye(b, 1, V(.15f, 2.45f, -.30f), .065f, purple);
            for (int s = -1; s <= 1; s += 2)
            {
                T(b, 1, V(s * .21f, 2.64f, -.02f), V(s * .32f, 2.98f, .08f), .13f, .005f, fur);
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                L(b, arm, V(s * .43f, 1.94f, .02f), V(s * .65f, 1.31f, -.06f), .14f, fur);
                E(b, arm, s * .66f, 1.20f, -.08f, .18f, .20f, .16f, light); Claws(b, arm, V(s * .66f, 1.13f, -.16f), C(0xd0cfb8));
                L(b, leg, V(s * .20f, 1.13f, .04f), V(s * .31f, .64f, .12f), .17f, fur);
                L(b, leg, V(s * .31f, .64f, .12f), V(s * .34f, .20f, -.12f), .105f, light);
                E(b, leg, s * .34f, .15f, -.24f, .18f, .14f, .29f, fur);
                G(b, 0, s * .17f, 1.77f, -.24f, .12f, .20f, .08f, purple);
                for (int i = 0; i < 3; i++) T(b, arm, V(s * .45f, 1.80f - i * .15f, .02f), V(s * .73f, 1.93f - i * .15f, .12f), .10f, .005f, light);
            }
            Horn(b, 6, V(0, .95f, .21f), V(.28f, .72f, .61f), V(.42f, 1.04f, .99f), .18f, fur);
            L(b, 9, V(-.77f, .90f, -.08f), V(-.77f, 1.33f, -.08f), .05f, C(0x846041));
            T(b, 9, V(-.77f, 1.32f, -.08f), V(-.77f, 2.51f, -.13f), .09f, .006f, steel);
            for (int i = 0; i < 3; i++) T(b, 0, V(-.28f + i * .22f, 1.65f - i * .16f, .12f), V(-.64f + i * .40f, 2.31f - i * .1f, .43f), .055f, .008f, steel);
        }
        private static void Jellyfish(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.10f, 1.62f, 1.15f); Color mint = C(0x7ae6bc), rim = C(0x3fb999), dark = C(0x126a62), glow = C(0x5bffcb);
            E(b, 0, 0, 2.02f, .0f, .94f, .58f, .72f, mint, 16, 10);
            E(b, 0, 0, 1.70f, -.02f, .97f, .13f, .76f, rim, 16, 6);
            E(b, 0, 0, 1.73f, -.65f, .46f, .24f, .12f, dark);
            Eye(b, 0, V(-.20f, 1.83f, -.75f), .07f, C(0xe7fff5)); Eye(b, 0, V(.20f, 1.83f, -.75f), .07f, C(0xe7fff5));
            L(b, 0, V(-.10f, 1.63f, -.77f), V(.10f, 1.63f, -.77f), .035f, glow);
            G(b, 0, -.43f, 2.18f, -.58f, .22f, .26f, .14f, C(0x24bb8a)); G(b, 0, .43f, 2.18f, -.58f, .22f, .26f, .14f, C(0x24bb8a));
            for (int i = 0; i < 8; i++)
            {
                float x = (i - 3.5f) * .19f, sway = (i % 2 == 0 ? 1 : -1) * .15f;
                int bone = i < 4 ? 7 : 8;
                b.Bone(bone, V(i < 4 ? -.35f : .35f, 1.62f, .05f));
                T(b, bone, V(x, 1.65f, .04f), V(x + sway, 1.02f, -.02f), .043f, .032f, mint);
                T(b, bone, V(x + sway, 1.02f, -.02f), V(x - sway * .5f, .30f + (i % 3) * .09f, .07f), .032f, .009f, glow);
            }
        }
        private static void ArmoredBull(SpiritMeshBuilder b)
        {
            QuadBones(b, 1.30f); Color plate = C(0xe1dbb9), gold = C(0xcead51), joint = C(0x4e3d5e), pink = C(0xf681e9);
            E(b, 0, 0, 1.20f, .14f, .62f, .50f, .82f, plate);
            E(b, 0, 0, 1.45f, -.18f, .64f, .38f, .45f, gold);
            QuadLegs(b, plate, gold);
            E(b, 1, 0, 1.43f, -.78f, .39f, .38f, .44f, plate);
            E(b, 1, 0, 1.23f, -1.09f, .29f, .18f, .32f, gold);
            Eye(b, 1, V(-.22f, 1.48f, -1.10f), .07f, pink); Eye(b, 1, V(.22f, 1.48f, -1.10f), .07f, pink);
            for (int s = -1; s <= 1; s += 2)
            {
                Horn(b, 1, V(s * .29f, 1.61f, -.73f), V(s * .79f, 1.80f, -.59f), V(s * 1.0f, 2.12f, -.89f), .15f, gold);
                G(b, 0, s * .49f, 1.27f, -.47f, .13f, .18f, .10f, pink);
                E(b, 0, s * .49f, 1.12f, .2f, .18f, .27f, .60f, joint);
            }
            G(b, 1, 0, 1.65f, -.99f, .14f, .18f, .10f, pink);
            for (int i = 0; i < 5; i++) G(b, 0, 0, 1.81f, -.12f + i * .22f, .11f, .30f - i * .025f, .12f, pink);
            Horn(b, 6, V(0, 1.24f, .87f), V(.25f, 1.2f, 1.3f), V(.37f, 1.47f, 1.51f), .085f, gold);
        }
        private static void Marionette(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.38f, 1.87f, .96f); Color wood = C(0xc6a36e), dark = C(0x693e31), gold = C(0xf0c851), steel = C(0xe7e4d5);
            E(b, 0, 0, 1.50f, 0, .28f, .48f, .19f, wood);
            E(b, 0, 0, 1.50f, -.14f, .24f, .32f, .10f, dark);
            E(b, 1, 0, 2.37f, -.03f, .24f, .36f, .19f, wood);
            for (int s = -1; s <= 1; s += 2) E(b, 1, s * .09f, 2.44f, -.207f, .043f, .063f, .015f, C(0x30231d), 8, 6);
            E(b, 1, 0, 2.25f, -.21f, .07f, .055f, .015f, C(0x30231d), 8, 6);
            L(b, 0, V(0, 1.89f, 0), V(0, 2.10f, 0), .08f, dark);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, arm, s * .39f, 1.89f, 0, .13f, .13f, .13f, gold, 10, 6);
                L(b, arm, V(s * .41f, 1.83f, 0), V(s * .62f, 1.44f, -.04f), .075f, wood);
                E(b, arm, s * .62f, 1.44f, -.04f, .12f, .12f, .12f, dark, 10, 6);
                L(b, arm, V(s * .62f, 1.40f, -.04f), V(s * .74f, 1.20f, -.18f), .07f, wood);
                G(b, arm, s * .74f, 1.17f, -.18f, .14f, .17f, .13f, gold);
                L(b, leg, V(s * .17f, 1.04f, 0), V(s * .24f, .60f, -.03f), .09f, wood);
                G(b, leg, s * .24f, .60f, -.03f, .13f, .13f, .13f, gold);
                L(b, leg, V(s * .24f, .56f, -.03f), V(s * .28f, .20f, -.1f), .075f, wood);
                E(b, leg, s * .28f, .15f, -.19f, .12f, .11f, .25f, dark);
                T(b, 0, V(s * .39f, 1.93f, .06f), V(s * .32f, 3.02f, .04f), .012f, .012f, C(0x8c8d83));
            }
            L(b, 9, V(.72f, 1.11f, -.19f), V(.72f, 1.43f, -.19f), .04f, dark);
            E(b, 9, .72f, 1.44f, -.19f, .17f, .035f, .17f, gold);
            T(b, 9, V(.72f, 1.46f, -.19f), V(.85f, 2.82f, -.31f), .028f, .001f, steel);
            L(b, 0, V(-.38f, 3.03f, .04f), V(.38f, 3.03f, .04f), .025f, dark);
        }
        private static void Troll(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.29f, 1.96f, .91f); Color gray = C(0x6c7383), hair = C(0xc5c6d1), dark = C(0x333447), crystal = C(0xae6df1), wood = C(0x71513e);
            E(b, 0, 0, 1.45f, .12f, .67f, .64f, .42f, gray);
            E(b, 1, 0, 2.30f, -.06f, .43f, .38f, .35f, dark);
            E(b, 1, 0, 2.46f, .02f, .48f, .29f, .37f, hair);
            Eye(b, 1, V(-.17f, 2.34f, -.37f), .055f, crystal); Eye(b, 1, V(.17f, 2.34f, -.37f), .055f, crystal);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, arm, s * .72f, 1.90f, 0, .35f, .36f, .33f, gray);
                L(b, arm, V(s * .77f, 1.80f, -.02f), V(s * .98f, 1.10f, -.19f), .26f, gray);
                E(b, arm, s * 1.00f, .96f, -.22f, .28f, .30f, .27f, dark);
                L(b, leg, V(s * .31f, 1.01f, .10f), V(s * .48f, .53f, .10f), .25f, gray);
                E(b, leg, s * .48f, .22f, -.05f, .26f, .23f, .37f, dark);
                for (int i = 0; i < 4; i++) T(b, 1, V(s * (.10f + i * .075f), 2.43f, -.15f), V(s * (.09f + i * .09f), 1.72f + i * .11f, -.38f), .13f, .005f, hair);
            }
            for (int i = 0; i < 5; i++) G(b, 0, -.36f + i * .18f, 1.95f, .43f, .12f, .34f, .16f, crystal);
            L(b, 9, V(-1.01f, 1.20f, -.05f), V(.84f, 2.52f, .10f), .10f, wood);
            E(b, 9, .70f, 2.43f, .10f, .38f, .45f, .26f, wood);
            G(b, 9, .60f, 2.56f, -.13f, .13f, .15f, .08f, crystal);
        }
        private static void Sawfish(SpiritMeshBuilder b)
        {
            QuadBones(b); Color blue = C(0x254d74), dark = C(0x192a46), silver = C(0xa1b7c2), gem = C(0x5cd5ab);
            E(b, 0, 0, 1.48f, .05f, .43f, .33f, .82f, blue);
            E(b, 1, 0, 1.49f, -.66f, .33f, .23f, .42f, dark);
            E(b, 1, 0, 1.48f, -1.30f, .13f, .075f, .67f, silver);
            for (int i = 0; i < 8; i++)
                for (int s = -1; s <= 1; s += 2) T(b, 1, V(s * .11f, 1.49f, -.82f - i * .15f), V(s * .25f, 1.49f, -.86f - i * .15f), .045f, .004f, silver);
            Eye(b, 1, V(-.25f, 1.58f, -.77f), .065f, C(0xff625a)); Eye(b, 1, V(.25f, 1.58f, -.77f), .065f, C(0xff625a));
            for (int s = -1; s <= 1; s += 2)
            {
                b.Plate(s < 0 ? 7 : 8, new[] { V(s * .25f, 1.42f, -.2f), V(s * 1.1f, 1.28f, .2f), V(s * .34f, 1.40f, .43f) }, .06f, dark);
                b.Plate(0, new[] { V(s * .2f, 1.43f, .43f), V(s * .66f, 1.27f, .79f), V(s * .24f, 1.42f, .82f) }, .04f, silver);
                L(b, 0, V(s * .24f, 1.68f, -.32f), V(s * .25f, 1.66f, .46f), .035f, silver);
            }
            T(b, 6, V(0, 1.48f, .65f), V(0, 1.49f, 1.25f), .23f, .06f, blue);
            b.Plate(6, new[] { V(0, 1.48f, 1.18f), V(-.56f, 1.81f, 1.59f), V(0, 1.48f, 1.38f), V(.56f, 1.14f, 1.59f) }, .06f, dark);
            for (int i = 0; i < 3; i++) G(b, 0, 0, 1.78f, -.05f + i * .26f, .12f, .18f, .12f, gem);
        }
        private static void Salamander(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.20f, 1.72f, .77f); Color orange = C(0xe9a149), cream = C(0xf4dfaf), teal = C(0x3e7c7f), steel = C(0xd6e1dd);
            E(b, 0, 0, 1.22f, .05f, .54f, .62f, .36f, orange);
            E(b, 0, 0, 1.12f, -.26f, .40f, .43f, .16f, cream);
            E(b, 1, 0, 2.15f, -.02f, .48f, .38f, .39f, orange);
            E(b, 1, 0, 1.99f, -.25f, .42f, .18f, .24f, cream);
            Eye(b, 1, V(-.23f, 2.22f, -.35f), .08f, C(0x4a3930)); Eye(b, 1, V(.23f, 2.22f, -.35f), .08f, C(0x4a3930));
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, arm, s * .52f, 1.73f, .03f, .24f, .20f, .27f, teal);
                L(b, arm, V(s * .52f, 1.67f, .03f), V(s * .69f, 1.24f, -.14f), .16f, orange);
                E(b, arm, s * .70f, 1.18f, -.18f, .18f, .16f, .20f, orange);
                E(b, leg, s * .31f, .48f, -.01f, .22f, .33f, .23f, orange);
                E(b, leg, s * .32f, .17f, -.20f, .23f, .14f, .34f, cream);
                Leaf(b, 0, V(s * .26f, 1.62f, -.29f), .17f, .32f, teal, s * 10);
            }
            Horn(b, 6, V(0, .80f, .31f), V(.58f, .45f, .82f), V(.88f, 1.10f, 1.06f), .26f, orange);
            for (int i = 0; i < 4; i++) G(b, 6, .24f + i * .15f, .68f + i * .07f, .62f + i * .10f, .09f, .12f, .07f, C(0xd8b346));
            L(b, 9, V(.78f, 1.12f, -.18f), V(.78f, 1.46f, -.18f), .045f, C(0x78513a));
            b.Plate(9, new[] { V(.70f, 1.45f, -.18f), V(.80f, 2.16f, -.18f), V(1.05f, 2.38f, -.18f), V(.91f, 1.45f, -.18f) }, .035f, steel);
        }
        private static void Pumpkin(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.10f, 1.50f, .70f); Color pumpkin = C(0xe38a32), sack = C(0xc0ac80), leaf = C(0x4c783b), glow = C(0x69dcff), dark = C(0x593928);
            E(b, 0, 0, 1.10f, 0, .38f, .48f, .29f, sack);
            for (int i = -2; i <= 2; i++) E(b, 1, i * .18f, 2.07f, -.02f, .25f, .48f - Math.Abs(i) * .05f, .42f, pumpkin, 10, 8);
            T(b, 1, V(0, 2.49f, .0f), V(.12f, 2.82f, .04f), .12f, .075f, leaf);
            b.Plate(1, new[] { V(-.33f, 2.19f, -.44f), V(-.10f, 2.25f, -.44f), V(-.18f, 1.99f, -.44f) }, .02f, glow);
            b.Plate(1, new[] { V(.33f, 2.19f, -.44f), V(.10f, 2.25f, -.44f), V(.18f, 1.99f, -.44f) }, .02f, glow);
            E(b, 1, 0, 1.89f, -.42f, .25f, .075f, .04f, glow, 10, 5);
            G(b, 0, 0, 1.17f, -.28f, .16f, .19f, .08f, glow);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                L(b, arm, V(s * .34f, 1.44f, 0), V(s * .70f, 1.12f, -.13f), .105f, sack);
                E(b, arm, s * .74f, 1.10f, -.13f, .14f, .12f, .15f, dark);
                L(b, leg, V(s * .18f, .74f, .01f), V(s * .27f, .18f, -.05f), .12f, sack);
                E(b, leg, s * .27f, .13f, -.16f, .17f, .12f, .25f, dark);
                for (int i = 0; i < 3; i++) Leaf(b, 0, V(s * (.14f + i * .12f), 1.54f - i * .06f, -.13f), .10f, .22f, leaf, s * (20 + i * 15));
            }
            L(b, 9, V(-1.05f, 1.06f, -.17f), V(.87f, 1.24f, -.17f), .065f, dark);
            b.Plate(9, new[] { V(-1.05f, .78f, -.18f), V(-1.39f, .92f, -.18f), V(-1.45f, 1.40f, -.18f), V(-1.05f, 1.63f, -.18f), V(-.87f, 1.28f, -.18f) }, .12f, pumpkin);
        }
        private static void LeafCat(SpiritMeshBuilder b)
        {
            UprightBones(b, 1.90f, 1.25f, .65f); Color orange = C(0xdca25a), cream = C(0xf6e4bb), green = C(0x527b37), gem = C(0x63d1d5);
            E(b, 0, 0, 1.03f, .10f, .47f, .61f, .39f, orange);
            E(b, 0, 0, 1.10f, -.23f, .33f, .44f, .14f, cream);
            E(b, 1, 0, 1.94f, -.04f, .48f, .39f, .34f, orange);
            E(b, 1, -.18f, 1.78f, -.31f, .21f, .17f, .12f, cream); E(b, 1, .18f, 1.78f, -.31f, .21f, .17f, .12f, cream);
            Eye(b, 1, V(-.19f, 2.01f, -.31f), .065f, C(0x69b99a)); Eye(b, 1, V(.19f, 2.01f, -.31f), .065f, C(0x69b99a));
            E(b, 1, 0, 1.82f, -.44f, .055f, .035f, .04f, C(0xb66d68), 8, 5);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                T(b, 1, V(s * .29f, 2.20f, -.01f), V(s * .37f, 2.55f, -.03f), .17f, .005f, orange);
                E(b, arm, s * .29f, .85f, -.24f, .14f, .49f, .15f, orange);
                E(b, arm, s * .30f, .40f, -.27f, .19f, .14f, .23f, cream);
                E(b, leg, s * .41f, .48f, .14f, .25f, .31f, .27f, orange);
                G(b, 0, s * .40f, 1.31f, -.10f, .11f, .15f, .08f, gem);
                for (int i = 0; i < 3; i++) Leaf(b, 0, V(s * (.10f + i * .11f), 1.51f - i * .08f, -.24f), .09f, .15f, green, s * (10 + i * 20));
                for (int i = 0; i < 3; i++) Leaf(b, arm, V(s * .30f + (i - 1) * .08f, .57f, -.31f), .07f, .12f, green, (i - 1) * 25);
            }
            Horn(b, 6, V(.23f, .62f, .37f), V(.83f, .49f, .72f), V(.93f, 1.28f, .47f), .14f, orange);
            for (int i = 0; i < 5; i++) E(b, 0, (float)Math.Sin(i * Math.PI * 2 / 5) * .09f, 1.43f + (float)Math.Cos(i * Math.PI * 2 / 5) * .09f, -.37f, .075f, .075f, .04f, C(0xaf8dd9), 8, 5);
            G(b, 0, 0, 1.43f, -.41f, .06f, .06f, .04f, gem);
        }
        private static void HangingPossum(SpiritMeshBuilder b)
        {
            UprightBones(b, 1.06f, 1.58f, 2.20f); Color blue = C(0x467da3), white = C(0xeee9d9), pink = C(0xdba8a4), green = C(0x4d7839), dark = C(0x2c3c54);
            E(b, 0, 0, 1.81f, 0, .35f, .56f, .27f, blue);
            E(b, 0, 0, 1.82f, -.22f, .28f, .43f, .11f, white);
            E(b, 1, 0, 1.07f, -.04f, .43f, .39f, .32f, white);
            E(b, 1, 0, 1.25f, .07f, .43f, .25f, .27f, blue);
            Eye(b, 1, V(-.16f, 1.02f, -.34f), .06f, C(0x394253)); Eye(b, 1, V(.16f, 1.02f, -.34f), .06f, C(0x394253));
            E(b, 1, 0, .86f, -.36f, .065f, .05f, .055f, pink, 8, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, 1, s * .37f, 1.22f, .0f, .21f, .24f, .11f, dark); E(b, 1, s * .37f, 1.22f, -.09f, .12f, .15f, .04f, pink, 10, 6);
                L(b, arm, V(s * .28f, 1.65f, .01f), V(s * .58f, 1.30f, -.15f), .08f, blue);
                E(b, arm, s * .61f, 1.24f, -.17f, .10f, .11f, .11f, pink);
                L(b, leg, V(s * .19f, 2.17f, .03f), V(s * .26f, 2.48f, -.08f), .09f, blue);
                E(b, leg, s * .26f, 2.54f, -.11f, .12f, .11f, .11f, pink);
                Leaf(b, 0, V(s * .18f, 1.51f, -.23f), .14f, .30f, green, s * 30);
            }
            b.Bone(6, V(0, 2.26f, .03f));
            Horn(b, 6, V(0, 2.26f, .09f), V(.25f, 2.87f, .22f), V(.0f, 3.13f, .17f), .12f, blue);
            Horn(b, 6, V(.0f, 3.13f, .17f), V(-.31f, 3.03f, .11f), V(-.21f, 2.77f, .04f), .11f, blue);
            G(b, 0, -.12f, 1.75f, -.30f, .07f, .08f, .05f, C(0x66cbe0)); G(b, 0, .13f, 2.01f, -.29f, .07f, .08f, .05f, C(0x66cbe0));
        }
        private static void RatSentinel(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.12f, 1.70f, .80f); Color blue = C(0x547ea2), pale = C(0xc8d3d5), pink = C(0xd9a3a4), green = C(0x57763e), bark = C(0x7b684a), gem = C(0x6ac9eb);
            E(b, 0, 0, 1.31f, .04f, .34f, .48f, .29f, blue);
            E(b, 1, 0, 2.10f, -.04f, .35f, .34f, .29f, blue);
            E(b, 1, 0, 1.98f, -.30f, .24f, .16f, .27f, pale);
            E(b, 1, 0, 1.99f, -.54f, .07f, .055f, .06f, pink);
            Eye(b, 1, V(-.15f, 2.18f, -.26f), .055f, C(0x253c50)); Eye(b, 1, V(.15f, 2.18f, -.26f), .055f, C(0x253c50));
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                E(b, 1, s * .35f, 2.37f, .01f, .26f, .28f, .11f, blue); E(b, 1, s * .35f, 2.37f, -.09f, .19f, .20f, .05f, pink);
                L(b, arm, V(s * .31f, 1.71f, 0), V(s * .46f, 1.44f, -.27f), .09f, blue);
                L(b, arm, V(s * .46f, 1.44f, -.27f), V(s * .22f, 1.95f, -.47f), .065f, pale);
                L(b, leg, V(s * .19f, .89f, 0), V(s * .23f, .20f, -.06f), .10f, blue);
                E(b, leg, s * .23f, .15f, -.16f, .13f, .11f, .22f, pink);
                G(b, arm, s * .36f, 1.72f, -.12f, .10f, .14f, .08f, gem);
                Leaf(b, 1, V(s * .14f, 2.57f, -.02f), .19f, .29f, green, s * 48);
            }
            for (int i = -2; i <= 2; i++) Leaf(b, 0, V(i * .13f, .85f, -.24f), .10f, .32f, bark, i * 9);
            Horn(b, 6, V(0, .89f, .27f), V(.72f, .50f, .55f), V(.91f, 1.01f, .72f), .055f, pink);
            L(b, 9, V(0, 1.99f, -.50f), V(-.54f, 1.95f, -.78f), .045f, bark);
            T(b, 9, V(-.54f, 1.95f, -.78f), V(-.65f, 1.95f, -.86f), .06f, .13f, bark);
        }
        private static void Anteater(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.21f, 1.81f, .83f); Color blue = C(0x547b9b), cream = C(0xe2ded2), dark = C(0x324f73), gem = C(0x60cbe7);
            E(b, 0, 0, 1.40f, .09f, .56f, .67f, .38f, blue);
            E(b, 0, 0, 1.37f, -.23f, .40f, .51f, .15f, cream);
            E(b, 1, 0, 2.25f, -.02f, .29f, .34f, .30f, blue);
            T(b, 1, V(0, 2.18f, -.19f), V(0, 1.90f, -.96f), .22f, .045f, cream);
            E(b, 1, 0, 1.91f, -.99f, .065f, .055f, .07f, dark, 8, 6);
            Eye(b, 1, V(-.17f, 2.31f, -.25f), .045f, dark); Eye(b, 1, V(.17f, 2.31f, -.25f), .045f, dark);
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3, leg = s < 0 ? 4 : 5;
                L(b, arm, V(s * .45f, 1.86f, .05f), V(s * .89f, 2.33f, .0f), .19f, blue);
                L(b, arm, V(s * .89f, 2.33f, .0f), V(s * .94f, 2.79f, -.09f), .14f, cream);
                E(b, arm, s * .94f, 2.85f, -.12f, .20f, .16f, .19f, cream); Claws(b, arm, V(s * .94f, 2.91f, -.17f), dark, .34f);
                L(b, leg, V(s * .28f, .94f, .05f), V(s * .33f, .22f, -.04f), .17f, blue);
                E(b, leg, s * .34f, .15f, -.19f, .22f, .14f, .30f, cream);
            }
            G(b, 0, 0, 1.49f, -.39f, .19f, .23f, .08f, gem);
            Horn(b, 6, V(0, .89f, .32f), V(.36f, .46f, .98f), V(.83f, .70f, 1.38f), .28f, dark);
            for (int i = 0; i < 4; i++) T(b, 6, V(.31f + i * .12f, .60f, .87f + i * .11f), V(.24f + i * .17f, .94f, .94f + i * .12f), .13f, .005f, blue);
        }
        private static void Beetle(SpiritMeshBuilder b)
        {
            UprightBones(b, 1.88f, 1.42f, .84f); Color shell = C(0x4189ae), blue = C(0x263e73), violet = C(0x665594), gold = C(0xd6ba6d), pink = C(0xdc9ecb);
            E(b, 0, 0, 1.24f, .24f, .65f, .65f, .65f, shell);
            E(b, 0, 0, 1.59f, -.23f, .40f, .39f, .30f, violet);
            E(b, 1, 0, 1.96f, -.43f, .35f, .28f, .27f, gold);
            Eye(b, 1, V(-.22f, 1.98f, -.60f), .13f, C(0x7de7e7)); Eye(b, 1, V(.22f, 1.98f, -.60f), .13f, C(0x7de7e7));
            for (int s = -1; s <= 1; s += 2)
            {
                int arm = s < 0 ? 2 : 3;
                L(b, arm, V(s * .43f, 1.55f, -.20f), V(s * .85f, 1.41f, -.49f), .16f, blue);
                E(b, arm, s * .91f, 1.25f, -.64f, .24f, .37f, .24f, violet);
                Claws(b, arm, V(s * .91f, 1.10f, -.74f), gold, .33f);
                for (int i = 0; i < 3; i++)
                {
                    int leg = s < 0 ? 4 : 5; float z = -.08f + i * .36f;
                    L(b, leg, V(s * .43f, 1.05f, z), V(s * .88f, .70f, z + .13f), .085f, blue);
                    T(b, leg, V(s * .88f, .70f, z + .13f), V(s * 1.05f, .16f, z - .01f), .07f, .02f, gold);
                }
                Horn(b, 1, V(s * .16f, 2.15f, -.42f), V(s * .25f, 2.47f, -.40f), V(s * .49f, 2.60f, -.48f), .027f, blue);
                b.Plate(s < 0 ? 7 : 8, new[] { V(s * .25f, 1.65f, .15f), V(s * .92f, 2.15f, .42f), V(s * 1.04f, 1.60f, .52f), V(s * .45f, 1.40f, .32f) }, .035f, pink);
                for (int i = 0; i < 3; i++) T(b, arm, V(s * .91f, 1.43f - i * .13f, -.62f), V(s * 1.14f, 1.56f - i * .13f, -.67f), .075f, .003f, gold);
            }
            L(b, 0, V(0, 1.76f, .17f), V(0, 1.58f, .76f), .035f, blue);
        }
        private static void Worm(SpiritMeshBuilder b)
        {
            UprightBones(b, 2.22f, 2.02f, .75f); Color purple = C(0x4d476f), plate = C(0x34354f), blue = C(0x6a87b0), turquoise = C(0x58bbd0), mouth = C(0x781d40), tongue = C(0xd86c87), tooth = C(0xe3dcca);
            for (int i = 0; i < 6; i++)
            {
                float y = .34f + i * .33f, radius = .32f + i * .045f;
                E(b, i < 3 ? 6 : 0, 0, y, .15f + (i < 2 ? .14f : 0), radius, .25f, radius * .84f, i % 2 == 0 ? purple : plate, 14, 8);
                G(b, 0, -.27f, y + .05f, -.13f, .065f, .09f, .05f, blue);
                G(b, 0, .27f, y + .05f, -.13f, .065f, .09f, .05f, blue);
            }
            E(b, 1, 0, 2.23f, -.05f, .54f, .51f, .40f, purple);
            E(b, 1, 0, 2.20f, -.40f, .36f, .35f, .10f, mouth);
            for (int lobe = 0; lobe < 4; lobe++)
            {
                double angle = lobe * Math.PI / 2;
                float x = (float)Math.Sin(angle), y = (float)Math.Cos(angle);
                int bone = lobe == 0 ? 7 : lobe == 2 ? 8 : 1;
                E(b, bone, x * .49f, 2.20f + y * .49f, -.32f, .23f, .34f, .22f, plate, 12, 8);
                E(b, bone, x * .57f, 2.20f + y * .57f, -.48f, .16f, .28f, .07f, mouth, 10, 6);
                Horn(b, bone, V(x * .46f, 2.20f + y * .45f, -.42f), V(x * .81f, 2.20f + y * .72f, -.64f), V(x * .81f, 2.20f + y * .72f + .23f, -.82f), .10f, turquoise);
                for (int i = -1; i <= 1; i++)
                    T(b, bone, V(x * .58f + y * i * .09f, 2.20f + y * .58f - x * i * .09f, -.53f), V(x * .41f + y * i * .08f, 2.20f + y * .41f - x * i * .08f, -.61f), .047f, .004f, tooth);
            }
            L(b, 1, V(0, 2.06f, -.42f), V(.16f, 1.84f, -.90f), .11f, tongue);
            T(b, 1, V(.16f, 1.84f, -.90f), V(.43f, 1.89f, -1.12f), .11f, .035f, tongue);
            G(b, 1, 0, 2.65f, -.35f, .13f, .18f, .08f, blue);
        }
    }
}
