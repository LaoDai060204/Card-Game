using System;
using System.Collections.Generic;
using LaoDai.BattleSpirits.Core;
using UnityEngine;

namespace LaoDai.BattleSpirits.Presentation
{
    /// <summary>Owns a local match; presentation never modifies the rules state directly.</summary>
    public sealed class ArenaBootstrap : MonoBehaviour
    {
        private BattleMatch match;
        private ArenaHud hud;
        private ArenaWorld world;
        private ArenaAudio audioPlayer;
        private CardDefinition[] catalog;
        private int seed = 2026;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            audioPlayer = gameObject.AddComponent<ArenaAudio>();
            world = gameObject.AddComponent<ArenaWorld>();
            world.Build();
            hud = gameObject.AddComponent<ArenaHud>();
            hud.Initialize(this);
            try
            {
                var data = Resources.Load<TextAsset>("Data/cards");
                if (data == null)
                    throw new InvalidOperationException("Thiếu Assets/BattleSpirits/Resources/Data/cards.json.");
                var database = JsonUtility.FromJson<CardsDatabase>(data.text);
                if (database == null || database.cards == null)
                    throw new InvalidOperationException("Không đọc được danh mục thẻ.");
                catalog = database.cards;
                NewMatch();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                hud.ShowFatal("Không thể mở trận đấu", exception.Message);
            }
        }

        public void NewMatch()
        {
            if (catalog == null) return;
            try
            {
                if (match != null) match.EventEmitted -= OnBattleEvent;
                match = BattleMatch.CreateDefault(catalog, seed++);
                match.EventEmitted += OnBattleEvent;
                world.ResetMatch();
                hud.Bind(match);
                world.Synchronize(match);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                hud.ShowFatal("Không thể tạo trận đấu", exception.Message);
            }
        }

        public void Refresh()
        {
            world.Synchronize(match);
            hud.Refresh();
        }

        public void SelectSpirit(int instanceId)
        {
            world.SetSelected(instanceId);
        }

        private void OnBattleEvent(BattleEvent battleEvent)
        {
            world.OnBattleEvent(battleEvent, match);
            audioPlayer.Play(battleEvent.Kind);
        }

        private void OnDestroy()
        {
            if (match != null) match.EventEmitted -= OnBattleEvent;
        }
    }
}
