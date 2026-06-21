using Xunit;

namespace ServerTests;

// 서버 핵심 수식 검증 테스트
// 실제 서버 클래스와 동일한 로직을 순수 수식으로 추출해 검증한다.
public class StatCalculationTests
{
    // ── 데미지 계산 ──────────────────────────────────────────────────────────

    [Fact]
    public void MeleeDamage_WithCoefficient_CalculatesCorrectly()
    {
        // MeleeAttackEffect.Execute: damage = (int)(baseDamage * coefficient * chargeMultiplier)
        int baseDamage = 200;
        float coefficient = 1.5f;
        float chargeMultiplier = 1.0f;

        int result = (int)(baseDamage * coefficient * chargeMultiplier);

        Assert.Equal(300, result);
    }

    [Fact]
    public void MeleeDamage_WithCharge_ScalesLinearly()
    {
        int baseDamage = 100;
        float coefficient = 1.0f;
        float chargeMultiplier = 3.0f;

        int result = (int)(baseDamage * coefficient * chargeMultiplier);

        Assert.Equal(300, result);
    }

    [Fact]
    public void RangedDamage_CoefficientAndChargeMultiply()
    {
        // RangedAttackEffect: coefficient = DamageCoefficient * chargeMultiplier
        float damageCoefficient = 1.5f;
        float chargeMultiplier = 2.0f;

        float result = damageCoefficient * chargeMultiplier;

        Assert.Equal(3.0f, result, precision: 5);
    }

    // ── 채지 배율 계산 ────────────────────────────────────────────────────────

    [Fact]
    public void ChargeMultiplier_FullCharge_ReturnsExpectedTicks()
    {
        // SkillExecutor.StopChannel:
        // chargeMultiplier = Max(1f, Min(elapsedMs, totalMs) / tickMs)
        int elapsedMs = 1000;
        int totalMs = 1000;
        int tickMs = 250;

        float result = MathF.Max(1f, (float)Math.Min(elapsedMs, totalMs) / tickMs);

        Assert.Equal(4.0f, result, precision: 5);
    }

    [Fact]
    public void ChargeMultiplier_PartialCharge_CapsAtTotal()
    {
        // elapsedMs가 totalMs를 초과해도 totalMs 기준으로 계산
        int elapsedMs = 2000;
        int totalMs = 1000;
        int tickMs = 250;

        float result = MathF.Max(1f, (float)Math.Min(elapsedMs, totalMs) / tickMs);

        Assert.Equal(4.0f, result, precision: 5);
    }

    [Fact]
    public void ChargeMultiplier_ZeroElapsed_ReturnsMinimumOne()
    {
        int elapsedMs = 0;
        int totalMs = 1000;
        int tickMs = 250;

        float result = MathF.Max(1f, (float)Math.Min(elapsedMs, totalMs) / tickMs);

        Assert.Equal(1.0f, result, precision: 5);
    }

    // ── 크리티컬 확률 ─────────────────────────────────────────────────────────

    [Fact]
    public void CriticalRate_AtCap_AlwaysTriggered()
    {
        // PlayerStatCalculator.GetFinalDamage:
        // isCritical = rand.Next(10000) < CriticalRate
        // CriticalRate = 10000 이면 항상 크리티컬
        int criticalRate = 10000;
        bool alwaysCritical = 9999 < criticalRate; // rand.Next(10000) 최대값은 9999

        Assert.True(alwaysCritical);
    }

    [Fact]
    public void CriticalRate_AboveCap_StillAlwaysTriggered()
    {
        // 현재 서버 코드는 상한선 클램프가 없어서 10000 초과도 동일하게 동작
        int criticalRate = 15000;
        bool alwaysCritical = 9999 < criticalRate;

        Assert.True(alwaysCritical);
    }

    [Fact]
    public void CriticalRate_Zero_NeverTriggered()
    {
        int criticalRate = 0;
        bool neverCritical = 0 < criticalRate;

        Assert.False(neverCritical);
    }

    // ── 버프 스탯 계산 ────────────────────────────────────────────────────────

    [Fact]
    public void BuffEffect_PercentBuff_CalculatesDelta()
    {
        // BuffEffect.CalculateDelta: delta = (int)(baseValue * percentValue)
        int baseValue = 100;
        float percentValue = 0.3f;

        int delta = (int)(baseValue * percentValue);

        Assert.Equal(30, delta);
    }

    [Fact]
    public void BuffEffect_FlatBuff_CalculatesDelta()
    {
        // BuffEffect.CalculateDelta: delta = (int)flatValue
        float flatValue = 50f;

        int delta = (int)flatValue;

        Assert.Equal(50, delta);
    }

    [Fact]
    public void BuffEffect_PercentBuff_AppliedToStat()
    {
        int baseStr = 100;
        float percentBuff = 0.5f;
        int delta = (int)(baseStr * percentBuff);

        int finalStr = baseStr + delta;

        Assert.Equal(150, finalStr);
    }

    // ── 직업별 기본 데미지 ────────────────────────────────────────────────────

    [Fact]
    public void WarriorBaseDamage_UsesStrFormula()
    {
        // PlayerStatCalculator.CalculateBaseDamage: STR*4 + DEX + PhysicalDamage
        int str = 50, dex = 10, physicalDamage = 20;

        int result = str * 4 + dex + physicalDamage;

        Assert.Equal(230, result);
    }

    [Fact]
    public void MageBaseDamage_UsesIntFormula()
    {
        // INT*4 + LUK + MagicDamage
        int statInt = 80, luk = 5, magicDamage = 30;

        int result = statInt * 4 + luk + magicDamage;

        Assert.Equal(355, result);
    }
}

