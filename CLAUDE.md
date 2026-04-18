# Echodia 프로젝트 — Claude 작업 현황

## 프로젝트 구조
- `Server/` — C# 게임 서버 (JobSerializer 기반 단일 스레드 GameRoom)
- `Client/` — Unity 클라이언트
- `Common/` — protoc, GenSpecData.bat, Protocol.proto, xlsx 스펙 시트

---

## 구현 완료된 기능

### 스킬 시스템 아키텍처 (2026-04-17 ~ 18)
| 기능 | 상태 |
|------|------|
| `ISkillEffect` — `chargeMultiplier` 파라미터 | ✅ |
| `MeleeAttackEffect` — DamagedInfo 반환, chargeMultiplier 적용 | ✅ |
| `RangedAttackEffect` — SpawnProjectile에 coefficient 전달 | ✅ |
| `BuffEffect` / `DebuffEffect` — 인터페이스 시그니처 통일 | ✅ |
| `Projectile.DamageCoefficient` 필드 추가 | ✅ |
| `GameRoom.SpawnProjectile(ownerId, type, damageCoefficient)` 오버로드 | ✅ |
| `GameRoom.HandleProjectileAttack` — DamageCoefficient 적용 | ✅ |
| `GameRoom.HandleUseSkill` — 중복 S_UseSkill 제거 (SkillExecutor가 담당) | ✅ |
| `SkillExecutor.ExecuteInstant` — S_UseSkill {Success, Instant, DamagedList} 브로드캐스트 | ✅ |
| `SkillExecutor` 채널링 — 차지 방식 (completedTicks 누적, 마지막 1회 공격) | ✅ |
| `SkillFactory` — `RequiredJobs` 필드 참조 버그 수정 | ✅ |
| `Protocol.proto` — StatType, SkillCostType, SkillActionType, SkillTargetingType, CastDirectionType 추가 | ✅ |
| `PacketHandler` S_UseSkillHandler — 채널링 시 쿨타임 미시작 + SpecData 실제 쿨타임 | ✅ |
| `PacketHandler` S_ChannelEndHandler — 채널링 종료 시 쿨타임 시작 | ✅ |
| `CreatureController.OnStartChanneling` / `OnChannelingEnd` + 이벤트 구독 | ✅ |
| `MyPlayerController` T키 바인딩 (LightningChannel 700002 테스트) | ✅ |
| `KeySettings.UseSkill2 = KeyCode.T` | ✅ |

---

## 수정된 파일 리스트

### 서버
- `Server/Game/Skill/Effects/ISkillEffect.cs`
- `Server/Game/Skill/Effects/MeleeAttackEffect.cs`
- `Server/Game/Skill/Effects/RangedAttackEffect.cs`
- `Server/Game/Skill/Effects/BuffEffect.cs`
- `Server/Game/Skill/Effects/DebuffEffect.cs`
- `Server/Game/Skill/SkillFactory.cs`
- `Server/Game/Skill/SkillExecutor.cs`
- `Server/Game/Object/Projectile.cs`
- `Server/Game/Room/GameRoom.cs`

### 클라이언트
- `Client/Assets/Scripts/Packet/PacketHandler.cs`
- `Client/Assets/Scripts/Controllers/Creautres/CreatureController.cs`
- `Client/Assets/Scripts/Controllers/Creautres/Players/MyPlayerController.cs`
- `Client/Assets/Scripts/Utils/KeySettings.cs`

### 공통
- `Common/protoc-3.12.3-win64/bin/Protocol.proto`

---

## 아직 해결되지 않은 버그 / 미완성 항목

| 항목 | 설명 | 담당 |
|------|------|------|
| `SkillTargetSelector.GetTargets` 전체 순회 | `GetAllObjects()` 쓰고 있어서 존 기반 아님. `GetObjectsInRange()` 추가 필요 | Claude |
| `DebuffEffect.cs` 스텁 | StatusEffectTracker 시스템 미구현 — Stun/Slow/Poison/Bleed 전부 TODO | 추후 |
| `ChannelingEffect` 파티클 prefab | `Resources/` 내 미생성. OnStartChanneling에서 로드 시도하나 없으면 null | 유저 |
| protoc 재실행 | Protocol.proto 수정 후 Protocol.cs 재생성 필요 | 유저 |
| GenSpecData.bat 재실행 | xlsx 스킬 스펙 시트 작성 후 MetaData.cs 등 자동 생성 필요 | 유저 |

---

## 주의사항 (안티패턴 박제)

### 1. 채널링 = DoT가 아니라 차지 메카닉
처음에 채널링을 "틱마다 데미지 주는 DoT" 로 설계했다가 유저가 수정.
> **올바른 설계**: 틱마다 `chargeCount++`만 하고, 채널 완료 시 `DamageCoefficient × chargeCount` 배율로 **1회만** 공격. Ranged도 투사체 1개만 스폰 (계수만 커짐).

### 2. StatType / SkillCostType 등은 SheetEnum이 아니라 proto
버프/디버프 스탯 종류는 패킷에 실어야 하므로 `Protocol.proto`에 넣어야 함.
SheetEnum(자동 생성 C# enum)에 넣으면 proto 쪽이 모르는 타입이 됨.

### 3. SkillFactory에서 SkillMetaData 필드명 주의
`skill.GetRequiredJobsList()` 같은 메서드는 존재하지 않음.
자동 생성 필드는 `skill.RequiredJobs` (List 타입) 형태 — GenSpecData 후 생성된 클래스 직접 확인할 것.

### 4. 파일 Write 전 반드시 Read
Write 툴은 해당 파일을 먼저 Read하지 않으면 에러. 기존 파일 수정 시 항상 Read → Edit 순서.

### 5. Broadcast는 Push 불필요
`HandleUseSkill` / `ScheduleDelayedAction` 콜백은 이미 GameRoom 스레드(Job Queue) 내에서 실행 중.
`_owner.GameRoom.Broadcast(...)` 직접 호출해도 스레드 안전. 추가 Push 불필요.

---

## 스킬 아키텍처 요약 (참고용)

```
[Instant - Melee]
  C_UseSkill → HandleUseSkill → SkillExecutor.Use()
    → MeleeAttackEffect.Execute() → OnDamaged → DamagedList
    → S_UseSkill {Success, Instant, DamagedList} Broadcast

[Instant - Ranged]
  C_UseSkill → SkillExecutor.Use()
    → RangedAttackEffect.Execute() → SpawnProjectile → S_Spawn
    → 클라 콜라이더 → C_Attack {RangedAttack}
    → HandleProjectileAttack → DamageCoefficient 적용 → S_Attack

[Channeling]
  C_UseSkill → S_UseSkill {Channeling, CastTimeMs} → 클라 차지 애니
  틱마다 chargeCount++ (데미지 없음)
  채널 완료 → 1회 공격 (DamageCoefficient × chargeCount)
    Melee: S_UseSkill {Channeling, DamagedList}
    Ranged: S_Spawn (계수 반영 투사체 1개)
  → S_ChannelEnd {Interrupted} → 클라 쿨타임 시작
```

---

## Next Step

1. **GameRoom에 `GetObjectsInRange(center, range)` 추가** + `SkillTargetSelector.GetTargets`에서 `GetAllObjects()` → `GetObjectsInRange()` 교체 (존 기반 탐색 완성)
2. **GenSpecData.bat 실행** (유저) → 서버+클라 빌드 → Melee 스킬의 `allDamaged` 정상 채워지는지, 몬스터 HP 감소하는지 확인
3. **`ChannelingEffect` 파티클 prefab 생성** (유저) → T키로 LightningChannel 차지 → 3초 후 투사체 발사 + S_ChannelEnd 시 쿨타임 시작 E2E 검증
