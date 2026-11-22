using System;
using System.Collections.Generic;
using System.Data.Common;
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
		public Player MyPlayer { get; private set; }

		public int SessionId { get; set; }
        
        public int AccountId { get; private set; }  // DB Id (Account 테이블의 AccountDbId임)

        public int ServerId { get; private set; }   // 접속 중인 서버 Id
        
        public int ChannelId { get; private set; }  // 접속 중인 채널 Id

        private readonly object _lock = new object();
        private int _selectedPlayerId;
        private GameRoom _pendingEntryRoom;

        private bool CanChooseCharacter => !_loginClosed && AccountId > 0 &&
            ClientServerState == ClientServerState.PlayerSelect;

        public ClientServerState ClientServerState { get; private set; } = ClientServerState.Login;

        public void HandleDeletePlayer(int deletePlayerId)
        {
            lock (_lock)
            {
                if (!CanChooseCharacter)
                {
                    Send(new S_DeletePlayer { CanDelete = false });
                    return;
                }

                using (GameDbContext db = new GameDbContext())
                {
                    S_DeletePlayer serverDeletePlayerPacket = new S_DeletePlayer();
                    try
                    {
                        serverDeletePlayerPacket.CanDelete = db.OwnedPlayer(AccountId, deletePlayerId).ExecuteDelete() == 1;
                    }
                    catch (DbException ex)
                    {
                        ConsoleLogManager.Instance.Log(ex);
                    }
                    if (!serverDeletePlayerPacket.CanDelete)
                    {
                        Send(serverDeletePlayerPacket);
                        return;
                    }

                    var remainingPlayers = db.Players
                        .AsNoTracking()
                        .Where(p => p.AccountDbId == AccountId)
                        .Select(p => new { p.PlayerDbId, p.Name, p.Jewel, p.Gold });

                    foreach (var player in remainingPlayers)
                    {
                        PlayerSelectInfo info = new PlayerSelectInfo()
                        {
                            PlayerId = player.PlayerDbId,
                            Name = player.Name,
                            CurrencyData = new CurrencyData()
                            {
                                // TODO - 재화 자동화 필요
                                Jewel = player.Jewel,
                                Gold = player.Gold
                            }
                        };
                        serverDeletePlayerPacket.PlayerInfoList.Add(info);
                    }
                    Send(serverDeletePlayerPacket);
                }
            }
        }

        // 만들어진 캐릭터 선택 후 게임에 진입 (여기서 세션의 MyPlayer 세팅함)
        public void HandleEnterGame(int playerId, int serverId, int channelId)
        {
            lock (_lock)
            {
                if (_loginClosed || AccountId <= 0 || ClientServerState != ClientServerState.GameRoom ||
                    _selectedPlayerId != playerId || MyPlayer != null || ServerId != serverId || ChannelId != channelId)
                    return;

                using var db = new GameDbContext();
                var character = db.OwnedPlayer(AccountId, _selectedPlayerId)
                    .Select(p => new { p.PlayerDbId, p.Name, p.LastMapId })
                    .SingleOrDefault();
                if (character == null)
                    return;

                var rooms = ServerManager.Instance.FindChannel(ServerId, ChannelId)?.GameRoomManager;
                var room = rooms?.Find(character.LastMapId) ?? rooms?.Find(DataManager.Instance.DefaultStartMapId);
                if (room == null)
                    return;

                var player = ObjectManager.Instance.Add<Player>();
                try
                {
                    player.Init(character.PlayerDbId, character.Name, AccountId);
                    player.Session = this;
                    MyPlayer = player;
                    CheckAndAssignInitialQuest();
                }
                catch
                {
                    MyPlayer = null;
                    ObjectManager.Instance.Remove(player.Id);
                    throw;
                }

                QueueRoomEntry(room, player);
            }
        }

        internal void QueueRoomEntry(GameRoom room, Player player)
        {
            lock (_lock)
            {
                if (_loginClosed)
                {
                    ObjectManager.Instance.Remove(player.Id);
                    return;
                }
                _pendingEntryRoom = room;
                room.Push(() =>
                {
                    try
                    {
                        if (_loginClosed)
                            ObjectManager.Instance.Remove(player.Id);
                        else
                            room.EnterGame(player);
                    }
                    finally
                    {
                        lock (_lock)
                            _pendingEntryRoom = null;
                    }
                });
            }
        }

        public void HandleCreatePlayer(string name)
        {
            lock (_lock)
            {
                if (!CanChooseCharacter)
                {
                    ConsoleLogManager.Instance.Log($"[Warning] 캐릭터 선택창 상태가 아닌 곳에서 캐릭터 생성 시도. SessionId: {SessionId}");
                    return;
                }

                using (GameDbContext db = new GameDbContext())
                {
                    AccountDb account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == AccountId)
                        .FirstOrDefault();

                    if (account == null)
                    {
                        ConsoleLogManager.Instance.Log("[Error] DB에서 계정 정보를 찾을 수 없음");
                        return;
                    }

                    // 1. 캐릭터 이름 중복 검사
                    PlayerDb existingPlayer = db.Players
                        .Where(p => p.Name == name)
                        .FirstOrDefault();

                    S_CreatePlayer serverCreatePlayerPacket = new S_CreatePlayer();

                    if (existingPlayer != null)
                    {
                        // 중복이므로 생성 불가
                        serverCreatePlayerPacket.CanCreate = false;
                        Send(serverCreatePlayerPacket);
                        return;
                    }

                    // 2. 새 PlayerDb 생성
                    PlayerDb newPlayerDb = new PlayerDb(account.AccountDbId, name);

                    // 3. DB 저장
                    db.Players.Add(newPlayerDb);

                    // SaveChangesEx 성공하면 PlayerDbId인 PK 생성
                    {
                        bool success = db.SaveChangesEx();
                        if (success == false)
                            return;
                    }

                    // 혹시 모르니 최신 상태 다시 불러오기
                    account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == account.AccountDbId)
                        .FirstOrDefault();

                    // 4. 생성 성공 결과 패킷 전송
                    serverCreatePlayerPacket.CanCreate = true;

                    foreach (PlayerDb player in account.Players)
                    {
                        PlayerSelectInfo info = new PlayerSelectInfo()
                        {
                            PlayerId = player.PlayerDbId,
                            Name = player.Name,
                            // TODO - 재화 자동화 필요
                            CurrencyData = new CurrencyData()
                            {
                                Jewel = player.Jewel,
                                Gold = player.Gold
                            }
                        };

                        serverCreatePlayerPacket.PlayerInfoList.Add(info);
                    }

                    Send(serverCreatePlayerPacket);
                }
            }
        }

        public void HandleRequestPlayerList()
        {
            lock (_lock)
            {
                if (!CanChooseCharacter)
                {
                    ConsoleLogManager.Instance.Log($"[Warning] 캐릭터 선택창 상태가 아닌 곳에서 캐릭터 선택 시도. SessionId: {SessionId}");
                    return;
                }

                using (GameDbContext db = new GameDbContext())
                {
                    // Account + Players 데이터를 한 번에 로드해야 함
                    AccountDb account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == AccountId)
                        .FirstOrDefault();

                    if (account == null)
                    {
                        Console.WriteLine("[Error] 계정을 찾을 수 없음");
                        return;
                    }

                    // 1. 계정의 Player 목록
                    List<PlayerDb> playerList = account.Players?.ToList() ?? new List<PlayerDb>();

                    // 2. 패킷 생성
                    S_RequestPlayerList serverRequestPlayerList = new S_RequestPlayerList();

                    foreach (PlayerDb player in playerList)
                    {
                        PlayerSelectInfo info = new PlayerSelectInfo()
                        {
                            PlayerId = player.PlayerDbId,
                            Name = player.Name,
                            // TODO - 재화 자동화 필요
                            CurrencyData = new CurrencyData()
                            {
                                Jewel = player.Jewel,
                                Gold = player.Gold,
                                Exp = player.Exp,
                                Level = player.Level,
                            }
                        };

                        serverRequestPlayerList.PlayerInfoList.Add(info);
                    }

                    // 3. 클라로 전송
                    Send(serverRequestPlayerList);
                }
            }
        }
        
        private volatile bool _loginClosed;
        // Admission is bounded so login bursts leave CPU available for game rooms.
        static readonly SemaphoreSlim LoginSlots = new(Math.Max(1, Environment.ProcessorCount / 2));

        public void HandleLogin(C_Login loginPacket)
        {
            if (ClientServerState != ClientServerState.Login)
                return;
            if (!LoginSlots.Wait(0))
            {
                Send(new S_Login { LoginStatus = LoginStatus.None });
                return;
            }

            try
            {
                // Hashing and SQL must not hold the lock used by the network send loop.
                using var db = new GameDbContext();
                var (status, accountId) = AccountCredentials.Authenticate(db, loginPacket.Id, loginPacket.Password);
                lock (_lock)
                {
                    if (_loginClosed || ClientServerState != ClientServerState.Login)
                        return;
                    if (status == LoginStatus.Success)
                    {
                        if (AccountManager.Instance.Add(accountId))
                        {
                            AccountId = accountId;
                            ClientServerState = ClientServerState.ServerSelect;
                        }
                        else
                            status = LoginStatus.AlreadyLoggedIn;
                    }
                    Send(new S_Login { LoginStatus = status });
                }
            }
            finally
            {
                LoginSlots.Release();
            }
        }

        public void HandleRequestServerSummaryList()
        {
            lock (_lock)
            {
                S_RequestServerSummaryList requestServerSummaryListPacket = new S_RequestServerSummaryList();
                requestServerSummaryListPacket.ServerInfoList.AddRange(ServerManager.Instance.ServerSummaryList);
                Send(requestServerSummaryListPacket);
            }
        }

        public void HandleRequestServerList(int serverId)
        {
            lock (_lock)
            {
                S_RequestServerList requestServerListPacket = new S_RequestServerList();
                requestServerListPacket.ServerInfoList.Add(ServerManager.Instance.GetServerInfoList(serverId));
                requestServerListPacket.ServerId = serverId;
                Send(requestServerListPacket);
            }
        }

        public void HandleSelectServer(int serverId, int channelId)
        {
            lock (_lock)
            {
                if (_loginClosed || AccountId <= 0 || ClientServerState != ClientServerState.ServerSelect)
                {
                    Send(new S_SelectServer { ServerId = serverId, ChannelId = channelId, EnterServerResult = EnterServerResult.InvalidPlayer });
                    return;
                }
                // 서버/채널 존재하는지 확인 후 패킷 보내기
                S_SelectServer selectServerPacket = new S_SelectServer();
                ServerChannel channel = ServerManager.Instance.FindChannel(serverId, channelId);
                if (channel == null)
                {
                    ConsoleLogManager.Instance.Log($"[Error] 채널 정보를 찾을 수 없음. SessionId: {SessionId}, ServerId: {serverId}, ChannelId: {channelId}");
                    return;
                }
                
                // 채널 입장 작업
                channel.TryEnterChannel(SessionId, out EnterServerResult enterServerResult);

                if (enterServerResult == EnterServerResult.Success)
                {
                    ServerId = serverId;
                    ChannelId = channelId;
                    ClientServerState = ClientServerState.PlayerSelect;
                    ServerManager.Instance.BroadcastChannelPlayerCountChanged(serverId);
                }
                selectServerPacket.ServerId = serverId;
                selectServerPacket.ChannelId = channelId;
                selectServerPacket.EnterServerResult = enterServerResult;
                Send(selectServerPacket);
            }
        }

        public void HandleSelectPlayer(int playerId, int serverId, int channelId)
        {
            lock (_lock)
            {
                if (!CanChooseCharacter || ServerId != serverId || ChannelId != channelId)
                {
                    Send(new S_SelectPlayer { CanSelect = false });
                    return;
                }

                using var db = new GameDbContext();
                if (!db.OwnedPlayer(AccountId, playerId).Any())
                {
                    Send(new S_SelectPlayer { CanSelect = false });
                    return;
                }

                _selectedPlayerId = playerId;
                ClientServerState = ClientServerState.GameRoom;
                Send(new S_SelectPlayer { PlayerId = playerId, CanSelect = true });
            }
        }

    }
}
