using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ServerCore;
using System.Net;
using Google.Protobuf.Protocol;
using Google.Protobuf;
using Server.Game;
using Server.DB;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Server.Session;
using System.Collections;
using Server.Data;

namespace Server
{
    // PreGame 관련 핸들러들 (로그인, 캐릭터 선택까지를 PreGame이라 칭하자)
    public partial class ClientSession : PacketSession
    {
        private void ChangeQuestStatus(int mainQuestId, int subQuestId, QuestStatus questStatus)
        {
            S_ChangeQuestStatus questStatusPacket = new S_ChangeQuestStatus()
            {
                MainQuestId = mainQuestId,
                SubQuestId = subQuestId,
                QuestStatus = questStatus,
            };
            Send(questStatusPacket);
        }

        public void AcceptQuest(int mainQuestId, int subQuestId)
        {
            DbTransaction.UpdateQuestStatus(MyPlayer.PlayerId, mainQuestId, subQuestId, QuestStatus.Proceeding,
            () =>
            {
                ChangeQuestStatus(mainQuestId, subQuestId, QuestStatus.Proceeding);
                // DB가 Proceeding으로 바뀐 뒤 QuestTracker에 킬 퀘스트 등록
                MyPlayer.GameRoom?.Push(() => MyPlayer.QuestTracker.Load());
            });
        }

        public void ClaimQuestReward(int mainQuestId, int subQuestId)
        {
            QuestManager.Instance.ClaimReward(MyPlayer, mainQuestId, subQuestId);
        }

        public void CheckAndAssignInitialQuest()
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    // 1. 해당 유저의 메인 퀘스트가 하나라도 있는지 확인
                    bool hasQuest = db.Quests.Any(q => q.PlayerDbId == MyPlayer.PlayerId);
                    // 2. 없으면 초반 퀘스트 실행
                    if (hasQuest == false)
                    {
                        PlayerDb playerDb = db.Players
                                            .Where(p => p.PlayerDbId == MyPlayer.PlayerId)
                                            .FirstOrDefault();
                        int mainQuestId = ConfigManager.Instance.GetInt(ConfigType.DefaultCreationMainQuestId);
                        int subQuestId = ConfigManager.Instance.GetInt(ConfigType.DefaultCreationSubQuestId);

                        QuestManager.Instance.CreateQuest(db, playerDb, this, mainQuestId, subQuestId);

                        Console.WriteLine($"[Quest] Player {MyPlayer.PlayerId} assigned to initial quest.");
                    }
                }
            }
        }

        public void SendQuestList()
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    List<QuestInfo> questList = db.Quests
                        .AsNoTracking()
                        .Where(q => q.PlayerDbId == MyPlayer.PlayerId)
                        // Include로 다 가져오는 것보다 Select로 필요한 컬럼만 쿼리에 넣기
                        .Select(q => new QuestInfo
                        {
                            MainQuestId = q.MainQuestId,
                            SubQuestId = q.SubQuestId,
                            RequiredCount = q.RequiredCount,
                            QuestStatus = q.Status,
                        })
                        .ToList();

                    if (questList == null)
                        return;

                    S_RequestQuestData requestQuestDataPacket = new S_RequestQuestData();
                    requestQuestDataPacket.QuestInfoList.AddRange(questList);

                    Send(requestQuestDataPacket);
                }
            }
        }

        // 인메모리 CurrencyTracker에서 전체 재화 조회 (DB 접근 없음)
        public void HandleUpdateCurrencyDataAll()
        {
            if (MyPlayer?.CurrencyTracker == null)
                return;

            S_UpdateCurrencyDataAll updateCurrencyDataAllPacket = new S_UpdateCurrencyDataAll();
            updateCurrencyDataAllPacket.CurrencyData = new CurrencyData()
            {
                Jewel = MyPlayer.CurrencyTracker.Get(CurrencyType.Jewel),
                Gold  = MyPlayer.CurrencyTracker.Get(CurrencyType.Gold),
                Exp   = MyPlayer.CurrencyTracker.Get(CurrencyType.Exp),
                Level = MyPlayer.CurrencyTracker.Get(CurrencyType.Level),
            };
            Send(updateCurrencyDataAllPacket);
        }

        // 인메모리 CurrencyTracker에서 특정 재화 조회 (DB 접근 없음, switch 불필요)
        public void HandleUpdateCurrencyData(CurrencyType currencyType)
        {
            if (MyPlayer?.CurrencyTracker == null)
                return;

            Send(new S_UpdateCurrencyData
            {
                CurrencyType = currencyType,
                Amount = MyPlayer.CurrencyTracker.Get(currencyType),
            });
        }

        public void HandleRequestInitGameRoomData(int playerId)
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    PlayerDb player = db.Players
                        .AsNoTracking()
                        .Where(p => p.PlayerDbId == playerId)
                        .FirstOrDefault();

                    if (player == null)
                    {
                        ConsoleLogManager.Instance.Log($"[Error] 캐릭터 정보를 찾을 수 없음. PlayerId: {playerId}");
                        return;
                    }

                    // 레벨/경험치 초기 데이터
                    S_RequestInitGameRoomData initGameRoomDataPacket = new S_RequestInitGameRoomData();
                    initGameRoomDataPacket.InitGameRoomData = new InitGameRoomData()
                    {
                        Level = player.Level,
                        Exp = player.Exp,
                        MaxExp = SpecDataManager.Instance.GetExpRequiredForLevel(player.Level),
                    };
                    Send(initGameRoomDataPacket);

                    // 전체 인벤토리 전송
                    if (MyPlayer?.Items != null)
                    {
                        S_UpdateItemDataAll itemDataAllPacket = new S_UpdateItemDataAll();
                        foreach (PlayerItemDb item in MyPlayer.Items.Values)
                        {
                            itemDataAllPacket.ItemInfoList.Add(new ItemInfo
                            {
                                ItemId = item.ItemId,
                                Count = item.Count,
                                SlotIndex = item.SlotIndex,
                                IsEquipped = item.IsEquipped,
                                EnchantLevel = item.EnchantLevel,
                            });
                        }
                        Send(itemDataAllPacket);
                    }

                    // 전체 재화 전송 (CurrencyTracker 인메모리 데이터 활용, DB 접근 없음)
                    if (MyPlayer?.CurrencyTracker != null)
                    {
                        S_UpdateCurrencyDataAll currencyDataAllPacket = new S_UpdateCurrencyDataAll();
                        currencyDataAllPacket.CurrencyData = new CurrencyData()
                        {
                            Jewel = MyPlayer.CurrencyTracker.Get(CurrencyType.Jewel),
                            Gold  = MyPlayer.CurrencyTracker.Get(CurrencyType.Gold),
                            Exp   = MyPlayer.CurrencyTracker.Get(CurrencyType.Exp),
                            Level = MyPlayer.CurrencyTracker.Get(CurrencyType.Level),
                        };
                        Send(currencyDataAllPacket);
                    }
                }
            }
        }
    }
}
