using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_WaitingRoom : UI_Scene
{
    enum Buttons
    {
        ExitButton,
        StartGameButton,
    }

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.ExitButton).onClick.AddListener(OnClickExitRoomButton);
        GetButton((int)Buttons.StartGameButton).onClick.AddListener(OnClickStartGameButton);

        // 방장만 게임 시작 버튼 활성화
        GetButton((int)Buttons.StartGameButton).gameObject.SetActive(Managers.WaitingRoom.IsRoomOwner);
    }

    private void OnClickExitRoomButton()
    {
        C_ExitRoom exitRoomPacket = new C_ExitRoom();
        Managers.Network.Send(exitRoomPacket);
    }

    private void OnClickStartGameButton()
    {
        // 내가 방장이고 게임에 들어갈 수 있는 조건을 만족할 때 게임 진입 가능
        if (Managers.WaitingRoom.IsRoomOwner && Managers.WaitingRoom.CanEnterGame)
        {
            Managers.UI.ShowToastPopup("게임 시작 요청");
            // 서버한테 패킷 전송
            C_StartGame startGamePacket = new C_StartGame();
            startGamePacket.RoomId = Managers.WaitingRoom.RoomInfo.RoomId;
            Managers.Network.Send(startGamePacket);
        }
        else
        {
            Managers.UI.ShowToastPopup("게임 시작 불가능");
        }
    }
}
