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

### CB. Shield Wall Tactic Unassigned from Chapter 1-2 Stages 33-40 (supersedes section BW's content assignment; system code untouched)
`Assets/03. SO/Stages/Stage_1_33~40.asset`/`Stage_2_33~40.asset`(16개) — 방패벽 전술(`TacticEntries`)을 이 16개 스테이지에서 배정 해제했다. `Stage.Tactics.ITacticFormationGroup`/`ShieldWallFormationGroup`/`FormationFollower`/`GuardPositioner`/`Character.ShieldGuard` 등 전술 시스템 코드 자체는 전혀 건드리지 않았다 — section AX(War Climax를 챕터 2-4에서 배정만 해제)와 동일한 선례: `TacticEntries`를 다시 채우면 코드 변경 없이 방패벽 콘텐츠가 그대로 돌아온다.
- 16개 스테이지 전부 `tacticEntries: []`로 비웠고, 그 자리를 대신하던 원래 `totalUnitCount`(각 스테이지가 갖고 있던 방패벽 총 마릿수)를 그대로 총 마릿수로 유지한 채 **보병 66% / 궁병 34%**로 정확히 나눠 `spawnEntries`에 추가했다(반올림은 section V와 같은 방식 — 궁병 쪽을 먼저 반올림하고 보병이 나머지를 흡수, 이번 데이터셋에서는 두 반올림 방향이 결과적으로 동일한 값을 냄). 기존에 있던 엘리트/보스 `spawnEntries`(예: `Stage_1_40`/`Stage_2_40`의 보스 웨이브)는 그대로 유지하고 보병/궁병 항목만 그 앞에 추가했다.
- 새로 추가한 보병/궁병 웨이브의 `spawnInterval`은 `0.3`으로 통일했다 — section V 공식대로라면 이 구간(챕터 상대 스테이지 33~40)의 일반 웨이브 간격은 약 0.5~0.59초였겠지만, 명시적 요청("리스폰 간격을 기존보다 빠르게")에 따라 더 빠르게 잡았다. 다른 스테이지의 플레이스홀더 밸런스 수치와 같은 성격 — 나중에 개별 조정 가능.
- `StageProgressTracker.CalculateTotal`(section CA)은 `SpawnEntries`+`TacticEntries` 총합 공식을 그대로 쓰므로, `tacticEntries`가 빈 배열이면 자동으로 0을 더해 정상 동작한다 — 이 변경에 코드 수정이 필요 없었던 이유.

### CC. Monster Spawn Points: 4-Direction (Top/Bottom/Left/Right) + Wide-Zoom-Relative Off-Screen Placement (supersedes section G's top/bottom-only spawn point description)
`Assets/02. Script/Stage/MonsterSpawner.cs`/`StageController.cs` + 씬의 `StageController` 자식 스폰 지점들 — 섹션 BN(카메라 줌 슬라이더)/BJ~CB(카메라 추적) 작업으로 플레이어가 `CameraZoomControl`의 줌 슬라이더를 value=0(최광각, `wideOrthographicSize=16`)까지 당길 수 있게 되면서, 기존에 y=±10에 고정돼 있던 상/하 스폰 지점(±16 안쪽)이 최광각 뷰에서 실제로 화면에 노출되는 문제가 있었다(섹션 BN이 playtesting 과제로 남겨뒀던 우려가 현실화된 사례). 이번에 좌/우 스폰 지점을 새로 추가하면서 전체를 최광각 뷰 기준으로 재배치했다.
- **경계 기준:** `wideOrthographicSize(16) × aspect(0.5)` → 최광각 뷰는 X:[-8,8] / Y:[-16,16]. 모든 스폰 지점을 이 사각형 밖에 여유를 두고 배치했다: 위/아래는 `y=±18`(x=∓3, 기존 위치 유지), 좌/우는 `x=±10`(y=-8/0/8, 3개씩). 좌표는 여전히 손으로 배치한 고정 `Transform`이다 — `CameraFollowService.GetWorldBoundsHalfExtent()`를 읽어 런타임에 동적으로 배치하지 않는다(이 프로젝트의 스폰 지점은 원래부터 Inspector에 미리 심어두는 정적 좌표라는 기존 관례를 그대로 따름).
- **`Stage.MonsterSpawner.SpawnSide`**(새 `private enum`, `Top`/`Bottom`/`Left`/`Right`) — 기존 `IsPlayerNearTop()`(세로 축 하나만 보고 위/아래 이분법)을 `DetermineSpawnSide()`로 대체: 플레이어의 뷰포트 좌표가 화면 중심(0.5, 0.5)에서 세로/가로 중 **더 많이 벗어난 축**을 고르고, 그 축의 반대쪽 방향에서 스폰한다(예: 플레이어가 화면 오른쪽에 치우쳐 있으면 왼쪽에서). 기존 "플레이어 반대편에서 등장" 설계 의도를 4방향으로 자연스럽게 확장한 것 — 카메라/플레이어를 아직 못 구하면 기존과 동일하게 `Top`으로 대체(fallback).
- `NextSpawnPoint()`/`GetSpawnPoints(side)`가 방향별로 분리된 커서(`_topCursor`/`_bottomCursor`/`_leftCursor`/`_rightCursor`)를 각각 순환한다. `PrepareFormationLayout(entry, side)`(전술 대형용)도 일반화됐다 — Top/Bottom은 리더 줄이 X축을 따라 늘어서고 Left/Right는 Y축을 따라 늘어서며, "열 간격"(리더→추종자, 화면 더 바깥쪽)은 각 방향의 바깥쪽 부호(Top=+Y, Bottom=-Y, Left=-X, Right=+X)로 계산한다.
- **제거된 필드:** `playerNearTopViewportThreshold`(`StageController`/`MonsterSpawner` 양쪽) — 세로 축 하나에만 의미가 있던 "위/아래 경계값(0.5)" 개념이 "중심에서 더 많이 벗어난 축"이라는 축-무관 비교로 대체되면서 더 이상 쓰이지 않아 제거했다. 씬 파일에는 값이 남아있어도(Unity가 다음 저장 때 자동으로 정리) 클래스에 필드가 없으므로 아무 영향 없다.
- 씬: 기존 `SpawnPoint_Left`/`SpawnPoint_Right`(실제로는 상단 스폰 지점이었는데 이름이 좌/우로 오해를 부르던 것)를 `SpawnPoint_Top_Left`/`SpawnPoint_Top_Right`로 개명하고 재배치. `SpawnPoint_Left_1~3`/`SpawnPoint_Right_1~3`을 `StageController`의 새 자식으로 추가하고 `leftSpawnPoints`/`rightSpawnPoints` 배열에 연결.
