using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Server.DB
{
    // 클래스 이름이 아닌 어노테이션 이름으로 테이블 만들어줌
    [Table("Account")]
    public class AccountDb
    {
        public int AccountDbId { get; set; }    // ByConvention 방법으로 클래스 이름에 Id 붙이면 자동으로 pk됨
        public string AccountId { get; set; } // 일단은 Unique 하게 설정 (Index 해줌)
        public string AccountPassword { get; set; } // 일단은 Unique 하게 설정 (Index 해줌)
        public ICollection<PlayerDb> Players { get; set; }  // 이렇게 하면 Player 테이블에 FK 컬럼이 생성됨 
    }

    [Table("Player")]
    public class PlayerDb
    {
        public PlayerDb() { }
        public PlayerDb(int accountDbId, string name)
        {
            // TODO - JSON
            AccountDbId = accountDbId;
            Name = name;
            Jewel = 0;
            Gold = 1000;
            LastPosX = int.MinValue;
            LastPosY = int.MinValue;
            LastPosZ = int.MinValue;
            Exp = 0;
            Level = 1;
        }
        public int PlayerDbId { get; set; } // 게임 내에서 사용하는 고유 Id (ObjectManager에서 사용하는 Id는 다른 거임)
        [ForeignKey("Account")]
        public int AccountDbId { get; set; }  // FK 컬럼
        public AccountDb Account { get; set; }
        public string Name { get; set; }  // 게임 내에서 사용하는 닉네임
        public ICollection<QuestDb> Quests { get; set; } = new List<QuestDb>(); // 플레이어가 진행중인 퀘스트 목록

        #region 재화
        // TODO - 재화 자동화 필요
        public int Jewel { get; set; }
        public int Gold { get; set; }
        public int Exp { get; set; }
        public int Level { get; set; }
        #endregion

        #region 재접속 시 위치
        public float LastPosX { get; set; }
        public float LastPosY { get; set; }
        public float LastPosZ { get; set; }
        // 코드에서 편하게 쓰기 위한 가상 프로퍼티 (DB에는 저장 안 함)
        [NotMapped]
        public Vector3 LastLogoutPosition
        {
            get { return new Vector3(LastPosX, LastPosY, LastPosZ); }
            set
            {
                LastPosX = value.X;
                LastPosY = value.Y;
                LastPosZ = value.Z;
            }
        }
        #endregion
    }

    [Table("Quest")]
    public class QuestDb
    {
        public int QuestDbId { get; set; } // PK
        [ForeignKey("Player")]
        public int PlayerDbId { get; set; } // FK
        public PlayerDb Player { get; set; }
        public int MainQuestId { get; set; } // 메인 퀘스트 번호
        public int SubQuestId { get; set; } // 서브 퀘스트 번호
        public int RequiredCount { get; set; }  // 퀘스트 클리어에 필요한 목표 수량
        public QuestStatus Status { get; set; } // 진행 상태 (Enum)
        public DateTime StartedDate { get; set; } = DateTime.UtcNow;
        public DateTime ClearedDate { get; set; }  // 완료 전엔 null
    }
}
