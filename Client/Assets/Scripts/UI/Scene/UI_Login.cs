using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Login : UI_Scene
{
    enum InputFields
    {
        IdInputField,
        PasswordInputField,
    }

    enum Buttons
    {
        LoginButton,
        ExitGameButton,
    }

    private TMP_InputField _idInputField;
    private TMP_InputField _passwordInputField;
    private bool _isLoginInProgress = false;    // 로그인 시도 중인지 여부

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.LoginButton).onClick.AddListener(OnClickLoginButton);
        GetButton((int)Buttons.ExitGameButton).onClick.AddListener(OnClickExitGameButton);
        Managers.Input.RegisterKeyAction(KeyCode.Return, OnClickLoginButton);

        Bind<TMP_InputField>(typeof(InputFields));

        _idInputField = Get<TMP_InputField>((int)InputFields.IdInputField);
        _passwordInputField = Get<TMP_InputField>((int)InputFields.PasswordInputField);
    }

    public void HandleLogin(LoginStatus loginStatus)
    {
        switch (loginStatus)
        {
            case LoginStatus.Success:
                OnLoginSuccess();
                break;
            case LoginStatus.PasswordWrong:
                Managers.UI.ShowToastPopup("비밀번호가 틀렸습니다.");
                break;
            case LoginStatus.AlreadyLoggedIn:
                Managers.UI.ShowToastPopup("이미 접속 중인 계정입니다.");
                break;
            case LoginStatus.SignUpSuccess:
                Managers.UI.ShowToastPopup("회원 가입에 성공했습니다.");
                _idInputField.text = "";
                _passwordInputField.text = "";
                break;
        }
    }
    
    private void OnLoginSuccess()
    {
        if (_isLoginInProgress)
        {
            Managers.UI.ShowToastPopup("로그인 처리 중입니다. 잠시만 기다려주세요.");
            return;
        }

        _isLoginInProgress = true;
        Managers.Scene.LoadScene(Define.Scene.ServerSelect);
    }

    private void OnClickLoginButton()
    {
        // TODO - 무한 패킷 발사 방지하기 위해 전송 주기 타이머 넣기
        C_Login loginPacket = new C_Login();
        loginPacket.Id = _idInputField.text;
        loginPacket.Password = _passwordInputField.text;
        Managers.Network.Send(loginPacket);
    }

    private void OnClickExitGameButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터 재생 종료
#else
    Application.Quit();                                // 빌드에서 게임 종료
#endif
    }
}
