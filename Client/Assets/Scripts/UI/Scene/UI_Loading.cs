using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Loading : UI_Scene
{
    private static UI_Loading instance;
    public static UI_Loading Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = FindObjectOfType<UI_Loading>();
                if (obj != null)
                {
                    instance = obj;
                }
                else
                {
                    instance = CreateLoadingScene();
                }
            }
            return instance;
        }
    }

    private static UI_Loading CreateLoadingScene()
    {
        return Managers.Resource.Instantiate("UI/Scene/UI_Loading").GetComponent<UI_Loading>();
    }

    private void Awake()
    {
        if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private Slider _sceneLoadSlider;

    [SerializeField] private TextMeshProUGUI _loadingText;

    private string _loadSceneName;

    public void LoadScene(Define.Scene sceneType)
    {
        gameObject.SetActive(true);
        _loadingText.color = new Color32(0, 0, 0, 255);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _loadSceneName = sceneType.ToString();
        StartCoroutine(CoLoadSceneProcess());
    }

    public void LoadScene(string sceneName)
    {
        gameObject.SetActive(true);
        _loadingText.color = new Color32(0, 0, 0, 255);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _loadSceneName = sceneName;
        StartCoroutine(CoLoadSceneProcess());
    }

    private IEnumerator CoLoadSceneProcess()
    {
        _sceneLoadSlider.value = 0f;
        yield return StartCoroutine(CoFade(true));

        AsyncOperation op = SceneManager.LoadSceneAsync(_loadSceneName);
        op.allowSceneActivation = false;

        float timer = 0f;
        while (op.isDone == false)
        {
            yield return null;
            if (op.progress < 0.9f)
            {
                _sceneLoadSlider.value = op.progress;
            }
            else
            {
                timer += Time.unscaledDeltaTime;
                _sceneLoadSlider.value = Mathf.Lerp(0.9f, 1f, timer);
                if (_sceneLoadSlider.value >= 1f)
                {
                    op.allowSceneActivation = true;
                    yield break;
                }
            }
        }
    }
    private IEnumerator CoFade(bool isFadeIn) 
    {
        float timer = 0f;
        while (timer <= 1f)
        {
            yield return null;
            timer += Time.unscaledDeltaTime * 3f;
            _canvasGroup.alpha = isFadeIn ? Mathf.Lerp(0f, 1f, timer) : Mathf.Lerp(1f, 0f, timer);
        }
        if (isFadeIn == false)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        // 모두 불러와졌음
        if (scene.name == _loadSceneName)
        {
            StartCoroutine(CoFade(false));
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
