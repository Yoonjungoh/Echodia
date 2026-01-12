using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class BaseScene : MonoBehaviour
{
	protected virtual void Init()
    {
        Object obj = GameObject.FindObjectOfType(typeof(EventSystem));
        if (obj == null)
            Managers.Resource.Instantiate("UI/EventSystem").name = "@EventSystem";

        // 재화 UI 소환
        if (Managers.Scene.CanShowCurrencyUIScene(Managers.Scene.CurrentScene))
        {
            Managers.UI.ShowSceneUI<UI_Currency>();
        }
    }
}
