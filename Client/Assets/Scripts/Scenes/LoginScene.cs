using System.Threading.Tasks;
using UnityEngine;

public class LoginScene : BaseScene
{
    protected override void Init()
    {
        base.Init();

        // 로그인 창 입장 처음에만 커넥팅 시도
        if (Managers.Network.IsInitialized == false)
        {
            Managers.Network.CoDownloadServerURL(() => Managers.UI.ShowSceneUI<UI_Login>()).Forget();
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
