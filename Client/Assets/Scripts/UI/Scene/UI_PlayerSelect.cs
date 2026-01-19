using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_PlayerSelect : UI_Scene
{
    enum Buttons
    {
        CreatePlayerButton,
        ExitGameButton,
    }

    private GameObject _playerScrollView;
    Dictionary<int, PlayerSelectInfo_SubItem> _playerSelectInfoSubItemDict = new Dictionary<int, PlayerSelectInfo_SubItem>();

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.CreatePlayerButton).onClick.AddListener(OnClickCreatePlayerButton);
        GetButton((int)Buttons.ExitGameButton).onClick.AddListener(OnClickExitGameButton);

        _playerScrollView = Util.FindChild(gameObject, "PlayerContent", recursive: true);

        // 서버에게 플레이어 리스트 요청
        C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
        Managers.Network.Send(requestPlayerListPacket);
    }

    public void UpdatePlayerInfos(RepeatedField<PlayerSelectInfo> playerInfoList)
    {
        foreach (PlayerSelectInfo_SubItem playerSelectInfo_SubItem in _playerSelectInfoSubItemDict.Values)
        {
            Managers.Resource.Destroy(playerSelectInfo_SubItem.gameObject);
        }
        _playerSelectInfoSubItemDict.Clear();

        int playerSelectInfoListCount = playerInfoList.Count;
        for (int i = 0; i < playerSelectInfoListCount; i++)
        {
            if (_playerSelectInfoSubItemDict.ContainsKey(playerInfoList[i].PlayerId))
                continue;

            PlayerSelectInfo_SubItem playerSelectInfo_SubItem = Managers.UI.MakeSubItem<PlayerSelectInfo_SubItem>(_playerScrollView.transform);
            playerSelectInfo_SubItem.SetData(new PlayerSelectInfo
            {
                PlayerId = playerInfoList[i].PlayerId,
                CurrencyData = new CurrencyData()
                {
                    Jewel = playerInfoList[i].CurrencyData.Jewel,
                    Gold = playerInfoList[i].CurrencyData.Gold
                }
            });
            _playerSelectInfoSubItemDict.TryAdd(playerInfoList[i].PlayerId, playerSelectInfo_SubItem);
        }
    }

    public void OnPlayerSelected(int playerId, bool canSelect)
    {
        if (canSelect == false)
        {
            Managers.UI.ShowToastPopup("해당 캐릭터로 입장이 불가능합니다");
            return;
        }
        
        Managers.GameRoomObject.PlayerId = playerId;
        Managers.Scene.LoadScene(Define.Scene.GameRoom);
    }

    private void OnClickCreatePlayerButton()
    {
        UI_CreatePlayer createPlayerUI = Managers.UI.ShowPopupUI<UI_CreatePlayer>();
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
