using MDPro3.BattleSpirits;
using Cysharp.Threading.Tasks;
using MDPro3.Duel.YGOSharp;
using MDPro3.Servant;
using MDPro3.UI.PropertyOverride;
using MDPro3.Utility;
using System.CodeDom;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YgomSystem.ElementSystem;

namespace MDPro3.UI
{
    public class DeckPickup : MonoBehaviour
    {

        #region Elements

        private ElementObjectManager m_Manager;
        private ElementObjectManager Manager =>
            m_Manager = m_Manager != null ? m_Manager 
            : GetComponent<ElementObjectManager>();

        private const string LABEL_IMG_DECKCASE = "DeckCase";
        private Image m_DeckCase;
        private Image DeckCase =>
            m_DeckCase = m_DeckCase != null ? m_DeckCase
            : Manager.GetElement<Image>(LABEL_IMG_DECKCASE);

        private const string LABEL_CRH_CARD1 = "Card1";
        private CardRawImageHandler m_Card1;
        private CardRawImageHandler Card1 =>
            m_Card1 = m_Card1 != null ? m_Card1
            : Manager.GetElement<CardRawImageHandler>(LABEL_CRH_CARD1);

        private const string LABEL_CRH_CARD2 = "Card2";
        private CardRawImageHandler m_Card2;
        private CardRawImageHandler Card2 =>
            m_Card2 = m_Card2 != null ? m_Card2
            : Manager.GetElement<CardRawImageHandler>(LABEL_CRH_CARD2);

        private const string LABEL_CRH_CARD3 = "Card3";
        private CardRawImageHandler m_Card3;
        private CardRawImageHandler Card3 =>
            m_Card3 = m_Card3 != null ? m_Card3
            : Manager.GetElement<CardRawImageHandler>(LABEL_CRH_CARD3);

        private const string LABEL_TXT_SETTING = "TextSetting";
        private TextMeshProUGUI m_TextSetting;
        private TextMeshProUGUI TextSetting =>
            m_TextSetting = m_TextSetting != null ? m_TextSetting
            : Manager.GetElement<TextMeshProUGUI>(LABEL_TXT_SETTING);

        #endregion

        public async void SetDeck(Deck deck)
        {
            deck ??= new Deck();
            var pickups = new int[3];
            for (int i = 0; i < pickups.Length; i++)
                pickups[i] = deck.Pickup != null && i < deck.Pickup.Count ? deck.Pickup[i] : 0;
            var setting = string.Empty;
            setting += GetCardName(pickups[0], 1);
            setting += GetCardName(pickups[1], 2);
            setting += GetCardName(pickups[2], 3);
            TextSetting.text = setting;

            Card1.protectorCode = deck.Protector;
            Card2.protectorCode = deck.Protector;
            Card3.protectorCode = deck.Protector;

            Card1.SetCard(pickups[0]);
            Card2.SetCard(pickups[1]);
            Card3.SetCard(pickups[2]);
            await LoadDeckCaseAsync(deck.Case);
        }

        private async UniTask LoadDeckCaseAsync(int deckCase)
        {
            var sprite = await BattleCardAdapter.BackIconAsync(destroyCancellationToken);
            if (sprite != null)
                DeckCase.sprite = sprite;
        }

        private string GetCardName(int code, int index)
        {
            var result = $"{index} : ";
            var card = CardsManager.Get(code);
            if (card == null || card.Id == 0)
                result += InterString.Get("Not Set");
            else
                result += card.Name;
            result += Program.STRING_LINE_BREAK;
            return result;
        }
    }
}
