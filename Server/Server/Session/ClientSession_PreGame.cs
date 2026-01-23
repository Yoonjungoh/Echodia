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
		public Player MyPlayer { get; set; }

		public int SessionId { get; set; }
        
        public int AccountId { get; set; }  // DB Id (Account 테이블의 AccountDbId임)

        public int ServerId { get; set; }   // 접속 중인 서버 Id
        
        public int ChannelId { get; set; }  // 접속 중인 채널 Id

        private object _lock = new object();

        public ClientServerState ClientServerState { get; private set; } = ClientServerState.Login;

        public void HandleDeletePlayer(int deletePlayerId)
        {
            lock (_lock)
            {
                if (ClientServerState != ClientServerState.PlayerSelect)
                {
                    ConsoleLogManager.Instance.Log($"[Warning] 플레이어 선택창 상태가 아닌 곳에서 캐릭터 삭제 시도. SessionId: {SessionId}");
                    return;
                }

                using (GameDbContext db = new GameDbContext())
                {
                    // 1. DB 로드
                    AccountDb account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == AccountId)
                        .FirstOrDefault();

                    if (account == null)
                    {
                        ConsoleLogManager.Instance.Log("[Error] 캐릭터 삭제 실패: 계정 정보 없음");
                        return;
                    }

                    // 2. 삭제하려는 캐릭터 찾기
                    PlayerDb target = db.Players
                        .Where(p => p.PlayerId == deletePlayerId)
                        .FirstOrDefault();

                    S_DeletePlayer serverDeletePlayerPacket = new S_DeletePlayer();

                    if (target == null)
                    {
                        ConsoleLogManager.Instance.Log($"[Error] 캐릭터 삭제 실패: AccountId={AccountId}, PlayerId={deletePlayerId} 없음");
                        serverDeletePlayerPacket.CanDelete = false;
                        Send(serverDeletePlayerPacket);
                        return;
                    }

                    // 3. DB에서 삭제
                    bool canDelete = db.Players.Contains(target);
                    serverDeletePlayerPacket.CanDelete = canDelete;
                    db.Players.Remove(target);

                    bool success = db.SaveChangesEx();
                    if (success == false)
                    {
                        Send(serverDeletePlayerPacket);
                        return;
                    }

                    // 4. 최신 캐릭터 목록 다시 로드
                    account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == AccountId)
                        .FirstOrDefault();


                    foreach (PlayerDb player in account.Players)
                    {
                        PlayerSelectInfo info = new PlayerSelectInfo()
                        {
                            PlayerId = player.PlayerId,
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
        public Player EnterGame(int playerId)
        {
            lock (_lock)
            {
                // enterLobbyPacket에 있는 PlayerId 활용해서 DB에서 찾은 다음 user 생성 후 넣기
                // 로비에 진입한 플레이어의 고유 아이디 (Player 테이블의 PlayerId)
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
                        return null;
                    }

                    // 1. 플레이어 찾기
                    PlayerDb player = db.Players
                        .Where(p => p.PlayerId == playerId)
                        .FirstOrDefault();
                    if (player == null)
                    {
                        ConsoleLogManager.Instance.Log($"[Error] DB에서 플레이어 정보를 찾을 수 없음 PlayerId: {playerId}");
                        return null;
                    }

                    // 2. 해당 플레이어 데이터를 바탕으로 현재 클라이언트 세션의 MyPlayer 생성
                    MyPlayer = ObjectManager.Instance.Add<Player>();
                    MyPlayer.Init(player.PlayerId, player.Name);
                    MyPlayer.Session = this;
                }

                return MyPlayer;
            }
        }

        public void HandleCreatePlayer(string name)
        {
            lock (_lock)
            {
                if (ClientServerState != ClientServerState.PlayerSelect)
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

                    // 2. PlayerId 자동 생성 (모든 계정의 전체 캐릭터 수 기반)
                    int newPlayerId = db.Players.Any() ? db.Players.Max(p => p.PlayerId) + 1 : 1;

                    // 3. 새 PlayerDb 생성
                    PlayerDb newPlayerDb = new PlayerDb()
                    {
                        AccountDbId = account.AccountDbId,
                        PlayerId = newPlayerId,
                        Name = name,
                        Jewel = 0,
                        Gold = 1000
                    };

                    // 4. DB 저장
                    db.Players.Add(newPlayerDb);

                    bool success = db.SaveChangesEx();
                    if (success == false)
                        return;

                    // 혹시 모르니 최신 상태 다시 불러오기
                    account = db.Accounts
                        .AsNoTracking()
                        .Include(a => a.Players)
                        .Where(a => a.AccountDbId == account.AccountDbId)
                        .FirstOrDefault();

                    // 5. 생성 성공 결과 패킷 전송
                    serverCreatePlayerPacket.CanCreate = true;

                    foreach (PlayerDb player in account.Players)
                    {
                        PlayerSelectInfo info = new PlayerSelectInfo()
                        {
                            PlayerId = player.PlayerId,
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
                if (ClientServerState != ClientServerState.PlayerSelect)
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
                            PlayerId = player.PlayerId,
                            Name = player.Name,
                            // TODO - 재화 자동화 필요
                            CurrencyData = new CurrencyData()
                            {
                                Jewel = player.Jewel,
                                Gold = player.Gold
                            }
                        };

                        serverRequestPlayerList.PlayerInfoList.Add(info);
                    }

                    // 3. 클라로 전송
                    Send(serverRequestPlayerList);
                }
            }
        }
        
        public void HandleLogin(C_Login loginPacket)
        {
            lock (_lock)
            {
                ConsoleLogManager.Instance.Log($"AccountId: {loginPacket.Id}, Password: {loginPacket.Password}");

                if (ClientServerState != ClientServerState.Login)
                {
                    ConsoleLogManager.Instance.Log($"[Warning] 로그인 상태가 아닌 곳에서 로그인 시도. SessionId: {SessionId}");
                    return;
                }

                using (GameDbContext db = new GameDbContext())
                {
                    // 1. 아이디 존재하는지 먼저 확인
                    AccountDb findAccount = db.Accounts
                        .AsNoTracking()
                        .Where(a => a.AccountId == loginPacket.Id)
                        .FirstOrDefault();

                    S_Login serverLoginPacket = new S_Login();
                    
                    // 2. 계정이 존재하는 경우
                    if (findAccount != null)
                    {
                        // 2-1. 비밀번호만 틀림
                        if (findAccount.AccountPassword != loginPacket.Password)
                        {
                            serverLoginPacket.LoginStatus = LoginStatus.PasswordWrong;
                            Send(serverLoginPacket);
                            return;
                        }

                        // 2-2. 이미 로그인 중인 아이디면 접속 못함
                        if (AccountManager.Instance.IsAccountLoggedIn(findAccount.AccountDbId))
                        {
                            serverLoginPacket.LoginStatus = LoginStatus.AlreadyLoggedIn;
                            Send(serverLoginPacket);
                            return;
                        }

                        // 2-3. 비밀번호 확인 성공 후, 로그인 처리
                        AccountManager.Instance.Add(findAccount.AccountDbId);
                        ClientServerState = ClientServerState.ServerSelect;
                        AccountId = findAccount.AccountDbId;
                        serverLoginPacket.LoginStatus = LoginStatus.Success;
                        Send(serverLoginPacket);
                        return;
                    }

                    // 3. TODO - 계정이 없으면 새로운 계정 생성 (개발 환경에선 편하지만 라이브 때는 공식 회원가입 시키기)
                    AccountDb newAccount = new AccountDb()
                    {
                        AccountId = loginPacket.Id,
                        AccountPassword = loginPacket.Password
                    };

                    db.Accounts.Add(newAccount);

                    bool success = db.SaveChangesEx();
                    if (success == false)
                        return;

                    // 4. 회원 가입 성공 의미 
                    ClientServerState = ClientServerState.Login;
                    AccountId = newAccount.AccountDbId;
                    serverLoginPacket.LoginStatus = LoginStatus.SignUpSuccess;
                    Send(serverLoginPacket);
                }
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
                // TODO - 캐릭터도 존재하는지 DB 뒤져서 미리 알아봐도 좋을듯?
                // 지금은 부하 생각해서 패스
                S_SelectPlayer selectPlayerPacket = new S_SelectPlayer();
                if (ServerId ==  serverId && ChannelId == channelId)
                {
                    // 패킷 전송
                    selectPlayerPacket.PlayerId = playerId;
                    selectPlayerPacket.CanSelect = true;
                    Send(selectPlayerPacket);
                    return;
                }
                
                ConsoleLogManager.Instance.Log($"[Warning] 잘못된 서버/채널 선택 시도. SessionId: {SessionId}, ServerId: {serverId}, ChannelId: {channelId}");
                selectPlayerPacket.CanSelect = false;
                Send(selectPlayerPacket);
            }
        }
    }
}
