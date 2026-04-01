using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomScene : BaseScene
{
    protected override void Init()
    {
        base.Init();

        // 씬에 사용할 오브젝트 미리 풀에 생성
        InitPoolingObjects();

        // 게임 입장 하겠다고 패킷 전송 (퀘스트도 여기서 같이 받음)
        C_EnterGame enterGamePacket = new C_EnterGame()
        {
            PlayerId = Managers.GameRoomObject.PlayerId,
            ServerId = Managers.GameRoom.ServerId,
            ChannelId = Managers.GameRoom.ChannelId,
        };
        Managers.Network.Send(enterGamePacket);
        
        Managers.UI.ShowSceneUI<UI_GameRoom>();
    }

    private void InitPoolingObjects()
    {
        // 몬스터 프리팹 풀링
        GameObject monsterPrefab = Managers.Resource.Load<GameObject>("Prefabs/Creatures/Monsters/Bear");
        Managers.Pool.CreatePool(monsterPrefab, 100);

        // 아이템 프리팹 풀링
        GameObject itemPrefab = Managers.Resource.Load<GameObject>("Prefabs/UI/WorldSpace/UI_DropItem");
        Managers.Pool.CreatePool(itemPrefab, 100);

        // 데미지 뷰어 프리팹 풀링
        GameObject damageViewerPrefab = Managers.Resource.Load<GameObject>("Prefabs/UI/WorldSpace/UI_DamageViewer");
        Managers.Pool.CreatePool(damageViewerPrefab, 100);
    }
    
    private void Awake()
    {
        Init();
    }
}
