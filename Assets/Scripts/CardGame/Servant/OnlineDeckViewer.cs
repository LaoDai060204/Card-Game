using DG.Tweening;
using CardGame;
using CardGame.UI;
using CardGame.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using CardGame.UI.PropertyOverride;
using CardGame.UI.ServantUI;

namespace CardGame.Servant
{
    public class OnlineDeckViewer : Servant
    {

        public static OnlineDeck.OnlineDeckData[] decks;
        [HideInInspector] public SelectionToggle_DeckOnline lastSelectedDeckItem;

        public override int Depth => 5;
        protected override bool ShowLine => true;

        #region Servant
        public override void Initialize()
        {
            returnServant = Program.instance.deckSelector;
            base.Initialize();
        }

        protected override void ApplyShowArrangement(int preDepth)
        {
            base.ApplyShowArrangement(preDepth);
            RefreshList();
        }

        public override void Select(bool forced = false)
        {
            if (!forced && !UserInput.NeedDefaultSelect())
                return;

            lastSelectedDeckItem.GetSelectable().Select();
        }

        public override void PerFrameFunction()
        {
            if (NeedResponseInput())
            {
                if (UserInput.MouseRightDown || UserInput.WasCancelPressed)
                    OnReturn();
                if (UserInput.WasGamepadButtonWestPressed)
                {
                    AudioManager.PlaySE("SE_MENU_SELECT_01");
                    GetUI<OnlineDeckViewerUI>().InputDeckName.ActivateInputField();
                }
                if (UserInput.WasGamepadButtonNorthPressed)
                {
                    AudioManager.PlaySE("SE_MENU_SELECT_01");
                    GetUI<OnlineDeckViewerUI>().InputDeckAuthor.ActivateInputField();
                }
            }
        }

        #endregion

        public void RefreshList()
        {
            decks = null;
            StartCoroutine(RefreshAsync());
        }

        private IEnumerator RefreshAsync()
        {
            var task = OnlineDeck.FetchSimpleDeckList(10000
                , GetUI<OnlineDeckViewerUI>().InputDeckName.text
                , GetUI<OnlineDeckViewerUI>().InputDeckAuthor.text);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
            {
                decks = task.Result;
                if (decks == null)
                {
                    MessageManager.Cast(InterString.Get("Network error. Failed to retrieve the online deck list."));
                    yield break;
                }
                GetUI<OnlineDeckViewerUI>().Print();
            }
            else
                MessageManager.Cast(InterString.Get("Network error. Failed to retrieve the online deck list."));
        }
    }
}
