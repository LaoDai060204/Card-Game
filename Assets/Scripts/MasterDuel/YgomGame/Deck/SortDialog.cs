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

		// Preserve imported asset lookup labels while keeping source text in English.
		private const string Label_Obtained = "\u5165";

		private const string Label_Inventroy = "\u6240\ufffd";

		private const string Label_Rarity = "レ\ufffd";

		private const string Label_Stars = "レベル\ufffd";

		private const string Label_Atk = "\u653b\ufffd";

		private const string Label_Def = "\u5b88\ufffd";

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
