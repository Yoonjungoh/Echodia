using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ServerMain_SubItem;

public class UI_ServerSelect : UI_Scene
{
    enum Buttons
    {
        ExitGameButton,
    }

    private GameObject _serverMainScrollView;
    private GameObject _serverChannelScrollView;
    Dictionary<int, ServerMain_SubItem> _serverMainSubItemDict = new Dictionary<int, ServerMain_SubItem>();

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.ExitGameButton).onClick.AddListener(OnClickExitGameButton);

        _serverMainScrollView = Util.FindChild(gameObject, "ServerMainContent", recursive: true);
        _serverChannelScrollView = Util.FindChild(gameObject, "ServerChannelContent", recursive: true);
        
        // 서버에게 플레이어 리스트 요청
        C_RequestServerSummaryList requestServerSummaryListPacket = new C_RequestServerSummaryList();
        Managers.Network.Send(requestServerSummaryListPacket);
    }

    // 서버 요약 정보 업데이트
    public void InitServerSummaryInfos(RepeatedField<ServerInfo> serverInfoList)
    {
        // 1. 이미 추가한 서버 ID 추적용
        HashSet<int> addedServerIds = new HashSet<int>();

        foreach (ServerInfo serverInfo in serverInfoList)
        {
            // 이미 처리한 서버면 스킵
            if (addedServerIds.Contains(serverInfo.ServerId))
                continue;

            ServerMain_SubItem serverMain_SubItem = Managers.UI.MakeSubItem<ServerMain_SubItem>(_serverMainScrollView.transform);

            serverMain_SubItem.SetData(new ServerMainSubItemData
            {
                ServerId = serverInfo.ServerId,
                ServerName = serverInfo.ServerName,
            });

            serverMain_SubItem.OnClickSelectButtonAction += () =>
            {
                // 서버 채널 정보 요청
                RequestServerChannelInfo(serverInfo.ServerId);
            };

            _serverMainSubItemDict.Add(serverInfo.ServerId, serverMain_SubItem);
            addedServerIds.Add(serverInfo.ServerId);
        }

        // 기본적으로 첫 번째 서버의 채널 정보 요청
        if (addedServerIds.Contains(Managers.Data.DefaultServerId))
        {
            RequestServerChannelInfo(Managers.Data.DefaultServerId);
        }
    }

    private void RequestServerChannelInfo(int serverId)
    {
        C_RequestServerList requestServerListPacket = new C_RequestServerList
        {
            ServerId = serverId,
        };
        Managers.Network.Send(requestServerListPacket);
    }

    public void UpdateServerChannelInfos(int selectedServerId, RepeatedField<ServerInfo> serverInfoList)
    {
        // 기존 채널 UI 삭제
        foreach (Transform child in _serverChannelScrollView.transform)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        foreach (ServerInfo serverInfo in serverInfoList)
        {
            ServerChannel_SubItem serverChannel_SubItem = Managers.UI.MakeSubItem<ServerChannel_SubItem>(_serverChannelScrollView.transform);
            serverChannel_SubItem.SetData(new ServerInfo
            {
                ServerId = serverInfo.ServerId,
                ServerName = serverInfo.ServerName,
                ChannelId = serverInfo.ChannelId,
                CurrentPlayerCount = serverInfo.CurrentPlayerCount,
                MaxPlayerCount = serverInfo.MaxPlayerCount,
            });
        }
    }

    private void OnClickExitGameButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터 재생 종료
#else
    Application.Quit();                                // 빌드에서 게임 종료
#endif
    }

    public void OnServerSelected(int serverId, int channelId, bool canSelect)
    {
        if (canSelect == false)
        {
            Managers.UI.ShowToastPopup("선택한 서버에 접속할 수 없습니다.");
            return;
        }

        Managers.GameRoom.SetMapData(serverId, channelId);
        Managers.Scene.LoadScene(Define.Scene.PlayerSelect);
    }
}
