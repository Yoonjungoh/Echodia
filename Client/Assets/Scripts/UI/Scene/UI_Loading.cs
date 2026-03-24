using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
        CoLoadSceneProcess().Forget();
    }

    public void LoadScene(string sceneName)
    {
        gameObject.SetActive(true);
        _loadingText.color = new Color32(0, 0, 0, 255);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _loadSceneName = sceneName;
        CoLoadSceneProcess().Forget();
    }

    private async UniTaskVoid CoLoadSceneProcess()
    {
        _sceneLoadSlider.value = 0f;
        CoFade(true).Forget();

        AsyncOperation op = SceneManager.LoadSceneAsync(_loadSceneName);
        op.allowSceneActivation = false;

        float timer = 0f;
        while (op.isDone == false)
        {
            await UniTask.Yield();
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
                    return;
                }
            }
        }
    }
    private async UniTaskVoid CoFade(bool isFadeIn) 
    {
        float timer = 0f;
        while (timer <= 1f)
        {
            await UniTask.Yield();
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
            CoFade(false).Forget();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
