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
    private int _selectedServerId = 0;

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.ExitGameButton).onClick.AddListener(OnClickExitGameButton);

        _serverMainScrollView = Util.FindChild(gameObject, "ServerMainContent", recursive: true);
        _serverChannelScrollView = Util.FindChild(gameObject, "ServerChannelContent", recursive: true);

        _selectedServerId = Managers.Data.DefaultServerId;

        // 서버에게 플레이어 리스트 요청
        C_RequestServerSummaryList requestServerSummaryListPacket = new C_RequestServerSummaryList();
        Managers.Network.Send(requestServerSummaryListPacket);
    }

    // 서버 요약 정보 업데이트 (서버의 채널 Id, 접속 유저수 같은 정보)
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
                _selectedServerId = serverInfo.ServerId;
            };

            _serverMainSubItemDict.Add(serverInfo.ServerId, serverMain_SubItem);
            addedServerIds.Add(serverInfo.ServerId);
        }

        // 기본적으로 첫 번째 서버의 채널 정보 요청
        if (addedServerIds.Contains(_selectedServerId))
        {
            RequestServerChannelInfo(_selectedServerId);
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
        // 내가 보고 있는 서버와 다른 유저에게 브로드캐스트 받은 서버가 다르면 렌더링 X
        if (_selectedServerId != selectedServerId)
            return;

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

    // 서버랑 채널 선택 후 입장 결과 처리
    public void OnServerSelected(int serverId, int channelId, EnterServerResult enterServerResult)
    {
        switch (enterServerResult)
        {
            case EnterServerResult.Success:
                Managers.GameRoom.SetMapData(serverId, channelId);
                Managers.Scene.LoadScene(Define.Scene.PlayerSelect);
                break;
            case EnterServerResult.ChannelFull:
                Managers.UI.ShowToastPopup("서버가 가득 찼습니다.");
                break;
            case EnterServerResult.ServerMaintenance:
                Managers.UI.ShowToastPopup("서버 점검 중입니다.");
                break;
            default:
                Managers.UI.ShowToastPopup("서버 오류가 발생했습니다.");
                break;
        }
    }
}
