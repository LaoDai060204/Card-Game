using System;
using DG.Tweening;
using MDPro3;
using MDPro3.Net;
using System.Collections;
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

    private News[] Entries => news?.EnglishUS ?? Array.Empty<News>();

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
        if (GetMax() == 0)
        {
            Hide();
            return;
        }
        _ = LoadNewsImageAsync();
    }

    async Task LoadNewsImageAsync()
    {
        Hide();
        for (int i = 0; i < Entries.Length && i < maxLoad; i++)
        {
            var load = Tools.DownloadImageAsync(Entries[i].image);

            await TaskUtility.WaitUntil(() => load.IsCompleted);
            if (!Application.isPlaying)
                return;

            newsPics.Add(load.Result);
            if (i == 0)
                Show();
        }
    }

    int currentNewsIndex = 0;
    public void OnRight(float moveTime = 0.1f)
    {
        idleTime = 0f;
        if (GetMax() < 2)
            return;

        var next = (currentNewsIndex + 1) % GetMax();

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];
        newsText.text = Entries[currentNewsIndex].title;

        if (newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
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

        if (newsPics.Count < currentNewsIndex + 1)
            newsPic.texture = null;
        else
            newsPic.texture = newsPics[currentNewsIndex];

        newsText.text = Entries[currentNewsIndex].title;
        if(newsPics.Count < next + 1)
            newsPic2.texture = null;
        else
            newsPic2.texture = newsPics[next];
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
        Application.OpenURL(Entries[currentNewsIndex].url.Replace("ygobbs.com", "ygobbs2.com"));
    }

    public void OnClose()
    {
        Hide();
    }
}
