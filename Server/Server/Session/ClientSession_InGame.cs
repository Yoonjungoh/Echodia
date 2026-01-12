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

namespace Server
{
    // PreGame 관련 핸들러들 (로그인, 캐릭터 선택까지를 PreGame이라 칭하자)
    public partial class ClientSession : PacketSession
    {
        public void HandleUpdateCurrencyDataAll(int playerId)
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    PlayerDb player = db.Players
                        .AsNoTracking()
                        .Where(p => p.PlayerId == playerId)
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
                        Gold = player.Gold
                    };
                    updateCurrencyDataAllPacket.CurrencyData = currencyData;

                    Send(updateCurrencyDataAllPacket);
                }
            }
        }

        public void HandleUpdateCurrencyData(int playerId, CurrencyType currencyType)
        {
            lock (_lock)
            {
                using (GameDbContext db = new GameDbContext())
                {
                    PlayerDb player = db.Players
                        .AsNoTracking()
                        .Where(p => p.PlayerId == playerId)
                        .FirstOrDefault();

                    if (player == null)
                    {
                        Console.WriteLine("[Error] 캐릭터를 찾을 수 없음");
                        return;
                    }

                    S_UpdateCurrencyData updateCurrencyDataAllPacket = new S_UpdateCurrencyData();
                    updateCurrencyDataAllPacket.CurrencyType = currencyType;
                    // TODO - 재화 자동화 필요
                    switch (currencyType)
                    {
                        case CurrencyType.Jewel:
                            updateCurrencyDataAllPacket.Amount = player.Jewel;
                            break;
                        case CurrencyType.Gold:
                            updateCurrencyDataAllPacket.Amount = player.Gold;
                            break;
                        default:
                            Console.WriteLine("[Error] 알 수 없는 재화 타입");
                            return;
                    }

                    Send(updateCurrencyDataAllPacket);
                }
            }
        }
    }
}
