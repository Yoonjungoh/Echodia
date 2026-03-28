using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;
using Server.Data;

namespace Server.Game.Object
{
    public class Bear : Monster
    {
        public Bear()
        {
            MonsterType = MonsterType.Bear;
            TemplateId = 30001;

            MonsterMetaData spec = SpecDataManager.Instance.GetMonster(TemplateId);
            if (spec != null)
            {
                Stat.MaxHp = spec.MaxHp;
                Stat.Hp = spec.MaxHp;
                Stat.CommonAttackDamage = spec.CommonAttackDamage;
                Stat.CommonAttackCoolTime = spec.CommonAttackCoolTime;
                Stat.AttackRange = spec.AttackRange;
                Stat.Defense = spec.Defense;
                Stat.MoveSpeed = spec.MoveSpeed;
                _searchRange = spec.SearchRange;
                _gold = spec.Gold;
                Level = spec.Level;
                SetExp(spec.Exp, needLevelUp: false);
                _respawnTime = spec.RespawnTime;
            }
            else
            {
                Console.WriteLine($"[Bear] MonsterMetaData not found for TemplateId={TemplateId}");
            }
        }
    }
}