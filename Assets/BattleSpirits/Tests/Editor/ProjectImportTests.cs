using System.Linq;
using LaoDai.BattleSpirits.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LaoDai.BattleSpirits.Tests
{
    // These checks need the actual Unity importer and are intentionally separate
    // from the standalone core-rule tests. Run from the Editor Test Runner.
    public sealed class ProjectImportTests
    {
        [Test]
        public void ImportedCatalogBuildsThePracticeMatchWithUnityJsonUtility()
        {
            var asset = Resources.Load<TextAsset>("Data/cards");
            Assert.That(asset, Is.Not.Null, "Catalog was not imported as a TextAsset.");
            var catalog = JsonUtility.FromJson<CardsDatabase>(asset.text);
            Assert.That(catalog.cards.Length, Is.EqualTo(16));
            var match = BattleMatch.CreateDefault(catalog.cards);
            Assert.That(match.State.Players.All(p => p.Hand.Count == 4 && p.Deck.Count == 36));
            Assert.That(match.State.Players.All(p => p.Reserve.Normal == 3 && p.Reserve.Soul));
        }

        [Test]
        public void ImportedDragonHasThreeAnimatorCompatibleClips()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/BattleSpirits/Resources/Models/SummonedDragon.fbx")
                .OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            foreach (var name in new[] { "Idle", "Summon", "Attack" })
            {
                var clip = clips.FirstOrDefault(c => c.name.EndsWith(name));
                Assert.That(clip, Is.Not.Null, "Missing imported animation: " + name);
                Assert.That(clip.legacy, Is.False, "Expected a Generic/Animator clip: " + name);
                Assert.That(clip.length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void AllCatalogImagesAndVietnameseFontAreAvailableOffline()
        {
            var catalog = JsonUtility.FromJson<CardsDatabase>(Resources.Load<TextAsset>("Data/cards").text);
            foreach (var card in catalog.cards)
            {
                var art = Resources.Load<Texture2D>("CardArt/" + card.id);
                Assert.That(art, Is.Not.Null, "Missing card art: " + card.id);
                Assert.That(art.width, Is.GreaterThan(0));
            }
            var font = Resources.Load<Font>("UI/Fonts/NotoSansJP");
            Assert.That(font, Is.Not.Null);
            foreach (char character in "Tiếng Việt Triệu hồi Tấn công Phòng thủ ゴラドン")
                Assert.That(font.HasCharacter(character), Is.True, "Missing glyph: " + character);
        }
    }
}
