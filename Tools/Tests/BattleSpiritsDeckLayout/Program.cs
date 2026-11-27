using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class ElementFixture
{
    public readonly Dictionary<string, RectTransform> Elements = new Dictionary<string, RectTransform>();
    public T GetElement<T>(string name) where T : class => Elements.TryGetValue(name, out var value) ? value as T : null;
}
public partial class InspectorFixture
{
    public readonly ElementFixture Manager = new ElementFixture();
    public void Configure() { ConfigureBattleInspector(); }
}
public sealed class CardDetailView : InspectorFixture { }
public sealed partial class CardFixture
{
    public readonly RectTransform transform;
    private readonly RectTransform child;
    public DeckFixture.DeckLocation location;
    public CardFixture(RectTransform parent, Vector2 initialRoot)
    {
        transform = LayoutTests.Rect(parent);
        child = LayoutTests.Rect(transform); child.sizeDelta = initialRoot;
    }
}
public partial class DeckFixture
{
    public enum DeckLocation { MainDeck = 1, ExtraDeck = 2, SideDeck = 4 }
    public bool battleDeckLayout;
    public int defaultColumns = 10;
    public Vector2 defaultSpacing = new Vector2(4, 4), battleViewportSize;
    public float contentWidth, templateWidth;
    public CanvasGroup Viewport;
    public ScrollRect ScrollRect;
    public GridLayoutGroup MainDeckContent, ExtraDeckContent, SideDeckContent;
    public GameObject Template;
    public UIScrollToSelection m_AutoScroll;
    public UIScrollToSelection AutoScroll => m_AutoScroll ?? ScrollRect.GetComponent<UIScrollToSelection>();
    public List<CardFixture> cards = new List<CardFixture>();
    public void SetCount(DeckLocation location, int count)
    {
        var parent = GetDeckLocationParent(location);
        while (cards.Count(card => card.location == location) > count)
            cards.Remove(cards.Last(card => card.location == location));
        while (cards.Count(card => card.location == location) < count)
            cards.Add(new CardFixture(parent, new Vector2(72, 101)) { location = location });
    }
    public void Configure() { ConfigureBattleDeckLayout(); }
    public void Refresh() { RefreshBattleDeckLayout(); }
    public void Resize(Vector2 size) { Viewport.transform.GetComponent<RectTransform>().sizeDelta = size; LateUpdate(); }
}
public static partial class FaceFixture { }
public static class LayoutTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new Exception(message);
    }
    private static bool Near(float left, float right) => Math.Abs(left - right) < .01f;
    private static bool Near(Vector2 left, Vector2 right) => Near(left.x, right.x) && Near(left.y, right.y);
    private static Vector2 Pair(float[] value) => new Vector2(value[0], value[1]);
    public static RectTransform Rect(RectTransform parent = null)
    {
        var rect = new GameObject().transform; rect.SetParent(parent); return rect;
    }
    private static GridLayoutGroup Grid(RectTransform parent)
    {
        var rect = Rect(parent); var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = Pair(Serialized.DeckCell); grid.constraint = (GridLayoutGroup.Constraint)(int)Serialized.GridConstraint[0]; grid.constraintCount = (int)Serialized.GridConstraint[1];
        grid.padding.left = (int)Serialized.GridPadding[0]; grid.padding.right = (int)Serialized.GridPadding[1];
        grid.padding.top = (int)Serialized.GridPadding[2]; grid.padding.bottom = (int)Serialized.GridPadding[3];
        return grid;
    }
    private static DeckFixture Deck(Vector2 viewportSize, int count)
    {
        var fixture = new DeckFixture();
        var viewport = Rect(); viewport.sizeDelta = viewportSize;
        fixture.Viewport = viewport.gameObject.AddComponent<CanvasGroup>();
        var content = Rect(viewport); content.anchorMax = Vector2.one;
        fixture.MainDeckContent = Grid(Rect(content)); fixture.ExtraDeckContent = Grid(Rect(content)); fixture.SideDeckContent = Grid(Rect(content));
        fixture.ScrollRect = new GameObject().AddComponent<ScrollRect>();
        fixture.ScrollRect.enabled = false; fixture.ScrollRect.content = null; fixture.ScrollRect.viewport = null;
        fixture.Template = new GameObject(); Rect(fixture.Template.transform).sizeDelta = Pair(Serialized.TemplateRoot);
        for (int i = 0; i < count; i++) fixture.cards.Add(new CardFixture(fixture.MainDeckContent.transform.GetComponent<RectTransform>(), Pair(Serialized.TemplateRoot)) { location = DeckFixture.DeckLocation.MainDeck });
        return fixture;
    }
    private static void InspectorChecks()
    {
        var panel = Rect(); panel.sizeDelta = Pair(Serialized.InspectorPanel);
        var face = Rect(panel); face.sizeDelta = Pair(Serialized.InspectorFace);
        var description = Rect(panel);
        var fixture = new CardDetailView(); fixture.Manager.Elements["CardArea"] = face; fixture.Manager.Elements["DescriptionArea"] = description;
        fixture.Configure();
        Check(face.sizeDelta.x > Serialized.InspectorFace[0] * 1.9f, "Inspector face grows substantially from real 143-pixel prefab width.");
        Check(Near(face.sizeDelta.x, 280) && Near(face.sizeDelta.x / face.sizeDelta.y, MDPro3.Utility.CardImageLoader.DEFAULT_ASPECT), "Inspector reserves a 280-pixel complete card face at the production aspect.");
        Check(Near(face.anchorMin, new Vector2(.5f, 1)) && Near(face.anchorMax, face.anchorMin) && Near(face.pivot, face.anchorMin), "Inspector face is centered at the top of its real parent.");
        Check(Near(face.anchoredPosition, new Vector2(0, -74)), "Inspector face clears the title area.");
        Check(Near(description.offsetMax.y, -(74 + face.rect.height + 16)), "Description starts below the complete face with a gap.");
        Check(description.rect.height >= 380, "The real 412x896 inspector retains readable scrolling text height.");
        panel.sizeDelta = new Vector2(220, 900); fixture.Configure();
        Check(Near(face.rect.width, 180) && Near(face.rect.width / face.rect.height, MDPro3.Utility.CardImageLoader.DEFAULT_ASPECT), "Narrow inspectors size the complete face to the available panel width.");
        DeckEditor.UseMobileLayout = true; var saved = face.sizeDelta; panel.sizeDelta = new Vector2(412, 900); fixture.Configure();
        Check(Near(face.sizeDelta, saved), "Desktop inspector changes leave the mobile layout untouched.");
        DeckEditor.UseMobileLayout = false;
        var actionMenu = new InspectorFixture(); actionMenu.Manager.Elements["CardArea"] = face; actionMenu.Manager.Elements["DescriptionArea"] = description;
        actionMenu.Configure(); Check(Near(face.sizeDelta, saved), "Card action menus retain their separate full-size layout.");
        new CardDetailView().Configure();
        fixture.Manager.Elements.Remove("DescriptionArea"); fixture.Configure();
        fixture.Manager.Elements["DescriptionArea"] = description; face.SetParent(null); fixture.Configure();
        Check(true, "Missing inspector areas or parent are guarded.");
    }
    private static void DeckChecks()
    {
        var fixture = Deck(new Vector2(772, 822), 40); fixture.Configure();
        Check(fixture.battleDeckLayout && fixture.defaultColumns == 7, "Desktop deck switches the real ten-column baseline to seven columns.");
        Check(fixture.ScrollRect.enabled && fixture.ScrollRect.content != null && fixture.ScrollRect.viewport != null, "The disabled, unbound native ScrollRect receives its real viewport/content and is enabled.");
        Check(fixture.ScrollRect.vertical && !fixture.ScrollRect.horizontal && Near(fixture.ScrollRect.scrollSensitivity, 40), "Native vertical scrolling is configured.");
        Check(fixture.ScrollRect.viewport.GetComponent<RectMask2D>() != null, "The real viewport receives card clipping.");
        Check(fixture.AutoScroll != null && fixture.ScrollRect.GetComponent<UIScrollToSelection>() == fixture.AutoScroll, "Native selection scrolling is attached to the configured ScrollRect.");
        var cell = fixture.MainDeckContent.cellSize;
        Check(cell.x > Serialized.DeckCell[0] * 1.35f && cell.y > Serialized.DeckCell[1] * 1.35f, "Deck cells grow beyond the serialized 72x101 faces.");
        Check(Near(cell.x / cell.y, MDPro3.Utility.CardImageLoader.DEFAULT_ASPECT), "Deck cells preserve the complete card aspect.");
        Check(Near((fixture.Template.transform.GetChild(0) as RectTransform).sizeDelta, cell), "The template's fixed Root grows with its grid cell.");
        Check(fixture.cards.All(card => Near((card.transform.GetChild(0) as RectTransform).sizeDelta, cell)), "Every existing card Root matches the new cell size.");
        Check(fixture.MainDeckContent.constraint == GridLayoutGroup.Constraint.FixedColumnCount && fixture.MainDeckContent.constraintCount == 7, "The grid retains exactly seven columns.");
        Check(fixture.MainDeckContent.spacing.x > 0 && cell.x * 7 + fixture.MainDeckContent.spacing.x * 6 <= fixture.ScrollRect.viewport.rect.width, "The deck fits seven nonoverlapping cards inside its viewport.");
        Check(fixture.ScrollRect.content.rect.height > fixture.ScrollRect.viewport.rect.height, "Forty larger cards produce scrollable content rather than overlapping columns.");
        Check(!fixture.ExtraDeckContent.transform.parent.gameObject.activeSelf && !fixture.SideDeckContent.transform.parent.gameObject.activeSelf, "Empty Extra and Side sections are hidden.");
        float fortyHeight = fixture.ScrollRect.content.rect.height;
        fixture.SetCount(DeckFixture.DeckLocation.MainDeck, 50); fixture.Refresh();
        Check(fixture.ScrollRect.content.rect.height > fortyHeight, "Fifty cards add rows and increase scroll height.");
        fixture.Resize(new Vector2(640, 822));
        Check(fixture.MainDeckContent.cellSize.x < cell.x && fixture.MainDeckContent.constraintCount == 7, "LateUpdate recomputes seven columns after width changes.");
        Check(fixture.cards.All(card => Near((card.transform.GetChild(0) as RectTransform).sizeDelta, fixture.MainDeckContent.cellSize)), "Resizing updates existing card Roots as well as the grid.");
        var movingCard = fixture.cards[0]; movingCard.CardRoot.SetParent(Rect());
        fixture.Resize(new Vector2(700, 822));
        Check(movingCard.transform.childCount == 0 && Near(movingCard.CardRoot.sizeDelta, fixture.MainDeckContent.cellSize), "A detached Root is resized safely while its card movement is animated.");
        fixture.SetCount(DeckFixture.DeckLocation.MainDeck, 0); fixture.Refresh();
        Check(fixture.MainDeckContent.transform.parent.gameObject.activeSelf, "An empty main deck keeps its destination visible.");
        Check(fixture.GetDeckLocationCount(DeckFixture.DeckLocation.MainDeck) == 50 && fixture.cards.Count == 0, "The fixture retains stale old grid children while Unity's deferred destruction is pending.");
        Check(Near(fixture.ScrollRect.content.rect.height, fixture.ScrollRect.viewport.rect.height), "An empty main deck fills its viewport without excess scroll height.");
        fixture.SetCount(DeckFixture.DeckLocation.ExtraDeck, 2); fixture.SetCount(DeckFixture.DeckLocation.SideDeck, 1); fixture.Refresh();
        var mainSection = fixture.MainDeckContent.transform.parent as RectTransform;
        var extraSection = fixture.ExtraDeckContent.transform.parent as RectTransform;
        var sideSection = fixture.SideDeckContent.transform.parent as RectTransform;
        Check(extraSection.gameObject.activeSelf && sideSection.gameObject.activeSelf, "Nonempty sections remain available to layout callers.");
        Check(Near(extraSection.anchoredPosition.y, -mainSection.rect.height) && Near(sideSection.anchoredPosition.y, -mainSection.rect.height - extraSection.rect.height), "Visible sections stack below the preceding section without overlap.");
        var mask = fixture.ScrollRect.viewport.GetComponent<RectMask2D>(); var scroll = fixture.AutoScroll; fixture.Configure();
        Check(fixture.ScrollRect.viewport.GetComponent<RectMask2D>() == mask && fixture.AutoScroll == scroll, "Repeated configuration reuses viewport clipping and selection scrolling.");
        var serialized = Deck(Pair(Serialized.DeckViewport), 40); serialized.Configure();
        Check(serialized.MainDeckContent.cellSize.x > Serialized.DeckCell[0] && serialized.ScrollRect.content.rect.height > serialized.ScrollRect.viewport.rect.height, "Actual standalone prefab viewport dimensions also produce larger scrollable cards.");
        DeckEditor.UseMobileLayout = true; var mobile = Deck(new Vector2(700, 800), 40); mobile.Configure();
        Check(!mobile.battleDeckLayout && !mobile.ScrollRect.enabled && mobile.ScrollRect.content == null, "Mobile layouts retain their existing separate configuration.");
        DeckEditor.UseMobileLayout = false;
        var beforeConfigure = Deck(new Vector2(772, 822), 0); beforeConfigure.Refresh();
        Check(!beforeConfigure.battleDeckLayout && beforeConfigure.ScrollRect.content == null, "Refresh before desktop configuration does not use a null ScrollRect binding.");
        var awake = Deck(new Vector2(772, 822), 0); awake.cards = null; awake.Configure();
        Check(awake.MainDeckContent.transform.parent.gameObject.activeSelf && Near(awake.ScrollRect.content.rect.height, 822), "Awake handles the null card list before its first deck print.");
    }
    private static void FaceChecks()
    {
        var image = new GameObject().AddComponent<RawImage>();
        image.material = new object(); image.uvRect = new Rect(.1f, .1f, .6f, .5f);
        var texture = new Texture { width = 650, height = 900 };
        FaceFixture.FitFace(image, texture);
        Check(image.texture == texture && image.material == null, "Complete faces use the native texture and default UI material.");
        Check(Near(image.uvRect.x, 0) && Near(image.uvRect.y, 0) && Near(image.uvRect.width, 1) && Near(image.uvRect.height, 1), "Full-face UVs show the entire source image.");
        var fitter = image.GetComponent<AspectRatioFitter>();
        Check(fitter.aspectMode == AspectRatioFitter.AspectMode.FitInParent && Near(fitter.aspectRatio, 650f / 900), "Each source texture supplies its actual aspect ratio.");
        FaceFixture.FitFace(image, new Texture { width = 1000, height = 1400 });
        Check(image.GetComponent<AspectRatioFitter>() == fitter && Near(fitter.aspectRatio, 1000f / 1400), "Subsequent faces reuse their fitter and update its aspect.");
        FaceFixture.FitFace(null, texture); var saved = image.texture; FaceFixture.FitFace(image, null);
        Check(image.texture == saved, "Null face arguments preserve the current image without throwing.");
    }
    public static int Main()
    {
        InspectorChecks(); DeckChecks(); FaceChecks();
        Console.WriteLine("PASS: " + checks + " production inspector/deck/full-face assertions with real prefab inputs; Unity rendering not run.");
        return 0;
    }
}
