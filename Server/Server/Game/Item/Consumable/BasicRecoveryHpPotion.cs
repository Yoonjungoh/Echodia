
using Server.Data;
using Server.Game;

public class BasicRecoveryHpPotion : Consumable
{
    public ConsumableType ConsumableType { get; set; }
    
    public override bool CanUse(Player player)
    {
        // 소모품 사용 조건 검사 (ex: 레벨, 클래스, 쿨타임 등)
        
        return true; // 조건을 만족하면 true 반환
    }

    public override void Use(Player player)
    {
        
    }
}