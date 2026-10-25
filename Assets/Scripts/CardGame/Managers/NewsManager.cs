using DG.Tweening;
using CardGame;
using CardGame.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

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
    private News[] sourceNews;
    private News[] activeNews = System.Array.Empty<News>();
    private int loadVersion;

    private void OnDestroy()
    {
        loadVersion++;
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
        if (Program.instance == null || Program.instance.menu == null || !Program.instance.menu.showing)
            return;
        idleTime += Time.deltaTime;
        if (idleTime > 5f)
            OnRight(0.2f);
    }

    int GetMax()
    {
        return activeNews.Length;
    }

    public void Show()
    {
        if (GetMax() == 0)
        {
            Hide();
            return;
        }
        if (newsPics.Count == 0)
            return;
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
        var englishNews = news?.EnglishUS;
        if (!object.ReferenceEquals(sourceNews, englishNews))
        {
            sourceNews = englishNews;
            var entries = new List<News>();
            if (englishNews != null)
            {
                foreach (var entry in englishNews)
                {
                    if (entry == null)
                        continue;
                    entries.Add(entry);
                    if (entries.Count == maxLoad)
                        break;
                }
            }

            // Keep titles, images, navigation and links on the same English feed.
            activeNews = entries.ToArray();
            loadVersion++;
            newsPics.Clear();
            currentNewsIndex = 0;
            Hide();
        }

        if (GetMax() == 0)
        {
            Hide();
            return;
        }
        if (newsPics.Count > 0)
            Show();
        if (loadingNews || newsPics.Count >= GetMax())
            return;
        _ = LoadNewsImageAsync(loadVersion);
    }

    async Task LoadNewsImageAsync(int version)
    {
        loadingNews = true;
        var entries = activeNews;
        try
        {
            for (int i = newsPics.Count; i < entries.Length; i++)
            {
                if (version != loadVersion)
                    return;
                var image = entries[i].image;
                var texture = string.IsNullOrEmpty(image) ? null : await Tools.DownloadImageAsync(image);
                if (this == null || !Application.isPlaying || version != loadVersion)
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
            // A refreshed feed may have arrived while the previous image loaded.
            if (this != null && Application.isPlaying && version != loadVersion)
                LoadNews();
        }
    }

    int currentNewsIndex = 0;
    public void OnRight(float moveTime = 0.1f)
    {
        idleTime = 0f;
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
