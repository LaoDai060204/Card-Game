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
    private MyCardNews activeSource;
    private News[] activeNews;
    private string activeLanguage;
    private int newsGeneration;

    private static News[] SelectNews(MyCardNews source, string language)
    {
        if (language == Language.Vietnamese)
            return null;
        if (language == Language.SimplifiedChinese || language == Language.TraditionalChinese)
            return source?.ChineseCN;
        return source?.EnglishUS;
    }

    private void Awake()
    {
        SystemEvent.OnLanguageChange += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        SystemEvent.OnLanguageChange -= OnLanguageChanged;
        newsGeneration++;
        ClearNewsImages();
    }

    private void OnLanguageChanged()
    {
        LoadNews();
    }

    private bool RefreshSource()
    {
        var language = Language.GetConfig();
        var selected = SelectNews(news, language);
        if (activeLanguage == language && object.ReferenceEquals(activeSource, news)
            && object.ReferenceEquals(activeNews, selected))
            return false;

        // A new feed or language owns a separate cache and loading generation.
        // An older download may finish later, but cannot populate this cache.
        newsGeneration++;
        activeSource = news;
        activeNews = selected;
        activeLanguage = language;
        loadingNews = false;
        currentNewsIndex = 0;
        idleTime = 0f;
        Hide();
        ClearNewsImages();
        return true;
    }

    private void ClearNewsImages()
    {
        if (newsPic != null)
        {
            newsPic.transform.DOKill();
            newsPic.texture = null;
        }
        if (newsPic2 != null)
        {
            newsPic2.transform.DOKill();
            newsPic2.texture = null;
        }
        if (newsText != null)
            newsText.text = string.Empty;
        if (newsText2 != null)
            newsText2.text = string.Empty;
        if (textCount != null)
            textCount.text = string.Empty;
        foreach (var texture in newsPics)
            if (texture != null)
                Destroy(texture);
        newsPics.Clear();
    }

    private bool IsCurrentLoad(int generation, News[] source)
    {
        return generation == newsGeneration && activeLanguage == Language.GetConfig()
            && object.ReferenceEquals(activeSource, news)
            && object.ReferenceEquals(activeNews, source)
            && object.ReferenceEquals(SelectNews(news, activeLanguage), source);
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
        if (RefreshSource())
            LoadNews();
        if (!showing)
            return;
        if (Program.instance == null || Program.instance.menu == null || !Program.instance.menu.showing)
            return;
        idleTime += Time.deltaTime;
        if (idleTime > 5f)
            OnRight(0.2f);
    }

    int GetMax()
    {
        return Mathf.Min(activeNews?.Length ?? 0, maxLoad);
    }

    public void Show()
    {
        if (RefreshSource())
        {
            LoadNews();
            return;
        }
        if (GetMax() == 0 || newsPics.Count == 0)
        {
            Hide();
            return;
        }
        var cg = GetComponent<CanvasGroup>();
        cg.alpha = 1.0f;
        cg.blocksRaycasts = true;

        showing = true;

        newsPic.texture = newsPics[0];
        newsText.text = activeNews[0]?.title ?? string.Empty;
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
        RefreshSource();
        if (GetMax() == 0)
        {
            Hide();
            return;
        }
        if (newsPics.Count > 0)
            Show();
        if (loadingNews || newsPics.Count >= GetMax())
            return;
        _ = LoadNewsImageAsync(newsGeneration, activeNews);
    }

    async Task LoadNewsImageAsync(int generation, News[] source)
    {
        loadingNews = true;
        try
        {
            for (int i = newsPics.Count; i < Mathf.Min(source.Length, maxLoad); i++)
            {
                if (!IsCurrentLoad(generation, source))
                    return;
                var image = source[i]?.image;
                var texture = string.IsNullOrEmpty(image) ? null : await Tools.DownloadImageAsync(image);
                if (this == null || !Application.isPlaying || !IsCurrentLoad(generation, source))
                {
                    if (texture != null)
                        Destroy(texture);
                    return;
                }

                newsPics.Add(texture);
                if (i == 0)
                    Show();
            }
        }
        catch (System.Exception exception)
        {
            if (this != null && Application.isPlaying && IsCurrentLoad(generation, source))
                Debug.LogWarning($"Unable to load news image: {exception.Message}");
        }
        finally
        {
            if (generation == newsGeneration)
                loadingNews = false;
        }
    }

    int currentNewsIndex = 0;
    public void OnRight(float moveTime = 0.1f)
    {
        idleTime = 0f;
        if (RefreshSource())
        {
            LoadNews();
            return;
        }
        if (!showing || GetMax() < 2)
            return;

        var next = (currentNewsIndex + 1) % GetMax();

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];
        newsText.text = activeNews[currentNewsIndex]?.title ?? string.Empty;

        if (newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
        newsText2.text = activeNews[next]?.title ?? string.Empty;

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
        if (RefreshSource())
        {
            LoadNews();
            return;
        }
        if (!showing || GetMax() < 2)
            return;
        var max = GetMax();
        var next = (max + currentNewsIndex - 1) % max;

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];

        newsText.text = activeNews[currentNewsIndex]?.title ?? string.Empty;
        if(newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
        newsText2.text = activeNews[next]?.title ?? string.Empty;
        currentNewsIndex = next;
        textCount.text = $"{currentNewsIndex + 1}/{GetMax()}";

        newsPic.transform.localPosition = new Vector2(0, 0);
        newsPic2.transform.localPosition = new Vector2(-width, 0);

        newsPic.transform.DOLocalMoveX(width, moveTime);
        newsPic2.transform.DOLocalMoveX(0, moveTime);
    }

    public void OnNewsClick()
    {
        if (RefreshSource())
        {
            LoadNews();
            return;
        }
        if (!showing || currentNewsIndex >= GetMax())
            return;
        var url = activeNews[currentNewsIndex]?.url;
        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url.Replace("ygobbs.com", "ygobbs2.com"));
    }

    public void OnClose()
    {
        Hide();
    }
}
