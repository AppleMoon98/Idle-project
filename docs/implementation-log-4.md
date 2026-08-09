# 구현 히스토리 4부 (섹션 BZ~)

CLAUDE.md의 "### BZ. ..."부터 이어지는 상세 구현 기록입니다. 섹션 번호는 CLAUDE.md 전체 기준(3. Architecture & Technical Rules 하위)과 이어지며, `docs/implementation-log-3.md`의 마지막 섹션(BY) 바로 다음부터 시작합니다. **새 섹션을 추가할 때는 이 파일 끝에 이어서 작성하세요.**

### BZ. Tactic Formation Spawning: Simultaneous Spawn, Total-Count Field, Leaderless Follower Kiting
`Assets/02. Script/Stage/MonsterSpawner.cs`/`TacticSpawnEntry.cs`/`StageController.cs` + `Stage/Tactics/ShieldWallFormationGroup.cs` + `Assets/04. Prefab/Monster_Spearman.prefab`

- 전술 웨이브(`TacticSpawnEntry`)의 모든 리더/추종자는 한 틱 안에서 동시에 스폰된다. `MonsterSpawner.TickTactics()`가 남은 전술 엔트리를 `while` 루프로 순회하며 각 엔트리마다 `SpawnFormation(entry)`을 호출해 그 엔트리의 유닛 전원을 한 번에 생성한다(시간차 없음).
- `TacticSpawnEntry.TotalUnitCount`는 이 웨이브의 총 유닛 수(1열+2열 합산)를 나타낸다. 실제 스폰 시 `pairCount = TotalUnitCount / 2`(정수 나눗셈이라 홀수 총량은 1 적게 나옴)로 리더/추종자를 절반씩 나눈다. `StageController.LoadStage`의 `PoolManager.EnsurePool` 풀 크기도 동일한 공식을 사용한다.
- 대형의 리더(1열)/추종자(2열)는 스폰 시점에 `MonsterSpawner.PrepareFormationLayout`이 계산한 고유 좌표에 각각 배치된다 — 리더는 스폰 지점 기준 한 줄로, 추종자는 그보다 `rowSpacing`만큼 더 화면 밖(플레이어 반대쪽)으로 물러난 평행한 줄로 나란히 선다(스폰 지점을 순환 재사용하지 않고 매번 새 좌표를 계산 — 그래야 많은 쌍이 한 지점에 뭉치지 않는다).
- 리더(방패병)를 잃은 추종자(창병, 또는 대체로 들어간 궁병)는 `Combat.RangedKiter`(원래 궁병 전용이지만 사격 종류와 무관한 순수 "거리 유지" 이동 로직이라 재사용 가능)로 전환돼 카이팅한다 — `FormationFollower`를 끄고 `RangedKiter`를 켠다(`Stage.Tactics.ShieldWallFormationGroup.ReleaseFollowerAlone`). 리더가 (재)배정되면 반대로 전환된다(`AssignPrimaryPair`). `Monster_Spearman.prefab`은 평소 비활성 상태의 `RangedKiter`를 갖고 있으며 `kiteTriggerDistance=1.5`로 창의 긴 사거리(4)에 맞춰져 있다.

### CA. StageProgressTracker Must Count TacticEntries; Shield-Bearer Shield Visual
`Assets/02. Script/Stage/StageProgressTracker.cs` + `Character/ShieldGuard.cs`/`ShieldFacing.cs` + `Assets/04. Prefab/Monster_ShieldBearer.prefab`

- **함정:** `StageProgressTracker.CalculateTotal(stage)`는 `stage.SpawnEntries`뿐 아니라 `stage.TacticEntries`도 `MonsterSpawner.PrepareFormationLayout`과 **정확히 같은 공식**(`Mathf.Max(TotalUnitCount / 2, 0) * 2`)으로 합산해야 한다. 두 계산이 어긋나면 스테이지가 너무 일찍 클리어되거나(추적 총량 < 실제 스폰량) 영원히 클리어되지 않는다(추적 총량 > 실제 스폰량) — `_killCount`는 대형 유닛의 죽음도 그대로 세기 때문에, 전술 웨이브를 빠뜨리면 대형을 다 안 잡고도 스테이지가 클리어로 잘못 판정될 수 있다.
- `Character.ShieldGuard`는 `shieldVisual`(자식 GameObject) 참조를 갖고 방패 상태에 맞춰 직접 켜고 끈다 — 방패가 있으면(`HasShield`) 보이고, `AbsorbDamage`로 방패가 깨지는 순간 숨긴다.
- `ShieldGuard`는 `Core.Pooling.IPoolable`을 구현하며 `OnSpawned()`에서 방패 체력과 비주얼을 리셋한다. **함정:** 몬스터는 `PoolManager`로 재사용되고 `Awake()`는 오브젝트 생애에 한 번만 실행되므로, 풀링되는 컴포넌트의 상태는 `Awake()`뿐 아니라 `IPoolable.OnSpawned()`에서도 리셋해야 한다(`Character.Health`도 같은 패턴). `PoolManager.NotifySpawned`/`NotifyDespawned`는 `GetComponents<IPoolable>()`(복수형)로 순회하므로, 같은 GameObject에 `IPoolable` 컴포넌트가 여러 개 있어도(`Health`+`ShieldGuard`) 전부 알림을 받는다 — 단일 컴포넌트만 찾는 `GetComponent<T>()`/`TryGetComponent<T>()` 조회(예: `IMonsterMovementInitializer`)와 달리 이런 모호성 문제가 없다.
- `Monster_ShieldBearer.prefab`의 `ShieldVisual`(루트의 직계 자식)은 `WeaponSprite.png`를 회청색으로 틴트해 재사용하며(새 아트 없이 플레이스홀더 재사용), `localEulerAngles.z = 90`으로 90도 회전(긴 축을 가로로 눕혀 "가로로 넓은 판" 형태)하고 `localScale = (1.4, 0.8, 1)`로 조정돼 있다.
- `Character.ShieldFacing`(`ITickable`, `Monster_ShieldBearer.prefab`에 부착)은 `CharacterMover.Target`의 y좌표를 자신과 비교해(`retargetInterval=0.2초` 주기 폴링) `ShieldVisual.localPosition`을 위/아래 중앙으로 토글한다 — 목표가 아래에 있으면(아래로 이동 중) 방패는 아래 중앙(`(0,-0.4,z)`), 위에 있으면 위 중앙(`(0,0.4,z)`)에 위치한다.
