using System;
using System.Collections.Generic;
using UnityEngine;
using YgomGame.Menu;
using YgomSystem.UI;

namespace YgomGame.Deck
{
	public class SortDialog : SelectDialogViewControllerBase<SortComparer.Sorter>, IBokeSupported
	{
		private const string PREFAB_PATH_SORTDIALOG = "DeckEdit/SortDialog";

		private const string LABEL_SBN_CANCELBUTTON = "ButtonFooter";

		private const string LABEL_RT_SORTBUTTONAREA = "SortButtonsArea";

		private const string Label_Obtained = "Obtained";

		private const string Label_Inventroy = "Inventroy";

		private const string Label_Rarity = "Rarity";

		private const string Label_Stars = "Stars";

		private const string Label_Atk = "Atk";

		private const string Label_Def = "Def";

		private SorterToggle template;

		private static Dictionary<string, SortComparer.METHOD> methodTbl;

		private static Dictionary<string, string> methodLabelTbl;

		private RectTransform m_SortButtonArea => null;

		private SelectionButton m_CancelButton => null;

		public static void Open(SortComparer.METHOD method, SortComparer.ORDER order, Action<SortComparer.Sorter> callback = null)
		{
		}

		public override void NotificationStackEntry()
		{
		}

		protected override void OnCreatedView()
		{
		}

		public static string GetSorterName(SortComparer.Sorter s)
		{
			return null;
		}

		public SortDialog()
		{
			//((SelectDialogViewControllerBase<>)(object)this)._002Ector();
		}
	}
}
