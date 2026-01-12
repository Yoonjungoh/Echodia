using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerEx
{
    public Define.Scene _currentScene;
    public Define.Scene CurrentScene
    {
        get { return _currentScene; }
        set
        {
            if (_currentScene == value)
                return;
            _currentScene = value;
            Managers.Sound.ChangeBgmWhenSceneLoaded();
        }
    }
    // 해당 타입의 Scene이 재화UI를 보여줄 수 있는지 여부를 반환 (계정, 캐릭터 선택창에선 굳이 보여줄 필요 없으니까)

    private HashSet<Define.Scene> _canShowCurrencyUIScene = new HashSet<Define.Scene>()
    {
        Define.Scene.Lobby,
        Define.Scene.WaitingRoom,
        Define.Scene.GameRoom,
    };

    public void LoadScene(string sceneName)
    {
        Define.Scene sceneEnumValue = Define.Scene.Unknown;

        if (Enum.TryParse(sceneName, out sceneEnumValue))
        {
            Managers.Clear();
            UI_Loading.Instance.LoadScene(sceneName);
            Debug.Log($"Loading {sceneName} Scene");
        }
        else
        {
            Debug.Log($"Dont exist {sceneName} Scene");
        }
        CurrentScene = sceneEnumValue;
    }

    public void LoadScene(Define.Scene type)
    {
        Managers.Clear();
        CurrentScene = type;
        UI_Loading.Instance.LoadScene(GetSceneName(type));
    }

    // 해당 타입의 Scene이 재화UI를 보여줄 수 있는지 여부를 반환 (계정, 캐릭터 선택창에선 굳이 보여줄 필요 없으니까)
    public bool CanShowCurrencyUIScene(Define.Scene type)
    {
        if (_canShowCurrencyUIScene == null)
            return false;
        
        return _canShowCurrencyUIScene.Contains(type);
    }

    private string GetSceneName(Define.Scene type)
    {
        string name = System.Enum.GetName(typeof(Define.Scene), type);
        return name;
    }
}
