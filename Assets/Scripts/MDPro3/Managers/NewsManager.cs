using System;
using DG.Tweening;
using MDPro3;
using MDPro3.Net;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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

    private News[] loadedEntries;
    private News[] Entries => loadedEntries ?? news?.EnglishUS ?? Array.Empty<News>();
    private CancellationTokenSource imageLoadCancellation;
    private int imageLoadGeneration;
    private bool closeRequested;

    private List<Texture2D> newsPics = new List<Texture2D>();
    private float width = 455f;
    private int maxLoad = 5;

    private void Start()
    {
        Hide();
        width = GetComponent<RectTransform>().rect.width;
    }

    float idleTime;
    private void Update()
    {
        if (!showing)
            return;
        if (!Program.instance.menu.showing)
            return;
        idleTime += Time.deltaTime;
        if (idleTime > 5f)
            OnRight(0.2f);
    }

    int GetMax()
    {
        if(news == null)
            return 0;
        return Entries.Length > maxLoad ? maxLoad : Entries.Length;
    }

    public void Show()
    {
        if (GetMax() == 0 || newsPics.Count == 0)
            return;
        var cg = GetComponent<CanvasGroup>();
        cg.alpha = 1.0f;
        cg.blocksRaycasts = true;

        showing = true;

        newsPic.texture = newsPics[0];
        newsText.text = Entries[0].title;
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
        CancelImageLoad();
        Hide();
        ClearImages();
        currentNewsIndex = 0;
        closeRequested = false;
        // Keep titles, images and links from the same feed snapshot. A reload
        // cannot pair a late old thumbnail with a new news item's link.
        loadedEntries = (news?.EnglishUS ?? Array.Empty<News>())
            .Where(entry => entry != null).Take(maxLoad)
            .Select(entry => new News { title = entry.title, image = entry.image, url = entry.url }).ToArray();
        if (loadedEntries.Length == 0) return;
        imageLoadCancellation = new CancellationTokenSource();
        _ = LoadNewsImageAsync(loadedEntries, imageLoadGeneration, imageLoadCancellation.Token);
    }

    private async Task LoadNewsImageAsync(News[] snapshot, int generation, CancellationToken cancellationToken)
    {
        for (int i = 0; i < snapshot.Length; i++)
        {
            Texture2D texture = null;
            try
            {
                texture = await Tools.DownloadImageAsync(snapshot[i].image, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error)
            {
                // Only the optional thumbnail task is guarded. UI binding and
                // unrelated application errors are not swallowed here.
                Debug.LogWarning("News thumbnail could not be loaded: " + error.Message);
            }
            if (generation != imageLoadGeneration || !Application.isPlaying)
            {
                ReleaseImage(texture);
                return;
            }
            // A local neutral background keeps the English title/link usable
            // when an upstream image host is missing or its image is invalid.
            newsPics.Add(texture != null ? texture : Texture2D.blackTexture);
            if (i == 0 && !closeRequested) Show();
        }
    }

    private static void ReleaseImage(Texture2D texture)
    {
        if (texture != null && texture != Texture2D.blackTexture)
            UnityEngine.Object.Destroy(texture);
    }

    private void ClearImages()
    {
        if (newsPic != null) newsPic.texture = null;
        if (newsPic2 != null) newsPic2.texture = null;
        foreach (var texture in newsPics.Distinct()) ReleaseImage(texture);
        newsPics.Clear();
    }

    private void CancelImageLoad()
    {
        imageLoadGeneration++;
        if (imageLoadCancellation == null) return;
        imageLoadCancellation.Cancel();
        imageLoadCancellation.Dispose();
        imageLoadCancellation = null;
    }

    private void OnDestroy()
    {
        CancelImageLoad();
        ClearImages();
    }

    private Texture2D NewsImage(int index) => index >= 0 && index < newsPics.Count
        ? newsPics[index] : Texture2D.blackTexture;

    int currentNewsIndex = 0;
    public void OnRight(float moveTime = 0.1f)
    {
        idleTime = 0f;
        if (GetMax() < 2)
            return;

        var next = (currentNewsIndex + 1) % GetMax();

        newsPic.texture = NewsImage(currentNewsIndex);
        newsText.text = Entries[currentNewsIndex].title;

        newsPic2.texture = NewsImage(next);
        newsText2.text = Entries[next].title;

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
        if (GetMax() < 2)
            return;
        var max = GetMax();
        var next = (max + currentNewsIndex - 1) % max;

        newsPic.texture = NewsImage(currentNewsIndex);

        newsText.text = Entries[currentNewsIndex].title;
        newsPic2.texture = NewsImage(next);
        newsText2.text = Entries[next].title;
        currentNewsIndex = next;
        textCount.text = $"{currentNewsIndex + 1}/{GetMax()}";

        newsPic.transform.localPosition = new Vector2(0, 0);
        newsPic2.transform.localPosition = new Vector2(-width, 0);

        newsPic.transform.DOLocalMoveX(width, moveTime);
        newsPic2.transform.DOLocalMoveX(0, moveTime);
    }

    public void OnNewsClick()
    {
        if (currentNewsIndex < 0 || currentNewsIndex >= GetMax()) return;
        string url = Entries[currentNewsIndex].url;
        if (!string.IsNullOrEmpty(url)) Application.OpenURL(url.Replace("ygobbs.com", "ygobbs2.com"));
    }

    public void OnClose()
    {
        closeRequested = true;
        Hide();
    }
}
