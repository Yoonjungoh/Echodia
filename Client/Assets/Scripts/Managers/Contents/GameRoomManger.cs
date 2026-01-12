using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomManager
{
    // 현재 Room Id, -1이면 방에 없음
    public RoomInfo RoomInfo { get; set; } = new RoomInfo();
    public RepeatedField<int> PlayerIdList = new RepeatedField<int>();
    public bool IsCountdownFinished { get; set; } = false;
    public float GameOverPopupDelayTime { get; set; } = 3.0f;

    public void Init()
    {

    }

    public void OnShowGameOverPopup(RoomExitReason roomExitReason)
    {
        Managers.GameRoomObject.MyPlayer.CreatureState = CreatureState.None;
        UI_GameResult gameResultUI = Managers.UI.ShowPopupUI<UI_GameResult>();
        gameResultUI.SetData(new GameResultPopupData
        {
            GameResultText = Util.GetGameResult(roomExitReason)
        });
    }
    
    public void EnterGame(RoomInfo roomInfo, RepeatedField<int> playerIdList)
    {
        RoomInfo = roomInfo;
        PlayerIdList = playerIdList;
        Managers.Scene.LoadScene(Define.Scene.GameRoom);
    }

    public void StartGame(float time)
    {
        Managers.UI.ShowCountdown(time, OnStartGame);
    }

    private void OnStartGame()
    {
        Managers.UI.ShowToastPopup("게임 시작!");
        Managers.GameRoomObject.MyPlayer.OnStartGame();
        IsCountdownFinished = true;
    }

    public void ExitGame()
    {
        Managers.Scene.LoadScene(Define.Scene.Lobby);
    }

    public void Clear()
    {
        RoomInfo = new RoomInfo();
        RoomInfo.RoomId = -1;
        IsCountdownFinished = false;
    }
}