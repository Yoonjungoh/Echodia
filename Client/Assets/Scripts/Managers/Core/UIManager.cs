using System;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using UnityEngine;

public class UIManager
{
    private int _order = 10;

    private Dictionary<Type, UI_Popup> _popups = new Dictionary<Type, UI_Popup>();
    private Stack<UI_Popup> _popupStack = new Stack<UI_Popup>();

    private UI_Scene _sceneUI = null;
    public UI_Scene CurrentScene { get { return _sceneUI; } }

    public UI_Currency CurrencyUI { get; set; }
    private UI_ToastPopup _toastPopup;
    private UI_ItemTooltip _itemTooltip;
    private float _damageViewerDuration = -1f;

    public GameObject Root
    {
        get
        {
            GameObject root = GameObject.Find("@UI_Root");
            if (root == null)
            {
                root = new GameObject { name = "@UI_Root" };
            }
            return root;
        }
    }

    public void SetCanvas(GameObject go, bool sort = true)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        if (sort)
        {
            canvas.sortingOrder = _order;
            _order++;
        }
        else
        {
            canvas.sortingOrder = 0;
        }
    }

    public T ShowSceneUI<T>(string name = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(name))
        {
            name = typeof(T).Name;
        }

        GameObject go = Managers.Resource.Instantiate($"UI/Scene/{name}");
        T sceneUI = Util.GetOrAddComponent<T>(go);

        _sceneUI = sceneUI;
        _sceneUI.Init();
        go.transform.SetParent(Root.transform);

        return sceneUI;
    }

    public T ShowPopupUI<T>(string name = null) where T : UI_Popup
    {
        if (string.IsNullOrEmpty(name))
        {
            name = typeof(T).Name;
        }

        if (_popups.TryGetValue(typeof(T), out UI_Popup exist))
        {
            Debug.LogWarning($"[UIManager] {name} is already opened.");
            return exist as T;
        }

        GameObject go = Managers.Resource.Instantiate($"UI/Popup/{name}");
        T popup = Util.GetOrAddComponent<T>(go);

        _popups.Add(typeof(T), popup);
        _popupStack.Push(popup);

        popup.Init();
        SetCanvas(go, true);
        go.transform.SetParent(Root.transform);

        return popup;
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0)
            return;

        UI_Popup popup = _popupStack.Pop();
        if (popup == null)
            return;

        _popups.Remove(popup.GetType());

        Managers.Resource.Destroy(popup.gameObject);
        _order--;
    }

    public void ClosePopupUI(UI_Popup popup)
    {
        if (popup == null || _popupStack.Count == 0)
        {
            return;
        }

        if (_popupStack.Peek() != popup)
        {
            List<UI_Popup> tempStack = new List<UI_Popup>(_popupStack);
            if (tempStack.Remove(popup))
            {
                tempStack.Reverse();
                _popupStack = new Stack<UI_Popup>(tempStack);
            }
        }
        else
        {
            _popupStack.Pop();
        }

        _popups.Remove(popup.GetType());
        Managers.Resource.Destroy(popup.gameObject);
        _order--;
    }

    public void CloseSpecificPopup<T>() where T : UI_Popup
    {
        if (_popups.TryGetValue(typeof(T), out UI_Popup popup))
        {
            ClosePopupUI(popup);
        }
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
        {
            ClosePopupUI();
        }
    }

    public bool IsPopupActive<T>() where T : UI_Popup
    {
        return _popups.ContainsKey(typeof(T));
    }

    public T GetPopupUI<T>() where T : UI_Popup
    {
        if (_popups.TryGetValue(typeof(T), out UI_Popup popup))
            return popup as T;

        return null;
    }

    public void ShowItemTooltip(ItemInfo data)
    {
        if (_itemTooltip == null)
        {
            _itemTooltip = Managers.UI.ShowPopupUI<UI_ItemTooltip>();
            _itemTooltip.transform.SetParent(Root.transform);
            _itemTooltip.Init();
            // 항상 다른 팝업 위에 렌더링되도록 고정 order 설정
            _itemTooltip.GetComponent<Canvas>().sortingOrder = 999;
        }
        _itemTooltip.ShowTooltip(data);
    }

    public void HideItemTooltip()
    {
        if (_itemTooltip != null)
            _itemTooltip.HideTooltip();
    }

    // 맵 이동 시 UI 상태 초기화
    public void OnMapTransfer()
    {
        HideItemTooltip();
    }

    public void ShowDamageText(int damage, Vector3 worldPosition, bool isCritical)
    {
        UI_DamageViewer damageViewer = Managers.UI.MakeWorldSpaceUI<UI_DamageViewer>();
        damageViewer.SetData(isCritical);
        
        if (_damageViewerDuration < 0f)
        {
            if (damageViewer.TryGetComponent(out Animator anim))
            {
                foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == UI_DamageViewer.IDLE_CLIP_NAME)
                    {
                        _damageViewerDuration = clip.length;
                        break;
                    }
                }
            }
            
            if (_damageViewerDuration < 0f)
            {
                Debug.LogWarning($"[UIManager] '{UI_DamageViewer.IDLE_CLIP_NAME}' 클립을 찾지 못해 기본값 사용");
                _damageViewerDuration = 3.0f;
            }
        }

        damageViewer.ShowDamage(damage, worldPosition, _damageViewerDuration);
    }

    public void ShowToastPopup(string message, float duration = 1f)
    {
        if (_toastPopup == null)
        {
            _toastPopup = ShowPopupUI<UI_ToastPopup>();
        }
        _toastPopup.ShowToastPopup(message, duration);
    }

    public T MakeWorldSpaceUI<T>(Transform parent = null, bool worldPositionStays = false, string name = null) where T : UI_Base
    {
        if (string.IsNullOrEmpty(name))
        {
            name = typeof(T).Name;
        }

        GameObject go = Managers.Resource.Instantiate($"UI/WorldSpace/{name}");
        if (parent != null)
        {
            go.transform.SetParent(parent, worldPositionStays);
        }

        Canvas canvas = go.GetOrAddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        T worldSpaceUI = Util.GetOrAddComponent<T>(go);
        if (!worldSpaceUI.IsInitialized)
            worldSpaceUI.Init();

        return worldSpaceUI;
    }

    public T MakeSubItem<T>(Transform parent = null, string name = null, bool worldPositionStays = false) where T : UI_Base
    {
        if (string.IsNullOrEmpty(name))
        {
            name = typeof(T).Name;
        }

        GameObject go = Managers.Resource.Instantiate($"UI/SubItem/{name}");
        if (parent != null)
        {
            go.transform.SetParent(parent, worldPositionStays);
        }

        T subItem = Util.GetOrAddComponent<T>(go);
        subItem.Init();
        return subItem;
    }

    public void Clear()
    {
        CloseAllPopupUI();
        _sceneUI = null;
        _popups.Clear();
        _itemTooltip = null;
    }
}