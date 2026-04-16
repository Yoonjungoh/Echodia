using Google.Protobuf.Protocol;
using Server.Currency;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server.Game
{
    public class GameObject
    {
        public GameObjectType ObjectType { get; protected set; } = GameObjectType.None;

        // 오브젝트 전용 Id (계정 Id와 별개임, 휘발성이 있음)
        public int Id
        {
            get { return ObjectState.ObjectId; }
            set { ObjectState.ObjectId = value; }
        }
        public int TemplateId { get; set; }  // 데이터 시트에 명시된 고유 Id
        public GameRoom GameRoom { get; set; }

        public ObjectState ObjectState { get; set; }
        public string Name { get { return ObjectState.Name; } set { ObjectState.Name = value; } }
        public ProtoVector3 Position { get { return ObjectState.Position; } set { ObjectState.Position = value; } }
        public ProtoQuaternion Rotation { get { return ObjectState.Rotation; } set { ObjectState.Rotation = value; } }
        public ProtoVector3 Velocity { get { return ObjectState.Velocity; } set { ObjectState.Velocity = value; } }
        public Vector3 CurrentPosition
        {
            get
            {
                return MovementHelper.PredictPosition(
                MovementHelper.ProtoVec3ToVec3(Position),
                MovementHelper.ProtoVec3ToVec3(Velocity),
                ObjectState.ServerReceivedTime,
                Util.GetTimestampMs()
                );
            }
        }
        public CreatureState CreatureState { get { return ObjectState.CreatureState; } set { ObjectState.CreatureState = value; } }
        public Stat Stat { get { return ObjectState.Stat; } set { ObjectState.Stat = value; } }
        public MonsterType MonsterType { get { return ObjectState.MonsterType; } set { ObjectState.MonsterType = value; } }
        public ProjectileType ProjectileType { get { return ObjectState.ProjectileType; } set { ObjectState.ProjectileType = value; } }
        public int OwnerId { get { return ObjectState.OwnerId; } set { ObjectState.OwnerId = value; } }
        public int Level { get { return ObjectState.Level; } set { ObjectState.Level = value; } }
        public int Exp { get { return ObjectState.Exp; } set { ObjectState.Exp = value; } }
        public float ProjectileSpawnOffset { get; set; } = 3.0f;   // 투사체 소환 오프셋 (기본은 주인 중앙에 스폰됨)
        public ProtoVector3 SpawnPosition { get; set; } // 최초 소환 됐을 때 위치

        public GameObject()
        {
            ObjectState = new ObjectState();
            ObjectState.Position = new ProtoVector3();
            ObjectState.Velocity = new ProtoVector3();
            ObjectState.Rotation = new ProtoQuaternion();
            ObjectState.Stat = new Stat();
            ObjectState.CreatureState = new CreatureState();
        }

        public virtual void Update()
        {

        }

        public virtual void OnDamaged(GameObject instigator, int damage)
        {
            if (GameRoom == null)
                return;

            // 남은 체력 계산
            SetHp(ObjectState.Stat.Hp - damage);

            if (ObjectState.Stat.Hp <= 0)
            {
                // 죽음 처리 부분
                OnDead(instigator);
            }
        }

        public virtual void OnDead(GameObject instigator)
        {
            if (GameRoom == null)
                return;

            ConsoleLogManager.Instance.Log($"Id: {Id}, Type: {ObjectType} is dead");
            CreatureState = CreatureState.Die;

            S_Die diePacket = new S_Die();
            diePacket.DamagedObjectId = Id;
            diePacket.InstigatorId = instigator.Id;
            diePacket.CreatureState = CreatureState;

            // 경험치 올려주는 부분
            // 플레이어 본인이 죽였거나 본인의 투사체가 죽여야 본인이 죽인걸로 인정
            Player player = null;
            bool isInstigatorPlayer = false;
            if ((instigator.ObjectType == GameObjectType.Player && ObjectType == GameObjectType.Monster))
            {
                player = instigator as Player;
                isInstigatorPlayer = true;
            }
            if (isInstigatorPlayer == false && instigator.ObjectType == GameObjectType.Projectile)
            {
                Projectile projectile = instigator as Projectile;
                Player owner = GameRoom.Find(projectile.OwnerId);
                if (owner != null)
                {
                    player = owner;
                    isInstigatorPlayer = true;
                }
            }
            if (isInstigatorPlayer && player != null)
            {
                int totalExp = player.Exp + Exp;
                int levelUpAmount = player.SetExp(totalExp, needLevelUp: true);

                // 레벨업 했으면, 디비 저장 끝내고 레벨, 경험치 패킷 같이 전송
                // 레벨업 안 했으면, 디비 저장 끝내고 경험치 패킷만 전송
                CurrencyManager.Instance.SetCurrency(player, CurrencyType.Exp, player.Exp);
                ConsoleLogManager.Instance.Log($"Player {player.Name} gained {Exp} EXP. Total EXP: {player.Exp}");

                if (levelUpAmount > 0)
                {
                    // 레벨업 횟수는 카운팅이 퀘스트 연동면에서도 좋아보임
                    CurrencyManager.Instance.AddCurrency(player, CurrencyType.Level, levelUpAmount);
                    ConsoleLogManager.Instance.Log($"Player {player.Name} leveled up! New Level: {player.Level}");
                }
            }

            if (GameRoom != null)
            {
                GameRoom.Push(GameRoom.Broadcast, CurrentPosition, diePacket);
                GameRoom.Push(GameRoom.LeaveGame, Id);
            }
        }

        public void HealHp(int healAmount)
        {
            SetHp(ObjectState.Stat.Hp + healAmount);

            // 패킷 전송
            S_HealHp healPacket = new S_HealHp();
            healPacket.ObjectId = Id;
            healPacket.HealAmount = healAmount;
            healPacket.TotalHp = ObjectState.Stat.Hp;

            GameRoom.Push(GameRoom.Broadcast, CurrentPosition, healPacket);
        }

        public void SetHp(int hp)
        {
            Stat.Hp = hp;
        }

        // 마나 회복 (포션, 재생 등) — 최대 마나를 초과하지 않도록 클램핑
        public void HealMp(int healAmount)
        {
            SetMp(ObjectState.Stat.Mp + healAmount);

            // 마나 변경 패킷 전송
            S_ChangeMp changeMpPacket = new S_ChangeMp();
            changeMpPacket.ObjectId = Id;
            changeMpPacket.ChangeAmount = healAmount;
            changeMpPacket.TotalMp = ObjectState.Stat.Mp;

            GameRoom.Push(GameRoom.Broadcast, CurrentPosition, changeMpPacket);
        }

        // 스킬 사용 시 마나 소모 — 마나가 부족하면 false 반환
        public bool ConsumeMp(int amount)
        {
            if (ObjectState.Stat.Mp < amount)
            {
                return false;
            }

            SetMp(ObjectState.Stat.Mp - amount);

            // 마나 변경 패킷 전송 (소모이므로 음수)
            S_ChangeMp changeMpPacket = new S_ChangeMp();
            changeMpPacket.ObjectId = Id;
            changeMpPacket.ChangeAmount = -amount;
            changeMpPacket.TotalMp = ObjectState.Stat.Mp;

            GameRoom.Push(GameRoom.Broadcast, CurrentPosition, changeMpPacket);
            return true;
        }

        // 마나 직접 설정 — 0 ~ MaxMp 범위로 클램핑
        public void SetMp(int mp)
        {
            Stat.Mp = Math.Clamp(mp, 0, Stat.MaxMp);
        }

        // LevelUp 개수를 반환
        public int SetExp(int exp, bool needLevelUp)
        {
            int levelUpAmount = 0;
            Exp = exp;
            // 몬스터 같은 경우는 레벨업 시키지 않고 Exp만 세팅해줄 거라 넣은 플래그 값
            if (needLevelUp == false)
                return levelUpAmount;

            // 현재 단계에서 레벨업에 필요한 경험치량
            int expToLevelUp = SpecDataManager.Instance.GetExpRequiredForLevel(Level);
            while (expToLevelUp > 0 && expToLevelUp <= Exp)
            {
                Exp -= expToLevelUp;
                LevelUp(1);
                expToLevelUp = SpecDataManager.Instance.GetExpRequiredForLevel(Level);
                ++levelUpAmount;
            }
            return levelUpAmount;
        }

        public void LevelUp(int level)
        {
            Level += level;
            ConsoleLogManager.Instance.Log($"Player {Name} leveled up! New Level: {Level}");
        }
    }
}