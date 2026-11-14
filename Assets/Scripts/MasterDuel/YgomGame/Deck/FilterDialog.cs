using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using YgomGame.Card;
using YgomGame.Menu;
using YgomSystem.ElementSystem;
using YgomSystem.UI;
using YgomSystem.Utility;
using YgomSystem.YGomTMPro;

namespace YgomGame.Deck
{
	public class FilterDialog : SelectDialogViewControllerBase<SearchFilter.Setting, SearchFilter.Setting, List<FilterDialog.FilterGroupType>, SearchFilter.Setting>, IBokeSupported
	{
		public enum FilterGroupType
		{
			Frame = 0,
			Attribute = 1,
			Tribe = 2,
			Level = 3,
			Spell = 4,
			Trap = 5,
			Rarity = 6,
			Ability = 7,
			Style = 8,
			Limit = 9,
			Cutin = 10
		}

		private class FilterGroup : MonoBehaviour
		{
			private const string k_ELabelIcon = "Icon";

			private const string k_ELabelText = "TextTMP";

			private const string k_ELabelButtonArea = "ButtonArea";

			private ElementObjectManager m_Eom;

			private Image m_GroupIconImage;

			private ExtendedTextMeshProUGUI m_GroupNameText;

			private GridLayoutGroup m_GridLayoutGroup;

			private bool isInitialized;

			private FilterGroupType m_Type;

			private Dictionary<string, FilterToggle> m_FilterToggles;

			public RectTransform m_ButtonArea
			{
				[CompilerGenerated]
				get
				{
					return null;
				}
				[CompilerGenerated]
				private set
				{
				}
			}

			public RectTransform m_RectTransform
			{
				[CompilerGenerated]
				get
				{
					return null;
				}
				[CompilerGenerated]
				private set
				{
				}
			}

			public void InitializeElements()
			{
			}

			private void Awake()
			{
			}

			public void Initialize(FilterGroupType type)
			{
			}

			public void AddFilterToggle(string label, FilterToggle filterToggle)
			{
			}
		}

		[SerializeField]
		private KeyConfigContainer keyConfig;

		private const string PREFAB_PATH_FILTERDIALOG = "DeckEdit/FilterDialog";

		private const string k_ELabelFilterMenuArea = "FilterMenuArea";

		private const string k_ELabelFilterScroll = "FilterScroll";

		private const string k_ELabelFooterArea = "FooterArea";

		private const string k_ELabelTitleText = "TitleText";

		private ElementObjectManager m_FilterMenuEom;

		private ElementObjectManager m_FooterEom;

		private ExtendedTextMeshProUGUI m_TitleText;

		private const string k_ALabelFilterToggleTemplate = "FilterToggleTemplate";

		private GameObject m_FilterToggleTemplate;

		private (SelectorManager.KeyType, SelectorManager.KeyType) keyInfoUpGroup;

		private (SelectorManager.KeyType, SelectorManager.KeyType) keyInfoDownGroup;

		private Coroutine m_InitializeCoroutine;

		private const string Label_Frame_Normal = "Frame Normal";

		private const string Label_Frame_Effect = "Frame Effect";

		private const string Label_Frame_Fusion = "Frame Fusion";

		private const string Label_Frame_Ritual = "Frame Ritual";

		private const string Label_Frame_Synchro = "Frame Synchro";

		private const string Label_Frame_Xyz = "Frame Xyz";

		private const string Label_Frame_Pendulum = "Frame Pendulum";

		private const string Label_Frame_Link = "Frame Link";

		private const string Label_Frame_Magic = "Frame Magic";

		private const string Label_Frame_Trap = "Frame Trap";

		private const string Label_Attr_Light = "Attr Light";

		private const string Label_Attr_Dark = "Attr Dark";

		private const string Label_Attr_Water = "Attr Water";

		private const string Label_Attr_Fire = "Attr Fire";

		private const string Label_Attr_Earth = "Attr Earth";

		private const string Label_Attr_Wind = "Attr Wind";

		private const string Label_Attr_Divine = "Attr Divine";

		private const string Label_Tribe_SpellCaster = "Tribe Spell Caster";

		private const string Label_Tribe_Dragon = "Tribe Dragon";

		private const string Label_Tribe_Zombie = "Tribe Zombie";

		private const string Label_Tribe_Warrior = "Tribe Warrior";

		private const string Label_Tribe_BeastWarrior = "Tribe Beast Warrior";

		private const string Label_Tribe_Beast = "Tribe Beast";

		private const string Label_Tribe_WingedBeast = "Tribe Winged Beast";

		private const string Label_Tribe_Machine = "Tribe Machine";

		private const string Label_Tribe_Fiend = "Tribe Fiend";

		private const string Label_Tribe_Fairy = "Tribe Fairy";

		private const string Label_Tribe_Insect = "Tribe Insect";

		private const string Label_Tribe_Dinosaur = "Tribe Dinosaur";

		private const string Label_Tribe_Reptile = "Tribe Reptile";

		private const string Label_Tribe_Fish = "Tribe Fish";

		private const string Label_Tribe_SeaSerpent = "Tribe Sea Serpent";

		private const string Label_Tribe_Aqua = "Tribe Aqua";

		private const string Label_Tribe_Pyro = "Tribe Pyro";

		private const string Label_Tribe_Thunder = "Tribe Thunder";

		private const string Label_Tribe_Rock = "Tribe Rock";

		private const string Label_Tribe_Plant = "Tribe Plant";

		private const string Label_Tribe_Psychic = "Tribe Psychic";

		private const string Label_Tribe_Wyrm = "Tribe Wyrm";

		private const string Label_Tribe_Cyberse = "Tribe Cyberse";

		private const string Label_Tribe_DivineBeast = "Tribe Divine Beast";

		private const string Label_Lvl0 = "Lvl0";

		private const string Label_Lvl1 = "Lvl1";

		private const string Label_Lvl2 = "Lvl2";

		private const string Label_Lvl3 = "Lvl3";

		private const string Label_Lvl4 = "Lvl4";

		private const string Label_Lvl5 = "Lvl5";

		private const string Label_Lvl6 = "Lvl6";

		private const string Label_Lvl7 = "Lvl7";

		private const string Label_Lvl8 = "Lvl8";

		private const string Label_Lvl9 = "Lvl9";

		private const string Label_Lvl10 = "Lvl10";

		private const string Label_Lvl11 = "Lvl11";

		private const string Label_Lvl12 = "Lvl12";

		private const string Label_Lvl13 = "Lvl13";

		private const string Label_Spell_Normal = "Spell Normal";

		private const string Label_Spell_Field = "Spell Field";

		private const string Label_Spell_Equip = "Spell Equip";

		private const string Label_Spell_Continuous = "Spell Continuous";

		private const string Label_Spell_QuickPlay = "Spell Quick Play";

		private const string Label_Spell_Ritual = "Spell Ritual";

		private const string Label_Trap_Normal = "Trap Normal";

		private const string Label_Trap_Continuous = "Trap Continuous";

		private const string Label_Trap_Counter = "Trap Counter";

		private const string Label_Rarity_Normal = "Rarity Normal";

		private const string Label_Rarity_Rare = "Rarity Rare";

		private const string Label_Rarity_SuperRare = "Rarity Super Rare";

		private const string Label_Rarity_UltraRare = "Rarity Ultra Rare";

		private const string Label_Style_Normal = "Style Normal";

		private const string Label_Style_Shine = "Style Shine";

		private const string Label_Style_Royal = "Style Royal";

		private const string Label_Limit_0 = "Limit 0";

		private const string Label_Limit_1 = "Limit 1";

		private const string Label_Limit_2 = "Limit 2";

		private const string Label_Limit_3 = "Limit 3";

		private const string Label_Cutin_Exist = "Cutin Exist";

		private const string Label_Cutin_NotExist = "Cutin Not Exist";

		private const string Label_Ability_Toon = "Ability Toon";

		private const string Label_Ability_Dual = "Ability Dual";

		private const string Label_Ability_Union = "Ability Union";

		private const string Label_Ability_Spirit = "Ability Spirit";

		private const string Label_Ability_Tuner = "Ability Tuner";

		private const string Label_Ability_Reverse = "Ability Reverse";

		private const string Label_Ability_SpSummon = "Ability Sp Summon";

		private SearchFilter.Setting m_setting;

		private static Dictionary<string, SearchFilter.Setting.FRAME> frameSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.ATTR> attrSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.TRIBE> tribeSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.LEVEL> lvlSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.NOTMONSTER> spellSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.NOTMONSTER> trapSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.RARITY> raritySettingTbl;

		private static Dictionary<string, SearchFilter.Setting.STYLE> styleSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.LIMIT> limitSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.CUTIN> cutinSettingTbl;

		private static Dictionary<string, SearchFilter.Setting.ABILITY> abilitySettingTbl;

		private static Dictionary<string, string> frameButtonLabelTbl;

		private static Dictionary<string, Content.Attribute> attrButtonLabelTbl;

		private static Dictionary<string, Content.Type> tribeButtonLabelTbl;

		private static Dictionary<string, Content.Icon> spellButtonLabelTbl;

		private static Dictionary<string, Content.Icon> trapButtonLabelTbl;

		private static Dictionary<string, string> rarityButtonLabelTbl;

		private static Dictionary<string, string> styleButtonLabelTbl;

		private static Dictionary<string, string> limitButtonLabelTbl;

		private static Dictionary<string, string> cutinButtonLabelTbl;

		private static Dictionary<string, string> abilityButtonLabelTbl;

		private static Dictionary<string, string> toggleLabelTbl;

		private const int numToggles = 84;

		private List<FilterToggle> toggles;

		private Dictionary<FilterGroupType, SelectionItem> groupTopItem;

		private ExtendedScrollRect m_FilterScroll;

		private bool contentScrollAnimation;

		private Vector2 targetContentPosition;

		private static SearchFilter.Setting deckEditSetting;

		private static List<FilterGroupType> deckEditFilterGroupTypes;

		private readonly Dictionary<FilterGroupType, int> groupSize;

		private readonly int spacing;

		private readonly int spacingMobile;

		private readonly int paddingTop;

		private readonly int paddingBottom;

		private readonly Dictionary<FilterGroupType, int> groupSizeMobile;

		private const string k_ELabelCancelButton = "CancelButton";

		private const string k_ELabelFilterButton = "FilterButton";

		private const string k_ELabelResetButton = "ResetButton";

		private const string k_ELabelCancelShortcut = "CancelShortcut";

		private const string k_ELabelFilterShortcut = "FilterShortcut";

		private const string k_ELabelResetShortcut = "ResetShortcut";

		private const string k_ELabelResetShortcutMain = "ResetShortcut/ShortcutIcon0";

		private const string k_ELabelResetShortcutSub = "ResetShortcut/ShortcutIcon1";

		private SelectionButton m_CancelButton;

		private SelectionButton m_FilterButton;

		private SelectionButton m_ResetButton;

		private const string k_ELabelFilterGroupFrame = "Frame";

		private const string k_ELabelFilterGroupAttribute = "Attribute";

		private const string k_ELabelFilterGroupSpellTrap = "SpellTrap";

		private const string k_ELabelFilterGroupTribe = "Tribe";

		private const string k_ELabelFilterGroupLevel = "Level";

		private const string k_ELabelFilterGroupRarity = "Rarity";

		private const string k_ELabelFilterGroupAbility = "Ability";

		private const string k_ELabelFilterGroupStyle = "Style";

		private const string k_ELabelFilterGroupLimit = "Limit";

		private const string k_ELabelFilterGroupCutin = "Cutin";

		private List<FilterGroupType> m_FilterGroupTypes;

		public SearchFilter.Setting m_DefaultSetting;

		private static Content m_cci => null;

		public static void Open(SearchFilter.Setting setting, Action<SearchFilter.Setting> callback = null)
		{
		}

		public static void Open(SearchFilter.Setting setting, SearchFilter.Setting defaultSetting, List<FilterGroupType> filterGroupTypes, Action<SearchFilter.Setting> callback = null)
		{
		}

		public override void NotificationStackEntry()
		{
		}

		protected override void OnCreatedView()
		{
		}

		private void InitializeDefault()
		{
		}

		private void InitializeFooter()
		{
		}

		private IEnumerator InitializeFileterMenu()
		{
			return null;
		}

		private void setFilterFrame(FilterToggle ft)
		{
		}

		private void setFilterAttr(FilterToggle ft)
		{
		}

		private void setFilterTribe(FilterToggle ft)
		{
		}

		private void setFilterLevel(FilterToggle ft)
		{
		}

		private void setFilterSpell(FilterToggle ft)
		{
		}

		private void setFilterTrap(FilterToggle ft)
		{
		}

		private void setFilterRarity(FilterToggle ft)
		{
		}

		private void setFilterAbility(FilterToggle ft)
		{
		}

		private void setFilterStyle(FilterToggle ft)
		{
		}

		private void setFilterLimit(FilterToggle ft)
		{
		}

		private void setFilterCutin(FilterToggle ft)
		{
		}

		private void OnReset()
		{
		}

		private void SelectNextGroupTop(FilterGroupType filterGroup)
		{
		}

		private void SelectPrevGroupTop(FilterGroupType filterGroup)
		{
		}

		private void SetupScrollToSelectingItem(SelectionItem item, RectTransform itemRootRect)
		{
		}

		private void InitPadTransition(SelectionButton button, RectTransform buttonArea, FilterGroupType type)
		{
		}

		private void Update()
		{
		}

		public override bool OnBack()
		{
			return false;
		}

		public FilterDialog()
		{
			//((SelectDialogViewControllerBase<, , , >)(object)this)._002Ector();
		}
	}
}
