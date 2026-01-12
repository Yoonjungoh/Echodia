using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Server.DB;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
	public class Player : GameObject
    {
        public Player()
        {
            Init();
        }

        public Lobby Lobby;	// 로비 나가면 초기화 해줘야 함
			
        public WaitingRoom WaitingRoom;	// 방 나가면 초기화 해줘야 함
        public int PlayerId { get; set; }   // DB에 저장된 플레이어 고유 Id
        public ClientSession Session { get; set; }
        public AOIController AOI { get; set; }

        // 플레이어 정보 초기화
        public void Init()
        {
            ObjectType = GameObjectType.Player;
            Name = $"NameNull_Player_{ObjectState.ObjectId}";
            ObjectState.CreatureState = CreatureState.Idle;
            AOI = new AOIController(this);

            InitStat();
        }

        public void Init(int playerId, string name)
		{
			ObjectType = GameObjectType.Player;
            Name = name;
            PlayerId = playerId;
            ObjectState.CreatureState = CreatureState.Idle;

			InitStat();
		}

		// TODO JSON - PlayerType에 따른 stat 변경
		public void InitStat()
		{
            if (ObjectState.Stat == null)
            {
                ObjectState.Stat = new Stat();
            }

            ObjectState.Stat.MaxHp = 100.0f;
            ObjectState.Stat.Hp = ObjectState.Stat.MaxHp;
            ObjectState.Stat.CommonAttackDamage = 30.0f;
            ObjectState.Stat.Defense = 0.0f;
            ObjectState.Stat.MoveSpeed = 7.0f;
            ObjectState.Stat.CommonAttackCoolTime = 2.0f;
            ObjectState.Stat.AttackRange = 10.0f;
            ObjectState.Stat.AttackHalfAngleDeg = 30.0f;
            ObjectState.Stat.AttackHeight = 10.0f;
        }

        public override void OnDamaged(GameObject instigator, float damage)
        {
            base.OnDamaged(instigator, damage);

        }

        public void OnLeaveGame()
        {

        }
    }
}
