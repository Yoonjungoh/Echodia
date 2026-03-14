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

        public void HandleUpdateCurrencyDataAll()
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    PlayerDb player = db.Players
                        .AsNoTracking()
                        .Where(p => p.PlayerDbId == MyPlayer.PlayerId)
                        .FirstOrDefault();

                    if (player == null)
                    {
                        Console.WriteLine("[Error] 캐릭터를 찾을 수 없음");
                        return;
                    }

                    S_UpdateCurrencyDataAll updateCurrencyDataAllPacket = new S_UpdateCurrencyDataAll();
                    // TODO - 재화 자동화 필요
                    CurrencyData currencyData = new CurrencyData()
                    {
                        Jewel = player.Jewel,
                        Gold = player.Gold,
                        Exp = player.Exp,
                        Level = player.Level
                    };
                    updateCurrencyDataAllPacket.CurrencyData = currencyData;

                    Send(updateCurrencyDataAllPacket);
                }
            }
        }

        public void HandleUpdateCurrencyData(CurrencyType currencyType)
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    PlayerDb player = db.Players
                        .AsNoTracking()
                        .Where(p => p.PlayerDbId == MyPlayer.PlayerId)
                        .FirstOrDefault();

                    if (player == null)
                    {
                        Console.WriteLine("[Error] 캐릭터를 찾을 수 없음");
                        return;
                    }

                    S_UpdateCurrencyData updateCurrencyDataPacket = new S_UpdateCurrencyData();
                    updateCurrencyDataPacket.CurrencyType = currencyType;
                    // TODO - 재화 자동화 필요
                    switch (currencyType)
                    {
                        case CurrencyType.Jewel:
                            updateCurrencyDataPacket.Amount = player.Jewel;
                            break;
                        case CurrencyType.Gold:
                            updateCurrencyDataPacket.Amount = player.Gold;
                            break;
                        case CurrencyType.Exp:
                            updateCurrencyDataPacket.Amount = player.Exp;
                            break;
                        case CurrencyType.Level:
                            updateCurrencyDataPacket.Amount = player.Level;
                            break;
                        default:
                            Console.WriteLine("[Error] 알 수 없는 재화 타입");
                            return;
                    }

                    Send(updateCurrencyDataPacket);
                }
            }
        }
    }
}
