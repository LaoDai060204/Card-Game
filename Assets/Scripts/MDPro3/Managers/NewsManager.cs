using DG.Tweening;
using MDPro3;
using MDPro3.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using MDPro3.Utility;

public class NewsManager : Manager
{
    public MyCardNews news;
    public RawImage newsPic;
    public RawImage newsPic2;
    public Text newsText;
    public Text newsText2;
    public Text textCount;
    public bool showing;

    private List<Texture2D> newsPics = new List<Texture2D>();
    private float width = 455f;
    private int maxLoad = 5;
    private bool loadingNews;
    private bool hiddenForVietnamese;

    private static bool UsesVietnamese => Language.GetConfig() == Language.Vietnamese;

    private void Awake()
    {
        hiddenForVietnamese = UsesVietnamese;
        SystemEvent.OnLanguageChange += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        SystemEvent.OnLanguageChange -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (UsesVietnamese)
        {
            hiddenForVietnamese = true;
            Hide();
        }
        else if (hiddenForVietnamese)
        {
            hiddenForVietnamese = false;
            LoadNews();
        }
    }

    private void Start()
    {
        Hide();
        width = GetComponent<RectTransform>().rect.width;
        if (newsPics.Count > 0)
            Show();
    }

    float idleTime;
    private void Update()
    {
        if (!showing)
            return;
        if (UsesVietnamese)
        {
            Hide();
            return;
        }
        if (Program.instance == null || Program.instance.menu == null || !Program.instance.menu.showing)
            return;
        idleTime += Time.deltaTime;
        if (idleTime > 5f)
            OnRight(0.2f);
    }

    int GetMax()
    {
        return Mathf.Min(news?.ChineseCN?.Length ?? 0, maxLoad);
    }

    public void Show()
    {
        if (UsesVietnamese)
        {
            Hide();
            return;
        }
        if (GetMax() == 0 || newsPics.Count == 0)
            return;
        var cg = GetComponent<CanvasGroup>();
        cg.alpha = 1.0f;
        cg.blocksRaycasts = true;

        showing = true;

        newsPic.texture = newsPics[0];
        newsText.text = news.ChineseCN[0]?.title ?? string.Empty;
        newsPic.rectTransform.anchoredPosition = new Vector2(0, 0);
        newsPic2.rectTransform.anchoredPosition = new Vector2(-width, 0);

        currentNewsIndex = 0;
        textCount.text = $"{currentNewsIndex + 1}/{GetMax()}";
    }

    public void Hide()
    {
        var cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        showing = false;
    }

    public void LoadNews()
    {
        if (UsesVietnamese)
        {
            hiddenForVietnamese = true;
            Hide();
            return;
        }
        if (GetMax() == 0)
            return;
        if (newsPics.Count > 0)
            Show();
        if (loadingNews || newsPics.Count >= GetMax())
            return;
        _ = LoadNewsImageAsync();
    }

    async Task LoadNewsImageAsync()
    {
        loadingNews = true;
        try
        {
            // Keep completed images when switching to Vietnamese, then resume
            // at the next image if a supported news language is selected again.
            for (int i = newsPics.Count; i < GetMax(); i++)
            {
                if (UsesVietnamese)
                    return;
                var image = news.ChineseCN[i]?.image;
                var texture = string.IsNullOrEmpty(image) ? null : await Tools.DownloadImageAsync(image);
                if (this == null || !Application.isPlaying)
                    return;

                newsPics.Add(texture);
                if (i == 0)
                    Show();
            }
        }
        catch (System.Exception exception)
        {
            if (this != null && Application.isPlaying)
                Debug.LogWarning($"Unable to load news image: {exception.Message}");
        }
        finally
        {
            loadingNews = false;
        }
    }

    int currentNewsIndex = 0;
    public void OnRight(float moveTime = 0.1f)
    {
        idleTime = 0f;
        if (UsesVietnamese || !showing || GetMax() < 2)
            return;

        var next = (currentNewsIndex + 1) % GetMax();

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];
        newsText.text = news.ChineseCN[currentNewsIndex]?.title ?? string.Empty;

        if (newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
        newsText2.text = news.ChineseCN[next]?.title ?? string.Empty;

        currentNewsIndex = next;
        textCount.text = $"{currentNewsIndex + 1}/{GetMax()}";

        newsPic.transform.localPosition = new Vector2 (0, 0);
        newsPic2.transform.localPosition = new Vector2(width, 0);

        newsPic.transform.DOLocalMoveX(-width, moveTime);
        newsPic2.transform.DOLocalMoveX(0, moveTime);

    }

    public void OnLeft(float moveTime = 0.1f)
    {
        idleTime = 0f;
        if (UsesVietnamese || !showing || GetMax() < 2)
            return;
        var max = GetMax();
        var next = (max + currentNewsIndex - 1) % max;

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];

        newsText.text = news.ChineseCN[currentNewsIndex]?.title ?? string.Empty;
        if(newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
        newsText2.text = news.ChineseCN[next]?.title ?? string.Empty;
        currentNewsIndex = next;
        textCount.text = $"{currentNewsIndex + 1}/{GetMax()}";

        newsPic.transform.localPosition = new Vector2(0, 0);
        newsPic2.transform.localPosition = new Vector2(-width, 0);

        newsPic.transform.DOLocalMoveX(width, moveTime);
        newsPic2.transform.DOLocalMoveX(0, moveTime);
    }

    public void OnNewsClick()
    {
        if (UsesVietnamese || !showing || currentNewsIndex >= GetMax())
            return;
        var url = news.ChineseCN[currentNewsIndex]?.url;
        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url.Replace("ygobbs.com", "ygobbs2.com"));
    }

    public void OnClose()
    {
        Hide();
    }
}
