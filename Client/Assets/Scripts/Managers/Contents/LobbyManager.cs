using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LobbyManager
{
    public bool IsEnterFirst { get; set; } = false;   // 클라이언트가 로비에 처음 접속했었는지 묻는 함수

    public PlayerSelectInfo MyPlayer { get; private set; }  // 선택된 플레이어 정보 (MyPlayer.PlayerId가 플레이어 선별하는 고유 Id), 오브젝트랑 별개, 재화도 같이 있음
    
    public void SetSelectedPlayerInfo(PlayerSelectInfo playerSelectInfo)
    {
        MyPlayer = playerSelectInfo;
        Debug.Log($"Selected Player Info Set! PlayerId: {MyPlayer.PlayerId}, Name: {MyPlayer.Name}");
    }
}