using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
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
        public int PlayerDbId { get; set; }

        [ForeignKey("Account")]
        public int AccountDbId { get; set; }  // FK 컬럼
        public AccountDb Account { get; set; }

        public int PlayerId { get; set; }  // 게임 내에서 사용하는 고유 Id (ObjectManager에서 사용하는 Id는 다른 거임)
        public string Name { get; set; }  // 게임 내에서 사용하는 닉네임

        // TODO - 재화 자동화 필요
        public int Jewel { get; set; }
        public int Gold { get; set; }
    }
}
