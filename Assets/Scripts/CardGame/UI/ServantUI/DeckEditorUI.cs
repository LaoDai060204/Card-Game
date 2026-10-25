using Cysharp.Threading.Tasks;
using DG.Tweening;
using CardGame.Duel.YGOSharp;
using CardGame.Net;
using CardGame.Servant;
using CardGame.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CardGame.Servant.DeckEditor;

namespace CardGame.UI.ServantUI
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
            if (!DeckView.deckLoaded) return;

            if (!DeckView.ButtonDeck.gameObject.activeSelf)
                return;

            if (!DeckIsFromLocal && condition == Condition.OnlineDeck)
            {
                OnlineDeck.LikeDeck(onlineDeckID);
                deckLiked = true;
                SetDeckButtonText();
                return;
            }

            if (DeckView.GetDirty() || !DeckIsFromLocal)
            {
                if (condition != Condition.ChangeSide)
                    MessageManager.Toast(InterString.Get("Please save the deck first"));
                return;
            }

            if (MyCard.account != null && Config.GetBool("SyncDeck", true))
            {
                var onlineDeck = OnlineDeck.GetByID(DeckEditor.Deck.deckId);
                if (onlineDeck == null || onlineDeck.isDelete)
                    return;
                _ = OnlineDeck.UpdatePublicState(DeckEditor.Deck.deckId, !onlineDeck.isPublic);
                onlineDeck.isPublic = !onlineDeck.isPublic;
            }

            SetDeckButtonText();
        }

        private void SetDeckButtonText()
        {
            string text = string.Empty;
            if (DeckIsFromLocal)
            {
                if (MyCard.account != null)
                {
                    if(Config.GetBool("SyncDeck", true))
                    {
                        var onlineDeck = OnlineDeck.GetByID(DeckEditor.Deck.deckId);
                        if (onlineDeck == null || onlineDeck.isDelete)
                        {
                            DeckView.ButtonDeck.gameObject.SetActive(false);
                            return;
                        }
                        else
                        {
                            if (onlineDeck.isPublic)
                                text = InterString.Get("Public");
                            else
                                text = InterString.Get("Private");
                        }
                    }
                    else
                    {
                        text = InterString.Get("Stopping sync");
                    }
                }
            }
            else
            {
                if (condition == Condition.OnlineDeck)
                {
                    text = InterString.Get("Like");
                    if (deckLiked)
                    {
                        DeckView.ButtonDeck.gameObject.SetActive(false);
                        return;
                    }
                }
            }
            DeckView.ButtonDeck.SetButtonText(text);
            DeckView.ButtonDeck.gameObject.SetActive(text != string.Empty);
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
            ButtonRegulation.SetButtonText(DeckEditor.Banlist.Name);

            if (condition == Condition.ChangeSide)
            {
                Destroy(ButtonTest.gameObject);
                Destroy(ButtonSave.gameObject);
            }
            else
            {
                Destroy(ButtonChangeSide.gameObject);
                ButtonTest.SetButtonText(InterString.Get("Test Hand"));
            }
        }

        public void SetCardInfoType()
        {
            var type = (CardInfoType)(((int)cardInfoType + 1) % 4);
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
            ButtonRegulation.SetButtonText(DeckEditor.Banlist.Name);
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
            AudioManager.PlaySE("SE_MENU_DECIDE");
            List<string> selections = new()
            {
                InterString.Get("Forbidden & Limited Lists"),
                string.Empty
            };
            var names = cardInfoType == CardInfoType.Genesys ? 
                BanlistManager.GetAllGenesysNames() : BanlistManager.GetAllNames();
            selections.AddRange(names);
            UIManager.ShowPopupSelection(selections, ChangeRegulation);
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
            var menus = new List<string>()
                {
                    InterString.Get("Submenu")
                };
            var actions = new List<Action>()
                {
                    null
                };

            if (condition != Condition.ChangeSide)
            {
                menus.Add(InterString.Get("Hand Test Settings"));
                actions.Add(OnHandTestSettings);
                menus.Add("YDKE");
                actions.Add(OnYdke);
            }

            menus.AddRange(new List<string>
            {
                InterString.Get("Reset"),
                InterString.Get("Shuffle")
            });
            actions.AddRange(new List<Action>()
            {
                OnReset,
                OnRandom
            });

            if (condition != Condition.ChangeSide)
            {
                menus.AddRange(new List<string>
                {
                    InterString.Get("Copy deck"),
                    InterString.Get("Share"),
                    InterString.Get("Clear deck")
                });
                actions.AddRange(new List<Action>()
                {
                    OnCopy,
                    OnShare,
                    OnClearDeck
                });
            }
            UIManager.ShowSubMenu(menus, actions);
        }

        public void OnSave()
        {
            if (DeckIsFromLocal && !DeckView.GetDirty()) return;

            if (DeckIsFromLocal)
                if (DeckEditor.Banlist.Name != BanlistManager.EMPTY_LIST_NAME)
                {
                    if (DeckView.mainCount > 60 || DeckView.extraCount > 15 || DeckView.sideCount > 15)
                    {
                        List<string> tasks = new()
                        {
                            InterString.Get("Save Failed"),
                            InterString.Get("Your deck contains cards that exceed the limit.@nTo ignore restrictions, set Forbidden & Limited Lists to N/A.")
                        };
                        UIManager.ShowPopupConfirm(tasks);
                        callExit = false;
                        return;
                    }
                }

            if (!DeckIsFromLocal && File.Exists(Program.PATH_DECK + DeckName + Program.EXPANSION_YDK))
            {
                List<string> tasks = new()
                {
                    InterString.Get("Deck name already exists"),
                    InterString.Get("A file with this deck name already exists. Overwrite it?"),
                    InterString.Get("Overwrite"),
                    InterString.Get("Cancel")
                };

                UIManager.ShowPopupYesOrNo(tasks, OnSaveConfirmed, () => { callExit = false; });
            }
            else
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
            UIManager.ShowPopupHandTestSettings(HandTestSettings.Load(), TryStartHandTest);
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
            if (!DeckView.deckLoaded)
                return false;
            if (condition == Condition.ChangeSide)
                return false;
            if (!DeckIsFromLocal)
            {
                MessageManager.Toast(InterString.Get("Please save the deck first"));
                return false;
            }

            _ = HandTestAsync(settings);
            return true;
        }

        private async UniTask HandTestAsync(HandTestSettings settings)
        {
            var launchSettings = settings != null ? settings.Clone() : HandTestSettings.Load();
            launchSettings.Clamp();

            int port = 7911;
            while (!TcpHelper.IsPortAvailable(port))
            {
                port++;
                if (port == 65536)
                    port = 1;
            }

            string args = launchSettings.BuildServerArgs(port);

            RoomServant.FromSolo = false;
            RoomServant.FromLocalHost = false;
            RoomServant.FromHandTest = true;
            YgoServer.StartServer(args);

            UIManager.UIBlackIn(0.3f);
            await UniTask.WaitForSeconds(0.3f);

            OcgCore.handler = Program.instance.room.Handler;

            Program.instance.solo.StartAIForHandTest(port);
            await UniTask.Delay(100);
            bool joined = false;
            TcpHelper.LinkStart("127.0.0.1", Config.Get("DuelPlayerName0", Config.EMPTY_STRING), port.ToString(), string.Empty, true, () => joined = true);
            await UniTask.WaitUntil(() => joined);

            var deck = DeckView.FromObjectDeckToCodedDeck();
            TcpHelper.CtosMessage_UpdateDeck(deck);
            TcpHelper.CtosMessage_HsReady();
            await UniTask.Delay(50);
            TcpHelper.CtosMessage_HandResult(2);
            await UniTask.Delay(50);
            TcpHelper.CtosMessage_TpResult(true);

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
            OcgCore.sideChangedDeck = DeckView.FromObjectDeckToCodedDeck();
            TcpHelper.CtosMessage_UpdateDeck(OcgCore.sideChangedDeck);
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
            IconCase.color = Color.clear;
            IconProtector.color = Color.clear;
            IconField.color = Color.clear;
            IconGrave.color = Color.clear;
            IconStand.color = Color.clear;
            IconMate.color = Color.clear;

            while (DeckEditor.Deck == null)
                await TaskUtility.WaitOneFrame(gameObject);

            var sprite = await Program.items.LoadDeckCaseIconAsync(DeckEditor.Deck.Case, "_L_SD");
            if (sprite != null)
            {
                IconCase.color = Color.white;
                IconCase.sprite = sprite;
            }

            IconProtector.material = await ABLoader.LoadProtectorMaterial(DeckEditor.Deck.Protector.ToString(), Application.exitCancellationToken);
            IconProtector.color = Color.white;

            IconField.sprite = await Program.items.LoadItemIconAsync(DeckEditor.Deck.Field.ToString(), Items.ItemType.Mat);
            IconField.color = Color.white;

            IconGrave.sprite = await Program.items.LoadItemIconAsync(DeckEditor.Deck.Grave.ToString(), Items.ItemType.Grave);
            IconGrave.color = Color.white;

            IconStand.sprite = await Program.items.LoadItemIconAsync(DeckEditor.Deck.Stand.ToString(), Items.ItemType.Stand);
            IconStand.color = Color.white;

            var mate = DeckEditor.Deck.Mate.ToString();
            if (mate.Length == 7 && mate.StartsWith("100"))
            {
                IconMate.sprite = await Program.items.LoadItemIconAsync(mate, Items.ItemType.Mate);
                IconMate.color = Color.white;
            }
            else
            {
                var art = await CardImageLoader.LoadArtAsync(DeckEditor.Deck.Mate, true);
                IconMate.sprite = TextureManager.Texture2Sprite(art);
                IconMate.color = Color.white;
            }
        }

        public void ShiftToAppearance()
        {
            if (!DeckView.deckLoaded)
                return;
            if (condition == Condition.ChangeSide)
                return;
            if (!DeckIsFromLocal)
            {
                if (condition != Condition.ChangeSide)
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
