using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;

class PacketHandler
{
    public static void S_AssignUserIdHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_AssignUserId assignUserIdPacket = packet as S_AssignUserId;
        if (assignUserIdPacket == null)
        {
            Debug.Log("S_AssignUserId 패킷이 null입니다");
            return;
        }
        // TODO - 재할당 필요할 때 요청
    }

    public static void S_ExitRoomHandler(PacketSession session, IMessage packet)
    {
        S_ExitRoom exitRoomPacket = packet as S_ExitRoom;
        if (exitRoomPacket == null)
        {
            Debug.Log("S_ExitRoom 패킷이 null입니다");
            return;
        }

        // TODO - 대기실 나가기 처리 하거나 게임 종료
        //Managers.WaitingRoom.ExitRoom();
    }

    // 내가 게임에 입장할 때 패킷
    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;
        if (enterGamePacket == null)
        {
            Debug.Log("S_EnterGame 패킷이 null입니다");
            return;
        }

        Managers.GameRoom.HandleEnterGame(enterGamePacket.ObjectState);
    }

    public static void S_AttackHandler(PacketSession session, IMessage packet)
    {
        S_Attack attackPacket = packet as S_Attack;
        if (attackPacket == null)
        {
            Debug.Log("S_Attack 패킷이 null입니다");
            return;
        }

        foreach (DamagedInfo damagedInfo in attackPacket.DamagedObjectList)
        {
            GameObject go = Managers.GameRoomObject.FindById(damagedInfo.ObjectId);
            if (go == null)
                continue;

            CreatureController cc = go.GetComponent<CreatureController>();
            if (cc == null)
                continue;

            if (cc.ObjectState.ObjectType == GameObjectType.Player)
            {
                Debug.Log($"크리티컬: {damagedInfo.IsCritical}, 공격력: {cc.Stat.Hp - damagedInfo.RemainHp}");
            }
            cc.OnDamaged(damagedInfo.RemainHp, damagedInfo.IsCritical);
        }
    }

    // 게임에서 죽었을 때
    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_LeaveGame leaveGamePacket = packet as S_LeaveGame;

        // 커서 잠금 풀기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // TODO - 리스폰 UI 띄우기
    }

    public static void S_SpawnHandler(PacketSession session, IMessage packet)
    {
        S_Spawn spawnPacket = packet as S_Spawn;

        if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            foreach (ObjectState objectState in spawnPacket.ObjectStateList)
            {
                Managers.GameRoomObject.Add(objectState, isMyPlayer: false);
            }
        }
    }

    public static void S_DespawnHandler(PacketSession session, IMessage packet)
    {
        S_Despawn despawnPacket = packet as S_Despawn;

        if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            foreach (int id in despawnPacket.ObjectIdList)
            {
                Managers.GameRoomObject.Remove(id, isDead: false);
            }
        }
    }

    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;

        // 움직임 동기화 시킬 오브젝트 찾기
        GameObject go = null;
        if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            go = Managers.GameRoomObject.FindById(movePacket.ObjectState.ObjectId);
        }
        if (go == null)
        {
            Debug.Log($"Cant find GameObject {movePacket.ObjectState.ObjectId}");
            return;
        }

        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Debug.Log("Cant find CreatureController");
            return;
        }

        cc.ObjectState.Position = movePacket.ObjectState.Position;
        cc.ObjectState.Rotation = movePacket.ObjectState.Rotation;
        cc.ObjectState.Velocity = movePacket.ObjectState.Velocity;
        cc.ObjectState.CreatureState = movePacket.ObjectState.CreatureState;

        GameObjectType type = GameObjectType.None;
        if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            type = Managers.GameRoomObject.GetObjectTypeById(cc.Id);
        }

        if (type == GameObjectType.Player)
        {
            OtherPlayerController otherPlayer = go.GetComponent<OtherPlayerController>();
            if (otherPlayer != null)
            {
                otherPlayer.SetServerState(
                    movePacket.ObjectState.Position,
                    movePacket.ObjectState.Rotation,
                    movePacket.ObjectState.Velocity,
                    movePacket.ObjectState.ServerReceivedTime
                );
            }
        }
        else if (type == GameObjectType.Monster)
        {
            MonsterController monster = go.GetComponent<MonsterController>();
            if (monster != null)
            {
                monster.SetServerState(
                    movePacket.ObjectState.Position,
                    movePacket.ObjectState.Rotation,
                    movePacket.ObjectState.Velocity,
                    movePacket.ObjectState.ServerReceivedTime
                );
            }
        }
    }

    public static void S_DieHandler(PacketSession session, IMessage packet)
    {
        S_Die diePacket = packet as S_Die;
        // 죽은 오브젝트 상태 변경
        GameObject go = Managers.GameRoomObject.FindById(diePacket.DamagedObjectId);
        if (go == null)
        {
            Debug.Log($"Id: {diePacket.DamagedObjectId}가 존재하지 않음");
            return;
        }
        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Debug.Log($"Id: {diePacket.DamagedObjectId}의 CreatureController가 존재하지 않음");
            return;
        }
        cc.CreatureState = diePacket.CreatureState;
        Managers.GameRoomObject.Remove(diePacket.DamagedObjectId, isDead: true);
        cc.OnDead();
    }

    public static void S_TimestampHandler(PacketSession session, IMessage packet)
    {
        S_Timestamp sereverTimestamp = packet as S_Timestamp;
        Managers.Network.CalculateTimeOffset(sereverTimestamp.ClientSendTime, sereverTimestamp.ServerReceivedTime);
        // Managers.UI.ShowToastPopup($"서버 레이턴시: {(int)Managers.Network.LatencyMs}ms");
    }

    public static void S_PingHandler(PacketSession session, IMessage packet)
    {
        S_Ping pingPacket = packet as S_Ping;
        if (pingPacket == null)
            return;

        C_Pong pongPacket = new C_Pong { PingId = pingPacket.PingId };
        Managers.Network.Send(pongPacket);
    }

    public static void S_ChangeCreatureStateHandler(PacketSession session, IMessage packet)
    {
        S_ChangeCreatureState changeCreatureStatePacket = packet as S_ChangeCreatureState;
        Managers.GameRoomObject.HandleChangeCreatureState(changeCreatureStatePacket.ObjectId, changeCreatureStatePacket.CreatureState);
    }

    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        S_Connected connectedPacket = packet as S_Connected;
        // TODO - 연결 성공 처리
        //Managers.UI.ShowToastPopup("서버와 연결되었습니다.");
    }

    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login loginPacket = packet as S_Login;
        if (loginPacket == null)
        {
            Debug.Log("S_Login 패킷이 null입니다");
            return;
        }

        UI_Login loginUI = Managers.UI.CurrentScene.GetComponent<UI_Login>();
        if (loginUI == null)
        {
            Debug.Log("현재 로그인 화면이 아닌데 로그인을 시도하려고 합니다.");
            return;
        }

        loginUI.HandleLogin(loginPacket.LoginStatus);
    }

    public static void S_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestPlayerList requestPlayerListPacket = packet as S_RequestPlayerList;
        if (requestPlayerListPacket == null)
        {
            Debug.Log("S_RequestPlayerList 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 선택을 하려고 합니다.");
            return;
        }

        playerSelectUI.UpdatePlayerInfos(requestPlayerListPacket.PlayerInfoList);
    }

    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer createPlaerPacket = packet as S_CreatePlayer;
        if (createPlaerPacket == null)
        {
            Debug.Log("S_CreatePlayer 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 선택을 하려고 합니다.");
            return;
        }

        if (createPlaerPacket.CanCreate == false)
        {
            Managers.UI.ShowToastPopup("중복 닉네임이 존재하여 캐릭터를 생성할 수 없습니다");
            return;
        }

        playerSelectUI.UpdatePlayerInfos(createPlaerPacket.PlayerInfoList);
    }

    public static void S_DeletePlayerHandler(PacketSession session, IMessage packet)
    {
        S_DeletePlayer deletePlaerPacket = packet as S_DeletePlayer;
        if (deletePlaerPacket == null)
        {
            Debug.Log("S_DeletePlayer 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 삭제를 하려고 합니다.");
            return;
        }

        if (deletePlaerPacket.CanDelete == false)
        {
            Managers.UI.ShowToastPopup("캐릭터 삭제에 실패했습니다. 다시 시도해주세요.");
            return;
        }

        Managers.UI.ShowToastPopup("캐릭터가 삭제되었습니다.");
        playerSelectUI.UpdatePlayerInfos(deletePlaerPacket.PlayerInfoList);
    }

    public static void S_UpdateCurrencyDataHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyData updateCurrencyDataPacket = packet as S_UpdateCurrencyData;
        if (updateCurrencyDataPacket == null)
        {
            Debug.Log("S_UpdateCurrencyData 패킷이 null입니다");
            return;
        }

        if (updateCurrencyDataPacket.CurrencyType == CurrencyType.Exp)
        {
            if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            {
                Debug.Log("현재 게임룸이 아닙니다.");
                return;
            }
            int maxExp = Managers.Data.GetMaxExpForLevelUp(Managers.GameRoomObject.MyPlayer.Level);
            //Managers.UI.ShowToastPopup($"현재 레벨: {Managers.GameRoomObject.MyPlayer.Level}, MaxExp: {maxExp}");
            Managers.GameRoomObject.MyPlayer.SetExp(updateCurrencyDataPacket.Amount, maxExp);
        }
        else if (updateCurrencyDataPacket.CurrencyType == CurrencyType.Level)
        {
            if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            {
                Debug.Log("현재 게임룸이 아닙니다.");
                return;
            }
            // 레벨 업에 따른 경험치 세팅 필요할 수도 있음
            Managers.GameRoomObject.MyPlayer.SetLevel(updateCurrencyDataPacket.Amount);
            int maxExp = Managers.Data.GetMaxExpForLevelUp(Managers.GameRoomObject.MyPlayer.Level);
            Managers.GameRoomObject.MyPlayer.SetExp(Managers.GameRoomObject.MyPlayer.Exp, maxExp);
        }
        else
        {
            Managers.Currency.UpdateCurrencyData(updateCurrencyDataPacket.CurrencyType, updateCurrencyDataPacket.Amount);
        }
    }

    public static void S_UpdateCurrencyDataAllHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyDataAll updateCurrencyDataAllPacket = packet as S_UpdateCurrencyDataAll;
        if (updateCurrencyDataAllPacket == null || updateCurrencyDataAllPacket.CurrencyData == null)
        {
            Debug.Log("S_UpdateCurrencyDataAll 패킷이 null입니다");
            return;
        }

        Managers.Currency.UpdateCurrencyDataAll(updateCurrencyDataAllPacket.CurrencyData);
    }

    public static void S_RequestServerSummaryListHandler(PacketSession session, IMessage packet)
    {
        S_RequestServerSummaryList requestServerSummaryListPacket = packet as S_RequestServerSummaryList;
        if (requestServerSummaryListPacket == null)
        {
            Debug.Log("S_RequestServerSummaryList 패킷이 null입니다");
            return;
        }

        UI_ServerSelect serverSelectUI = Managers.UI.CurrentScene.GetComponent<UI_ServerSelect>();
        if (serverSelectUI == null)
        {
            Debug.Log("현재 서버 선택창이 아닌데 서버 선택을 하려고 합니다.");
            return;
        }
        serverSelectUI.InitServerSummaryInfos(requestServerSummaryListPacket.ServerInfoList);
    }

    // 서버에서 보낸 서버 채널 정보 패킷 처리 (다른 유저가 들어오고 나와도 실행됨)
    public static void S_RequestServerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestServerList requestServerListPacket = packet as S_RequestServerList;
        if (requestServerListPacket == null)
        {
            Debug.Log("S_RequestServerList 패킷이 null입니다");
            return;
        }

        UI_ServerSelect serverSelectUI = Managers.UI.CurrentScene.GetComponent<UI_ServerSelect>();
        if (serverSelectUI == null)
        {
            Debug.Log("현재 서버 선택창이 아닌데 서버 선택을 하려고 합니다.");
            return;
        }
        serverSelectUI.UpdateServerChannelInfos(requestServerListPacket.ServerId, requestServerListPacket.ServerInfoList);
    }

    public static void S_SelectServerHandler(PacketSession session, IMessage packet)
    {
        S_SelectServer selectServerPacket = packet as S_SelectServer;
        if (selectServerPacket == null)
        {
            Debug.Log("S_SelectServer 패킷이 null입니다");
            return;
        }

        UI_ServerSelect serverSelectUI = Managers.UI.CurrentScene.GetComponent<UI_ServerSelect>();
        if (serverSelectUI == null)
        {
            Debug.Log("현재 서버 선택창이 아닌데 서버 선택을 하려고 합니다.");
            return;
        }
        serverSelectUI.OnServerSelected(selectServerPacket.ServerId, selectServerPacket.ChannelId, selectServerPacket.EnterServerResult);
    }

    public static void S_SelectPlayerHandler(PacketSession session, IMessage packet)
    {
        S_SelectPlayer selectPlayerPacket = packet as S_SelectPlayer;
        if (selectPlayerPacket == null)
        {
            Debug.Log("S_SelectPlayer 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 선택을 하려고 합니다.");
            return;
        }
        playerSelectUI.OnPlayerSelected(selectPlayerPacket.PlayerId, selectPlayerPacket.CanSelect);
    }

    public static void S_RequestInitGameRoomDataHandler(PacketSession session, IMessage packet)
    {
        S_RequestInitGameRoomData requestInitGameRoomDataPacket = packet as S_RequestInitGameRoomData;
        if (requestInitGameRoomDataPacket == null)
        {
            Debug.Log("S_RequestInitGameRoomData 패킷이 null입니다");
            return;
        }

        UI_GameRoom gameRoomUI = Managers.UI.CurrentScene.GetComponent<UI_GameRoom>();
        if (gameRoomUI == null)
        {
            Debug.Log("현재 게임룸이 아닙니다.");
            return;
        }
        gameRoomUI.SetData(requestInitGameRoomDataPacket.InitGameRoomData);
    }

    public static void S_CreateQuestHandler(PacketSession session, IMessage packet)
    {
        S_CreateQuest creatQuestPacket = packet as S_CreateQuest;
        if (creatQuestPacket == null)
        {
            Debug.Log("S_CreateQuest 패킷이 null입니다");
            return;
        }

        Managers.Quest.AddQuestData(creatQuestPacket.MainQuestId, creatQuestPacket.SubQuestId);
    }

    public static void S_RequestQuestDataHandler(PacketSession session, IMessage packet)
    {
        S_RequestQuestData requestQuestData = packet as S_RequestQuestData;
        if (requestQuestData == null)
        {
            Debug.Log("S_RequestQuestData 패킷이 null입니다");
            return;
        }

        Managers.Quest.InitQuestData(requestQuestData.QuestInfoList);
    }

    public static void S_ChangeQuestStatusHandler(PacketSession session, IMessage packet)
    {
        S_ChangeQuestStatus changeQuestStatusPacket = packet as S_ChangeQuestStatus;
        if (changeQuestStatusPacket == null)
        {
            Debug.Log("S_ChangeQuestStatus 패킷이 null입니다");
            return;
        }

        Managers.Quest.ChangeQuestStatus(changeQuestStatusPacket.MainQuestId, changeQuestStatusPacket.SubQuestId, changeQuestStatusPacket.QuestStatus);
    }

    public static void S_QuestProgressUpdateHandler(PacketSession session, IMessage packet)
    {
        S_QuestProgressUpdate questProgressPacket = packet as S_QuestProgressUpdate;
        if (questProgressPacket == null)
        {
            Debug.Log("S_QuestProgressUpdate 패킷이 null입니다");
            return;
        }

        Managers.Quest.UpdateQuestProgress(questProgressPacket.MainQuestId, questProgressPacket.SubQuestId, questProgressPacket.CurrentCount);
    }


    public static void S_CompleteQuestHandler(PacketSession session, IMessage packet)
    {
        S_CompleteQuest completeQuestPacket = packet as S_CompleteQuest;
        if (completeQuestPacket == null)
        {
            Debug.Log("S_CompleteQuest 패킷이 null입니다");
            return;
        }

        Managers.Quest.ChangeQuestStatus(completeQuestPacket.MainQuestId, completeQuestPacket.SubQuestId, completeQuestPacket.QuestStatus);
    }

    public static void S_GiveRewardHandler(PacketSession session, IMessage packet)
    {
        S_GiveReward giveRewardPacket = packet as S_GiveReward;
        if (giveRewardPacket == null)
        {
            Debug.Log("S_GiveReward 패킷이 null입니다");
            return;
        }

        Managers.Reward.ShowRewardList(giveRewardPacket.RewardItemList);
    }

    public static void S_UpdateItemDataHandler(PacketSession session, IMessage packet)
    {
        S_UpdateItemData updateItemDataPacket = packet as S_UpdateItemData;
        if (updateItemDataPacket == null)
        {
            Debug.Log("S_UpdateItemData 패킷이 null입니다");
            return;
        }

        Managers.Inventory.UpdateItemData(updateItemDataPacket.ItemInfo);
    }

    public static void S_UpdateItemDataAllHandler(PacketSession session, IMessage packet)
    {
        S_UpdateItemDataAll updateItemDataAllPacket = packet as S_UpdateItemDataAll;
        if (updateItemDataAllPacket == null)
        {
            Debug.Log("S_UpdateItemDataAll 패킷이 null입니다");
            return;
        }

        Managers.Inventory.UpdateItemDataAll(updateItemDataAllPacket.ItemInfoList);
    }

    public static void S_UseItemHandler(PacketSession session, IMessage packet)
    {
        S_UseItem useItemPacket = packet as S_UseItem;
        if (useItemPacket == null)
        {
            Debug.Log("S_UseItem 패킷이 null입니다");
            return;
        }

        switch (useItemPacket.UseItemResult)
        {
            case UseItemResult.Success:
                Managers.UI.ShowToastPopup("아이템이 사용되었습니다.");
                // 쿨타임 클라에서 시작해주기
                // 어차피 서버에서도 검사해주기 때문에 클라에선 보여주기 용도로 쿨타임 시작
                Managers.Cooldown.StartCooldown(useItemPacket.ItemId);
                break;
            case UseItemResult.NotEnoughLevel:
                Managers.UI.ShowToastPopup("레벨이 부족하여 아이템을 사용할 수 없습니다.");
                break;
            case UseItemResult.Cooldown:
                Managers.UI.ShowToastPopup("아직 쿨타임이 남았습니다.");
                break;
            default:
                Managers.UI.ShowToastPopup("알 수 없는 결과입니다.");
                break;
        }
    }

    public static void S_HealHpHandler(PacketSession session, IMessage packet)
    {
        S_HealHp healHpPacket = packet as S_HealHp;
        if (healHpPacket == null)
        {
            Debug.Log("S_HealHp 패킷이 null입니다");
            return;
        }

        GameObject go = Managers.GameRoomObject.FindById(healHpPacket.ObjectId);
        if (go == null)
        {
            Debug.Log($"Id: {healHpPacket.ObjectId}가 존재하지 않음");
            return;
        }
        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Debug.Log($"Id: {healHpPacket.ObjectId}의 CreatureController가 존재하지 않음");
            return;
        }
        cc.SetHp(healHpPacket.TotalHp, cc.Stat.MaxHp);
    }

    public static void S_UnequipItemHandler(PacketSession session, IMessage packet)
    {
        S_UnequipItem unEquipItemPacket = packet as S_UnequipItem;
        if (unEquipItemPacket == null)
        {
            Debug.Log("S_UnequipItem 패킷이 null입니다");
            return;
        }

        switch (unEquipItemPacket.UnEquipResult)
        {
            case UnEquipResult.Success:
                Managers.Inventory.UpdateItemData(unEquipItemPacket.UpdatedItem);
                Managers.UI.ShowToastPopup("장비를 해제했습니다.");
                break;
            case UnEquipResult.NotEquipped:
                Managers.UI.ShowToastPopup("해당 슬롯에 장착된 장비가 없습니다.");
                break;
            case UnEquipResult.InvalidSlot:
            default:
                Managers.UI.ShowToastPopup("장비를 해제할 수 없습니다.");
                break;
        }
    }

    public static void S_EquipItemHandler(PacketSession session, IMessage packet)
    {
        S_EquipItem equipItemPacket = packet as S_EquipItem;
        if (equipItemPacket == null)
        {
            Debug.Log("S_EquipItem 패킷이 null입니다");
            return;
        }

        switch (equipItemPacket.EquipResult)
        {
            case EquipResult.Success:
                foreach (ItemInfo itemInfo in equipItemPacket.UpdatedItems)
                {
                    Managers.Inventory.UpdateItemData(itemInfo);
                }
                Managers.UI.ShowToastPopup("장비를 장착했습니다.");
                break;
            case EquipResult.AlreadyEquipped:
                Managers.UI.ShowToastPopup("이미 착용 중인 장비입니다.");
                break;
            case EquipResult.LevelRestricted:
                Managers.UI.ShowToastPopup("레벨이 부족하여 장비를 착용할 수 없습니다.");
                break;
            case EquipResult.StatRestricted:
                Managers.UI.ShowToastPopup("스탯이 부족하여 장비를 착용할 수 없습니다.");
                break;
            case EquipResult.ClassRestricted:
                Managers.UI.ShowToastPopup("직업 제한으로 장비를 착용할 수 없습니다.");
                break;
            case EquipResult.InvalidItem:
            case EquipResult.NotOwned:
                Managers.UI.ShowToastPopup("장비를 착용할 수 없습니다.");
                break;
            default:
                Managers.UI.ShowToastPopup("알 수 없는 결과입니다.");
                break;
        }
    }

    public static void S_PickUpDropItemHandler(PacketSession session, IMessage packet)
    {
        S_PickUpDropItem pickUpDropItemPacket = packet as S_PickUpDropItem;
        if (pickUpDropItemPacket == null)
        {
            Debug.Log("S_PickUpDropItem 패킷이 null입니다");
            return;
        }

        switch (pickUpDropItemPacket.PickUpDropItemResult)
        {
            case PickUpDropItemResult.InventoryFull:
                Managers.UI.ShowToastPopup("인벤토리가 가득 차서 아이템을 획득할 수 없습니다.");
                return;
        }

        // TODO - 아이템이 사라지는 이펙트 보여주기
        GameObject go = Managers.GameRoomObject.FindById(pickUpDropItemPacket.ItemId);
        UI_DropItem dropItem = go?.GetComponent<UI_DropItem>();
        if (dropItem != null)
        {
            ItemMetaData item = Managers.SpecData.GetItem(dropItem.SpecItemId);
            Managers.GameRoomObject.Remove(pickUpDropItemPacket.ItemId, isDead: false);
        }
    }
}