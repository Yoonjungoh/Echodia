using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoginScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        
        // 로그인 창 입장 처음에만 커넥팅 시도
        if (Managers.Network.IsInitialized == false)
        {
            StartCoroutine(Managers.Network.CoDownloadServerURL(() => Managers.UI.ShowSceneUI<UI_Login>()));
        }
        else
        {
            Managers.UI.ShowSceneUI<UI_Login>();
        }

        Managers.Scene.CurrentScene = Define.Scene.Login;   // 처음 시작하는 Scene 강제 할당
    }

    private void Awake()
    {
        Init();
    }
}
