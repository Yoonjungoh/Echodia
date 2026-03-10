using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;

namespace Server.Game.Object
{
    public class Bear : Monster
    {
        public Bear()
        {
            MonsterType = MonsterType.Bear;

            // TODO - 데이터 시트에서 파싱
            Stat.MaxHp = 50;
            Stat.Hp = Stat.MaxHp;
            Stat.CommonAttackDamage = 15;
            Stat.CommonAttackCoolTime = 3;
            Stat.AttackRange = 4;
            Stat.Defense = 0;
            Stat.MoveSpeed = 7;
            _searchRange = 7f;
            ObjectUId = 30001;
            
            _gold = 15;
            Level = 1;
            SetExp(500, needLevelUp: false);
            _respawnTime = 5f;
        }
    }
}