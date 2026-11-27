using MDPro3.BattleSpirits;
using MDPro3.BattleSpirits.Core;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MDPro3.Duel.YGOSharp;
using MDPro3.Net;
using MDPro3.Servant;
using MDPro3.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MDPro3.Servant.DeckEditor;

namespace MDPro3.UI.ServantUI
{
    public class DeckEditorUI : ServantUI
    {

        #region Elements

        private const string LABEL_MONO_DECKVIEW = "DeckView";
        private DeckView m_DeckView;
        public DeckView DeckView =>
            m_DeckView = m_DeckView != null ? m_DeckView
            : Manager.GetElement<DeckView>(LABEL_MONO_DECKVIEW);

        private const string LABEL_MONO_CARDCOLLECTIONVIEW = "CardCollectionView";
        private CardCollectionView m_CardCollectionView;
        public CardCollectionView CardCollectionView =>
            m_CardCollectionView = m_CardCollectionView != null ? m_CardCollectionView
            : Manager.GetElement<CardCollectionView>(LABEL_MONO_CARDCOLLECTIONVIEW);

        private const string LABEL_MONO_CARDDETAILVIEW = "CardDetailView";
        private CardDetailView m_CardDetailView;
        public CardDetailView CardDetailView =>
            m_CardDetailView = m_CardDetailView != null ? m_CardDetailView
            : Manager.GetElement<CardDetailView>(LABEL_MONO_CARDDETAILVIEW);

        private const string LABEL_MONO_CARDACTIONMENU = "CardActionMenu";
        private CardActionMenu m_CardActionMenu;
        public CardActionMenu CardActionMenu =>
            m_CardActionMenu = m_CardActionMenu != null ? m_CardActionMenu
            : Manager.GetElement<CardActionMenu>(LABEL_MONO_CARDACTIONMENU);

        private const string LABEL_RT_DRAGCARD = "DragCard";
        private RectTransform m_DragCard;
        public RectTransform DragCard =>
        m_DragCard = m_DragCard != null ? m_DragCard
            : Manager.GetElement<RectTransform>(LABEL_RT_DRAGCARD);

        private const string LABEL_RIMG_DRAGCARDIMAGE = "DragCard/ImageCard";
        private RawImage m_DragCardImage;
        public RawImage DragCardImage =>
            m_DragCardImage = m_DragCardImage != null ? m_DragCardImage
            : Manager.GetNestedElement<RawImage>(LABEL_RIMG_DRAGCARDIMAGE);

        #region Header

        private const string LABEL_SBN_BACK = "Header/ButtonBack";
        private SelectionButton m_ButtonBack;
        private SelectionButton ButtonBack =>
            m_ButtonBack = m_ButtonBack != null ? m_ButtonBack
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_BACK);
        private RectTransform m_RectBack;
        private RectTransform RectBack =>
            m_RectBack = m_RectBack != null ? m_RectBack
            : ButtonBack.GetComponent<RectTransform>();

        private const string LABEL_SBN_INFO = "Header/ButtonInfoSwitching";
        private SelectionButton m_ButtonInfo;
        private SelectionButton ButtonInfo =>
            m_ButtonInfo = m_ButtonInfo != null ? m_ButtonInfo
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_INFO);

        private const string LABEL_SBN_REGULATION = "Header/ButtonRegulation";
        private SelectionButton m_ButtonRegulation;
        private SelectionButton ButtonRegulation =>
            m_ButtonRegulation = m_ButtonRegulation != null ? m_ButtonRegulation
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_REGULATION);

        private const string LABEL_SBN_TEST = "Header/ButtonTest";
        private SelectionButton m_ButtonTest;
        private SelectionButton ButtonTest =>
            m_ButtonTest = m_ButtonTest != null ? m_ButtonTest
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_TEST);

        private const string LABEL_SBN_SAVE = "Header/ButtonSave";
        private SelectionButton m_ButtonSave;
        private SelectionButton ButtonSave =>
            m_ButtonSave = m_ButtonSave != null ? m_ButtonSave
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_SAVE);

        private const string LABEL_SBN_CHANGESIDE = "Header/ButtonChangeSide";
        private SelectionButton m_ButtonChangeSide;
        private SelectionButton ButtonChangeSide =>
            m_ButtonChangeSide = m_ButtonChangeSide != null ? m_ButtonChangeSide
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_CHANGESIDE);

        #endregion

        #region Over Head

        private const string LABEL_SBN_APPEARANCE = "AppearanceGroup";
        private SelectionButton m_ButtonAppearance;
        private SelectionButton ButtonAppearance =>
            m_ButtonAppearance = m_ButtonAppearance != null ? m_ButtonAppearance
            : Manager.GetNestedElement<SelectionButton>(LABEL_SBN_APPEARANCE);

        private const string LABEL_IMG_ICONCASE = "OverHeader/IconCase";
        private Image m_IconCase;
        public Image IconCase =>
            m_IconCase = m_IconCase != null ? m_IconCase
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONCASE);

        private const string LABEL_IMG_ICONPROTECTOR = "OverHeader/IconProtector";
        private Image m_IconProtector;
        public Image IconProtector =>
            m_IconProtector = m_IconProtector != null ? m_IconProtector
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONPROTECTOR);

        private const string LABEL_IMG_ICONFIELD = "OverHeader/IconField";
        private Image m_IconField;
        public Image IconField =>
            m_IconField = m_IconField != null ? m_IconField
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONFIELD);

        private const string LABEL_IMG_ICONGRAVE = "OverHeader/IconGrave";
        private Image m_IconGrave;
        public Image IconGrave =>
            m_IconGrave = m_IconGrave != null ? m_IconGrave
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONGRAVE);

        private const string LABEL_IMG_ICONSTAND = "OverHeader/IconStand";
        private Image m_IconStand;
        public Image IconStand =>
            m_IconStand = m_IconStand != null ? m_IconStand
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONSTAND);

        private const string LABEL_IMG_ICONMATE = "OverHeader/IconMate";
        private Image m_IconMate;
        public Image IconMate =>
            m_IconMate = m_IconMate != null ? m_IconMate
            : Manager.GetNestedElement<Image>(LABEL_IMG_ICONMATE);

        #endregion

        #endregion

        private bool gotoAppearance;
        private static bool deckLiked;
        public bool callExit;
        private readonly List<Action> bookmarkCardMenuActions = new();
        private int bookmarkCardMenuCode;

        public enum ResponseRegion
        {
            Deck,
            Collection,
            Action
        }
        private ResponseRegion m_ResponseRegion;
        public ResponseRegion _ResponseRegion
        {
            get { return m_ResponseRegion; }
            set
            {
                m_ResponseRegion = value;
                ShiftToResponseRegion();
            }
        }

        private void Awake()
        {
            InitializeCardActionMenu();
            InitializeDeckView();
            InitializeCardDetailView();
            InitializeCardCollectionView();
            InitializeHeader();
            InitializeOverHeader();
        }

        private void ShiftToResponseRegion()
        {
            DeckView.SetCursor(_ResponseRegion == ResponseRegion.Deck);
            CardCollectionView.SetCursor(_ResponseRegion == ResponseRegion.Collection);
        }

        public override void ShowEvent()
        {
            base.ShowEvent();

            if (!gotoAppearance && !RoomServant.FromHandTest)
            {
                if (condition != Condition.ChangeSide)
                    ShowBackButton();
                else
                    HideBackButton();
            }
            else
            {
                gotoAppearance = false;
                RoomServant.FromHandTest = false;
                ShowBackButton();
            }

            UIManager.SetCanvasMatch(Program.instance.deckEditor.GetCanvasMatch(), 0.45f);
        }

        protected override void HideEvent()
        {
            base.HideEvent();
            UIManager.SetCanvasMatch(1f, 0.4f);

            HideBackButton();
            CardRarity.Save();
        }

        protected override void AfterHideEvent()
        {
            base.AfterHideEvent();

            if(!gotoAppearance && !RoomServant.FromHandTest)
                Dispose();
        }

        private void Dispose()
        {
            Destroy(gameObject);
            if (loadOnlineDeckCoroutine != null)
                StopCoroutine(loadOnlineDeckCoroutine);
            callExit = false;
            deckLiked = false;
        }

        #region Action Menu

        private void InitializeCardActionMenu()
        {
            CardActionMenu.SetRelatedCardEvent(() =>
            {
                CardActionMenu.blockMark = ResponseRegion.Collection;
                CardActionMenu.Hide();
                ShowRelatedCard(CardActionMenu.Card);
            });
        }

        public void ShowCardActionMenu()
        {
            if (_ResponseRegion == ResponseRegion.Deck
                && Program.instance.deckEditor.lastSelectedCardInDeck != null)
            {
                var list = new List<Card>();
                var index = 0;
                for (int i = 0; i < DeckView.cards.Count; i++)
                {
                    list.Add(DeckView.cards[i].Card);
                    if (DeckView.cards[i] == Program.instance.deckEditor.lastSelectedCardInDeck)
                        index = i;
                }
                if (list.Count == 0) return;
                CardActionMenu.Show(list, index, _ResponseRegion);
                _ResponseRegion = ResponseRegion.Action;
            }
            else if (_ResponseRegion == ResponseRegion.Collection)
            {
                if (CardCollectionView.printedCards == null
                    || CardCollectionView.printedCards.Count == 0)
                    return;
                if (Program.instance.deckEditor.lastSelectedCardInCollection == null
                    || !Program.instance.deckEditor.lastSelectedCardInCollection.selected)
                    return;
                var list = new List<Card>();
                var index = 0;
                for (int i = 0; i < CardCollectionView.printedCards.Count; i++)
                {
                    if (Program.instance.deckEditor.lastSelectedCardInCollection.card.Id == CardCollectionView.printedCards[i])
                    {
                        index = i;
                        break;
                    }
                }
                CardActionMenu.Show(CardCollectionView.printedCards, index, _ResponseRegion);
                _ResponseRegion = ResponseRegion.Action;
            }
        }

        #endregion

        #region Detail View

        private void InitializeCardDetailView()
        {
            if (CardDetailView == null)
                return;

            CardDetailView.SetRelatedCardEvent(() =>
            {
                ShowRelatedCard(CardDetailView.Card);
            });
        }

        public void ShowDetail(Card data)
        {
            if (CardDetailView != null)
                CardDetailView.ShowCard(data);
        }

        public void ShowDetail(List<int> cards, int index)
        {
            if (CardDetailView != null)
                CardDetailView.ShowCard(cards, index);
        }

        public void ChangeRarity(CardRarity.Rarity rarity)
        {
            var code = 0;
            if (_ResponseRegion == ResponseRegion.Action)
                code = CardActionMenu.Card.Id;
            else if (CardDetailView != null)
                code = CardDetailView.Card.Id;
            CardRarity.SetRarity(code, rarity);
            UpdateRarity(code);
        }

        private void UpdateRarity(int code)
        {
            if (CardDetailView != null)
                CardDetailView.RefreshRarity(code);
            if (CardActionMenu.showing)
                CardActionMenu.RefreshRarity(code);
            CardCollectionView.RefreshRarity(code);
            DeckView.RefreshRarity(code);
        }

        #endregion

        #region Deck View

        private void InitializeDeckView()
        {
            DeckView.SetNoItemButtonNavigationEvent(MoveDirection.Right, () =>
            {
                UserInput.NextSelectionIsAxis = true;
                Program.instance.deckEditor.SelectLastCollectionViewItem();
            });
            DeckView.GetInputField().onEndEdit.AddListener((string text) =>
            {
                Program.instance.deckEditor.SelectLastDeckViewItem();
            });
            var editConditon = DeckView.Condition.Editable;
            if (condition == Condition.OnlineDeck
                || condition == Condition.ReplayDeck)
                editConditon = DeckView.Condition.NonEditable;
            else if (condition == Condition.ChangeSide)
                editConditon = DeckView.Condition.ChangeSide;

            DeckView.ButtonDeck.SetClickEvent(OnDeckButtonClicked);
            if (DeckEditor.Deck == null)
                loadOnlineDeckCoroutine = StartCoroutine(LoadOnlineDeckAsync());
            else
                DeckView.PrintDeck(DeckEditor.Deck, DeckName, editConditon);
            SetDeckButtonText();
        }

        private Coroutine loadOnlineDeckCoroutine;
        private IEnumerator LoadOnlineDeckAsync()
        {
            var task = OnlineDeck.GetDeck(onlineDeckID);
            while (!task.IsCompleted)
                yield return null;
            var onlineDeckData = task.Result;
            if (onlineDeckData == null)
            {
                MessageManager.Cast(InterString.Get("Network error. Failed to retrieve the online deck."));
                yield break;
            }

            DeckName = onlineDeckData.deckName;
            DeckEditor.Deck = new Deck(onlineDeckData.deckYdk, onlineDeckData.deckContributor);
            InitializeDeckView();

            loadOnlineDeckCoroutine = null;
        }

        private void RefreshShowingCardCount()
        {
            if (CardDetailView != null)
                CardDetailView.SetCardCount();
            CardCollectionView.RefreshCardCount();
            if (_ResponseRegion == ResponseRegion.Action)
                CardActionMenu.SetCardCount();
        }

        /// <summary>
        /// Add a card through the +1 button or a middle click on CardInDeck.
        /// </summary>
        public void AddCard(Card data)
        {
            bool playAnimation = _ResponseRegion != ResponseRegion.Action;
            if (!DeckView.AddCard(data, playAnimation, playAnimation))
                return;
            AddHistoryCard(data.Id);
            RefreshShowingCardCount();
        }

        /// <summary>
        /// Add a CollectionCard to the deck by dragging it.
        /// </summary>
        /// <param name="code"></param>
        public void AddCardFromCollection(Card data)
        {
            if (!DeckView.AddCardFromPosition(data, GetDragCardPositon()))
                return;
            if (CardCollectionView.area != CardCollectionView.Area.History)
                AddHistoryCard(data.Id);
            RefreshShowingCardCount();
        }

        /// <summary>
        /// Add a CollectionCard to the deck by right-clicking it.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="position"></param>
        public void AddCardFromCollection(Card data, Vector3 position)
        {
            if (!DeckView.AddCardFromPositionWithSequence(data, position))
                return;
            if (CardCollectionView.area != CardCollectionView.Area.History)
                AddHistoryCard(data.Id);
            RefreshShowingCardCount();
        }

        /// <summary>
        /// Remove a card through the -1 button.
        /// </summary>
        /// <param name="data"></param>
        public void RemoveCard(Card data)
        {
            if (condition == Condition.ChangeSide)
                return;

            var card = DeckView.GetCardByData(data);
            if (card == null)
            {
                MessageManager.Toast(InterString.Get("Cannot delete any more cards"));
                return;
            }

            RemoveCardWithAnimation(card);
        }

        /// <summary>
        /// Called by the -1 button.
        /// Remove a CardInDeck by right-clicking it.
        /// </summary>
        /// <param name="card"></param>
        public void RemoveCardWithAnimation(SelectionButton_CardInDeck card)
        {
            bool needSelect = _ResponseRegion != ResponseRegion.Action;
            if (!DeckView.RemoveCard(card, needSelect, true, false))
                return;
            AddHistoryCard(card.Card.Id);

            AudioManager.PlaySE("SE_DECK_MINUS");

            card.transform.SetParent(transform, true);
            card.transform.localScale = new Vector3(2f, 2f, 1f);
            var cg = card.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            RefreshShowingCardCount();

            if (needSelect)
            {
                var endPostion = CardCollectionView.GetRubbishBinPositon();
                endPostion.z -= 1f;
                var startPostion = card.transform.position;
                startPostion.z = endPostion.z;
                card.transform.position = startPostion;

                DOTween.Sequence()
                    .Append(card.transform.DOMove(endPostion, 0.4f).SetEase(Ease.OutCubic))
                    .Append(card.transform.DOScale(1f, 0.2f).SetEase(Ease.InCubic))
                    .Join(cg.DOFade(0f, 0.2f))
                    .OnComplete(() =>
                    {
                        Destroy(card.gameObject);
                    });
            }
            else
            {
                Destroy(card.gameObject);
            }
        }

        /// <summary>
        /// Remove a card by dragging it into the DropArea.
        /// </summary>
        /// <param name="card"></param>
        public void RemoveCardByDrag(SelectionButton_CardInDeck card)
        {
            if (!DeckView.RemoveCard(card, false, true, true))
                return;
            AddHistoryCard(card.Card.Id);
            PlayDragCardShrinkAnimation();
        }

        public void DragCardTo(SelectionButton_CardInDeck dragCard)
        {
            if (!DeckView.deckLoaded) return;
            if (condition != Condition.ChangeSide
            && !DeckView.CanEditCard()) return;

            var position = GetDragCardPositon();
            var hoverCard = DeckView.GetHoveringCard();

            var location = DeckView.DeckLocation.All;
            if (hoverCard == null)
            {
                if (UIHover.HoveringLabel == UIHover.LABEL_REMOVEDECK)
                {
                    RemoveCardByDrag(dragCard);
                    return;
                }
                else if (UIHover.HoveringLabel == UIHover.LABEL_ADDBOOKMARK)
                {
                    BookmarkCard(dragCard.Card.Id);
                    PlayDragCardShrinkAnimation();
                    return;
                }
                else if (UIHover.HoveringLabel == UIHover.LABEL_CANNOTADDBOOKMARK)
                {
                    if (TryRemoveCardFromBook(dragCard.Card.Id))
                        PlayDragCardShrinkAnimation();
                    return;
                }
                else if (UIHover.HoveringLabel == UIHover.LABEL_MAINDECK)
                    location = DeckView.DeckLocation.MainDeck;
                else if (UIHover.HoveringLabel == UIHover.LABEL_EXTRADECK)
                    location = DeckView.DeckLocation.ExtraDeck;
                else if (UIHover.HoveringLabel == UIHover.LABEL_SIDEDECK)
                    location = DeckView.DeckLocation.SideDeck;
                else
                {
                    dragCard.MoveToParent(position);
                    return;
                }
            }
            else
                location = hoverCard.location;

            if (!DeckView.CanSwitchPosition(dragCard.Card, location))
            {
                dragCard.MoveToParent(position);
                return;
            }

            if (hoverCard == null)
            {
                if (dragCard.location == location)
                    dragCard.MoveToParent(position);
                else
                    DeckView.MoveCardToLocation(dragCard, location, GetDragCardPositon());
            }
            else
                DeckView.MoveCardToLocationWithSiblingIndex(dragCard, location
                    , hoverCard.transform.GetSiblingIndex(), GetDragCardPositon());
        }

        public void CardChangeSide(SelectionButton_CardInDeck card)
        {
            var location = DeckView.DeckLocation.SideDeck;
            if (card.location == DeckView.DeckLocation.SideDeck)
                location = card.Card.IsExtraCard() ? DeckView.DeckLocation.ExtraDeck : DeckView.DeckLocation.MainDeck;
            DeckView.MoveCardToLocation(card, location, card.transform.position);
        }

        public Vector3 GetDragCardPositon()
        {
            return DragCard.position;
        }

        public void OnDeckButtonClicked()
        {
            SetDeckButtonText();
        }

        private void SetDeckButtonText()
        {
            DeckView.ButtonDeck.SetButtonText(string.Empty);
            DeckView.ButtonDeck.gameObject.SetActive(false);
        }


        #endregion

        #region Card Collection View

        private void InitializeCardCollectionView()
        {
            CardCollectionView.historyCards = historyCards;
            CardCollectionView.SetNoItemButtonNavigationEvent(MoveDirection.Left, () =>
            {
                UserInput.NextSelectionIsAxis = true;
                Program.instance.deckEditor.SelectLastDeckViewItem();
            });
        }

        public void BookmarkCard(int code)
        {
            CardRarity.BookmarkCard(code);
            RefreshBookmarkViews();
        }

        public void UnbookmarkCard(int code)
        {
            CardRarity.UnbookmarkCard(code);
            RefreshBookmarkViews();
        }

        public bool TryRemoveCardFromBook(int code)
        {
            if (!CardCollectionView.TryRemoveCardFromBook(code))
                return false;

            RefreshBookmarkViews();
            return true;
        }

        public void ShowBookmarkListSelection(int code)
        {
            bookmarkCardMenuCode = code;
            bookmarkCardMenuActions.Clear();

            var selections = new List<string>
            {
                InterString.Get("Bookmark Lists"),
                InterString.Get("Choose a list to add to or remove from")
            };

            var added = CardRarity.CardInBookmarkList(code, string.Empty);
            AddBookmarkCardMenuEntry(
                selections,
                () => ToggleCardBookmarkList(string.Empty),
                InterString.Get("Default Bookmark List"),
                added ? "g" : string.Empty);

            foreach (var listName in CardRarity.GetBookmarkListNames())
            {
                var capturedName = listName;
                var added2 = CardRarity.CardInBookmarkList(code, capturedName);
                AddBookmarkCardMenuEntry(
                    selections,
                    () => ToggleCardBookmarkList(capturedName),
                    capturedName,
                    added2 ? "g" : string.Empty);
            }

            AddBookmarkCardMenuEntry(selections, CreateBookmarkListForCard, InterString.Get("Create New List and Add"), "b");
            UIManager.ShowPopupSelection(selections, OnBookmarkCardMenuSelection);
        }

        private void AddBookmarkCardMenuEntry(List<string> selections, Action action, string label, string color = "")
        {
            bookmarkCardMenuActions.Add(action);
            selections.Add(string.IsNullOrEmpty(color) ? label : $"{color}:{label}");
        }

        private void OnBookmarkCardMenuSelection()
        {
            if (EventSystem.current.currentSelectedGameObject == null)
                return;

            var index = EventSystem.current.currentSelectedGameObject.transform.GetSiblingIndex();
            if (index < 0 || index >= bookmarkCardMenuActions.Count)
                return;

            bookmarkCardMenuActions[index]?.Invoke();
        }

        private void ToggleCardBookmarkList(string listName)
        {
            var result = CardRarity.ToggleCardTo(bookmarkCardMenuCode, listName);
            if(result == 1)
                MessageManager.Toast(InterString.Get("Added to bookmark list \"[?]\".", listName));
            else if(result == 2)
                MessageManager.Toast(InterString.Get("Removed from bookmark list \"[?]\".", listName));

            RefreshBookmarkViews();
        }

        private void CreateBookmarkListForCard()
        {
            var selections = new List<string>
            {
                InterString.Get("Create Bookmark List"),
                string.Empty
            };
            UIManager.ShowPopupInput(selections, CreateBookmarkListForCard, null, TmpInputValidation.ValidationType.None);
        }

        private void CreateBookmarkListForCard(string listName)
        {
            if (!TryNormalizeBookmarkListName(listName, out var normalized))
                return;
            if (!CardRarity.AddBookmarkList(normalized))
            {
                MessageManager.Cast(InterString.Get("This favorites list already exists!"));
                return;
            }

            CardRarity.ToggleCardTo(bookmarkCardMenuCode, normalized);
            if (CardCollectionView.area == CardCollectionView.Area.Bookmark)
                CardCollectionView.ShowBookmarkList(normalized);
            RefreshBookmarkViews();
        }

        private bool TryNormalizeBookmarkListName(string listName, out string normalized)
        {
            normalized = listName == null ? string.Empty : listName.Trim();
            if (normalized == string.Empty)
            {
                MessageManager.Cast(InterString.Get("The favorites list name cannot be empty!"));
                return false;
            }
            if (normalized.Contains(":"))
            {
                MessageManager.Cast(InterString.Get("The favorites list name cannot contain \":\"!"));
                return false;
            }
            if (normalized.Contains("\n") || normalized.Contains("\r"))
            {
                MessageManager.Cast(InterString.Get("The favorites list name cannot contain line breaks!"));
                return false;
            }
            if (CardRarity.BookmarkListExists(normalized))
            {
                MessageManager.Cast(InterString.Get("This favorites list already exists!"));
                return false;
            }

            return true;
        }

        private void RefreshBookmarkViews()
        {
            CardCollectionView.RefreshBookmarkTab();
            if (CardDetailView != null)
                CardDetailView.RefreshBookmarkToggle();
            if (_ResponseRegion == ResponseRegion.Action)
                CardActionMenu.RefreshBookmarkToggle();
            if (CardCollectionView.area == CardCollectionView.Area.Bookmark)
                CardCollectionView.PrintBookmarkCards();
        }

        public void AddHistoryCard(int code)
        {
            if (condition == Condition.ChangeSide)
                return;
            CardCollectionView.AddHistoryCard(code);
        }

        public void AddHistoryCards(List<int> codes)
        {
            if (condition == Condition.ChangeSide)
                return;
            CardCollectionView.AddHistoryCards(codes);
        }

        public bool NeedAddCardToHistoryWhenClick()
        {
            return CardCollectionView.area != CardCollectionView.Area.History;
        }

        public void ShowRelatedCard(Card data)
        {
            CardCollectionView.ShowRelatedCard(data);
        }

        public void HideRelatedCard()
        {
            CardCollectionView.HideRelatedCard();
        }

        #endregion

        #region Header

        private void InitializeHeader()
        {
            DeckView.MainDeckCountChanged += RefreshDeckOverviewCaption;
            RefreshDeckOverviewCaption();

            if (condition == Condition.ChangeSide)
            {
                Destroy(ButtonTest.gameObject);
                Destroy(ButtonSave.gameObject);
            }
            else
            {
                Destroy(ButtonChangeSide.gameObject);
                ButtonTest.SetButtonText("Test Deck");
                BattleNativeLabels.Apply(gameObject);
            }
        }

        private void RefreshDeckOverviewCaption()
        {
            int missing = DeckValidator.MinimumCards - DeckView.mainCount;
            ButtonRegulation.SetButtonText(DeckView.deckLoaded && missing > 0
                ? "Deck · " + missing + " short"
                : "Deck overview");
        }

        protected override void OnDestroy()
        {
            if (m_DeckView != null)
                m_DeckView.MainDeckCountChanged -= RefreshDeckOverviewCaption;
            base.OnDestroy();
        }

        public void SetCardInfoType()
        {
            var type = cardInfoType == CardInfoType.None ? CardInfoType.Detail : CardInfoType.None;
            Program.instance.deckEditor.SetCardInfoType(type);
        }

        public void SetCardInfoType(CardInfoType type)
        {
            AudioManager.PlaySE("SE_MENU_SELECT_01");
            switch (cardInfoType)
            {
                case CardInfoType.None:
                    MessageManager.Toast(InterString.Get("Switch to Simple Display"));
                    break;
                case CardInfoType.Detail:
                    MessageManager.Toast(InterString.Get("Switch to Detail Display"));
                    break;
                case CardInfoType.Pool:
                    MessageManager.Toast(InterString.Get("Switch to Card Pool Display"));
                    break;
                case CardInfoType.Genesys:
                    MessageManager.Toast(InterString.Get("Switch to Genesys Points Display"));
                    break;
            }
            DeckView.SetCardInfoType(type);
            CardCollectionView.SetCardInfoType();
            if (CardDetailView != null)
                CardDetailView.RefreshRegulation();
            RefreshDeckOverviewCaption();
        }

        private void RefreshRegulationIcons()
        {
            foreach (var card in DeckView.cards)
            {
                card.SetRegulationIcon();
                card.RefreshCredit();
            }
            foreach (var go in CardCollectionView.superScrollView.gameObjects)
            {
                var mono = go.GetComponent<SelectionButton_CardInCollection>();
                mono.SetRegulationIcon();
                mono.RefreshCredit();
            }
            if (CardDetailView != null)
                CardDetailView.RefreshRegulation();
        }

        public void OnRegulation()
        {
            string rules = "A deck contains at least " + DeckValidator.MinimumCards + " cards. Cards sharing a name are limited to 3 copies. Only verified Standard cards are legal. Extra and Side stay empty.";
            if (!DeckView.deckLoaded)
            {
                UIManager.ShowPopupConfirm(new List<string> { "Battle Spirits Standard", rules });
                return;
            }

            var costs = new SortedDictionary<int, int>();
            int total = 0, spirits = 0, nexuses = 0, magic = 0;
            foreach (var card in DeckView.cards)
            {
                if (card.location != DeckView.DeckLocation.MainDeck)
                    continue;
                total++;
                if (!BattleCatalog.TryGet(card.Card.Id, out var definition))
                    continue;
                costs.TryGetValue(definition.cost, out int count);
                costs[definition.cost] = count + 1;
                if (definition.IsSpirit) spirits++;
                if (definition.IsNexus) nexuses++;
                if (definition.IsMagic) magic++;
            }

            var lines = new List<string> { total + " cards", "Spirits " + spirits + "  ·  Nexuses " + nexuses + "  ·  Magic " + magic };
            int missing = DeckValidator.MinimumCards - total;
            if (missing > 0)
                lines.Insert(1, "Add " + missing + (missing == 1 ? " card" : " cards") + " to reach the minimum.");
            if (costs.Count > 0)
            {
                lines.Add("\nCost distribution");
                foreach (var row in costs)
                    lines.Add("Cost " + row.Key + "   •   " + row.Value);
            }
            lines.Add("\n" + rules);
            UIManager.ShowPopupConfirm(new List<string> { "Standard deck overview", string.Join("\n", lines), "Close" });
        }

        private void ChangeRegulation()
        {
            string selected = EventSystem.current.currentSelectedGameObject
                .GetComponent<SelectionButton>().GetButtonText();
            if(cardInfoType == CardInfoType.Genesys)
                BanlistManager.currentGenesysBanList = BanlistManager.GetByName(selected);
            else
                BanlistManager.currentBanList = BanlistManager.GetByName(selected);
            ButtonRegulation.SetButtonText(selected);
            RefreshRegulationIcons();
        }

        public void OnSubMenu()
        {
            if (!DeckView.deckLoaded) return;
            UIManager.ShowSubMenu(new List<string> { "Deck", "Reset", "Shuffle", "Copy deck", "Share", "Clear deck" },
                new List<Action> { null, OnReset, OnRandom, OnCopy, OnShare, OnClearDeck });
        }

        public void OnSave()
        {
            if (!DeckView.deckLoaded) return;
            var validation = BattleDeckStorage.Validate(DeckView.FromObjectDeckToCodedDeck());
            if (!validation.IsValid)
            {
                UIManager.ShowPopupConfirm(new List<string> { "Invalid Battle Spirits Deck", validation.Summary });
                callExit = false;
                return;
            }
            OnSaveConfirmed();
        }

        private void OnSaveConfirmed()
        {
            if (!DeckView.Save())
                return;

            if (callExit)
            {
                CG.blocksRaycasts = false;
                Program.instance.deckEditor.CallExitIn(2f);
                return;
            }
            DeckIsFromLocal = true;
            SetDeckButtonText();
        }

        public void OnReset()
        {
            DeckView.ResetDeck();
        }

        public void OnSort()
        {
            DeckView.Sort();
        }

        public void OnRandom()
        {
            DeckView.Randomize();
        }

        public void OnCopy()
        {
            DeckView.Copy();
        }

        public void OnShare()
        {
            DeckView.Share();
        }

        public void OnHandTest()
        {
            TryStartHandTest(HandTestSettings.Load());
        }

        private void OnHandTestSettings()
        {
            UIManager.ShowPopupConfirm(new List<string> { "Battle Spirits Test Deck", "Test Deck starts a Battle Spirits match against a local opponent. Each player draws four opening cards and follows the Standard turn sequence." });
        }

        private void OnYdke()
        {
            UIManager.ShowPopupYdke(OnImportYdke, OnExportYdke);
        }

        private void OnImportYdke()
        {
            var importedDeck = YdkeConverter.Ydke2Deck(GUIUtility.systemCopyBuffer);
            if (importedDeck == null)
                return;

            DeckView.ImportCardLists(importedDeck);
        }

        private void OnExportYdke()
        {
            if (!DeckView.deckLoaded)
                return;

            var ydke = YdkeConverter.DeckToYdke(DeckView.FromObjectDeckToCodedDeck());
            if (string.IsNullOrEmpty(ydke))
                return;

            GUIUtility.systemCopyBuffer = ydke;
            MessageManager.Toast(StringHelper.Get(1480));
        }

        private bool TryStartHandTest(HandTestSettings settings)
        {
            if (!DeckView.deckLoaded) return false;
            var validation = BattleDeckStorage.Validate(DeckView.FromObjectDeckToCodedDeck());
            if (!validation.IsValid)
            {
                UIManager.ShowPopupConfirm(new List<string> { "Invalid Battle Spirits Deck", validation.Summary });
                return false;
            }
            if (!DeckView.Save()) return false;
            return BattleIntegration.StartSolo(DeckName);
        }

        private async UniTask HandTestAsync(HandTestSettings settings)
        {
            await UniTask.CompletedTask;
            TryStartHandTest(settings);
        }

        private void OnClearDeck()
        {
            var codes = new List<int>();
            foreach (var card in DeckView.cards)
                codes.Add(card.Card.Id);
            if (!DeckView.ClearDeck())
                return;
            AudioManager.PlaySE("SE_DECK_MINUS");
            AddHistoryCards(codes);
            RefreshShowingCardCount();
        }

        public void OnChangeSideComplete()
        {
            OnSave();
        }

        private void ShowBackButton()
        {
            ButtonBack.gameObject.SetActive(true);

            RectBack.anchoredPosition3D = new Vector3(24f, 120f, 0f);
            DOTween.Sequence()
                .AppendInterval(0.6f)
                .Append(RectBack.DOAnchorPos3D(new Vector3(24f, 0f, 0f), 0.2f).SetEase(Ease.OutQuart));
        }

        private void HideBackButton()
        {
            ButtonBack.gameObject.SetActive(false);
        }

        #endregion

        #region Over Header

        private void InitializeOverHeader()
        {
            if (condition == Condition.ChangeSide)
                ButtonAppearance.gameObject.SetActive(false);
            else
            {
                _ = RefreshOverHeaderIconsAsync();
            }
        }

        private async UniTask RefreshOverHeaderIconsAsync()
        {
            IconCase.sprite = await BattleCardAdapter.BackIconAsync(destroyCancellationToken);
            IconCase.color = Color.white;
            IconProtector.sprite = await BattleCardAdapter.BackIconAsync(destroyCancellationToken);
            IconProtector.material = null;
            IconProtector.color = Color.white;
            IconField.sprite = BattleCosmetics.LoadIcon((DeckEditor.Deck?.Field ?? BattleCosmetics.DefaultFieldId).ToString(), Items.ItemType.Mat);
            IconField.color = IconField.sprite != null ? Color.white : Color.clear;
            IconGrave.color = Color.clear;
            IconStand.color = Color.clear;
            IconMate.color = Color.clear;
        }

        public void ShiftToAppearance()
        {
            if (!DeckView.deckLoaded || condition == Condition.ChangeSide) return;
            if (!DeckIsFromLocal)
            {
                MessageManager.Toast(InterString.Get("Please save the deck first"));
                return;
            }
            gotoAppearance = true;
            Program.instance.appearance.SwitchCondition(Appearance.Condition.DeckEditor);
            Program.instance.ShiftToServant(Program.instance.appearance);
        }

        #endregion

        private void PlayDragCardShrinkAnimation()
        {
            DragCard.gameObject.SetActive(true);
            DragCard.localScale = Vector3.one;
            DragCard.DOScale(0.5f, 0.2f).SetEase(Ease.InCubic);
            DragCardImage.DOFade(0.5f, 0.2f).SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    DragCard.localScale = Vector3.one;
                    DragCardImage.color = Color.white;
                    DragCard.gameObject.SetActive(false);
                });
        }

    }
}
