# Project Specification: 2D Idle RPG (Unity 6)

## 1. Project Overview
- **Genre:** 2D Idle RPG (Medieval Fantasy)
- **Target Platform:** Mobile (Android / iOS)
- **Engine/Lang:** Unity 6 / C#
- **Key Mechanics:** Auto-combat, Monster Spawning, Equipment & Growth, Stage Escalation, Story-driven Rank System, Climax War Battles.

## 2. Core Game Systems

### A. Stage & Combat Mechanics
- **Stage Progression:** Chapter-based structure (e.g., 1-1 ~ 1-40, 2-1 ~ 2-40).
- **Combat Loop:**
  - Monsters spawn off-screen and move toward the Player.
  - Player attacks with equipped weapons.
  - On contact with player -> Player HP decreases.
  - Progression Loop: Auto-Combat -> Gold/Equipment Drop -> Enhance Equipment -> Stat Growth -> Stage Clear -> Unlock Rank/Content.

### B. Rank & Progression System
- Player progresses from "시골 소년" to "병사", "십인 대장", etc.
- Unlocks occur at stage milestones (e.g., Clearing 4-40 -> Rank Upgrade -> Short Dialogue Cutscene -> Unlocks New System).
- System unlocks are gated by **Rank**, NOT Player Level.

### C. War & Soldier Systems
- **Soldier System:** Unlocked at 'Decurion'. Spawns small auxiliary troops that fight alongside the player (does not alter the core idle RPG genre).
- **War System:** Triggered at chapter climaxes (e.g., Stage X-40). Features larger monster/soldier scale and visual effects without changing genre mechanics.

### D. Growth Systems (Decoupled Subsystems)
- Equipment, Skills, Pets, Relics, Collections, Achievements, Enhancements, Promotions.
- Every growth subsystem must be independent and decoupled.

## 3. Architecture & Technical Rules

### A. Programming Guidelines
- **Design Pattern:** Object-Oriented Programming (OOP), SOLID, Composition over Inheritance.
- **Data Architecture:** Data-driven via ScriptableObjects (SO).
- **Communication:** Event-driven architecture (Observer/Action based). Cross-system communication goes through `Core.EventBus` — domain systems must never reference each other directly.
- **Performance:** Avoid `Update()` abuse, strict Object Pooling, Addressables-ready design.
- **Code Style:** XML Documentation comments, zero magic numbers, interface-driven.

### B. Directory Structure
```
Assets/
├── 02. Script/
│   ├── Core/
│   ├── Character/
│   ├── Combat/
│   ├── Stage/
│   ├── War/
│   ├── Soldier/
│   ├── Equipment/
│   ├── Inventory/
│   ├── UI/
│   ├── Save/
│   ├── Managers/
│   └── Services/
└── 03. SO/
    ├── Characters/
    ├── Monsters/
    ├── Items/
    ├── Skills/
    ├── Stages/
    └── Configs/
```

### C. Core Foundation (implemented)
`Assets/02. Script/Core/` provides the base every other system builds on:
- `IManager` — lifecycle contract (`Initialize`/`Shutdown`) for stateful managers.
- `IService` — marker interface for stateless functionality providers.
- `ITickable` — `Tick(float deltaTime)` contract; register with `GameTicker` instead of writing a per-object `Update()`.
- `ServiceLocator` — type-keyed instance registry (`Register<T>`/`Get<T>`/`TryGet<T>`/`Unregister<T>`). Replaces scattered static singletons.
- `EventBus` — typed pub/sub (`Subscribe<T>`/`Unsubscribe<T>`/`Publish<T>`) for decoupled cross-system communication.
- `GameTicker` — the one `MonoBehaviour` allowed to own an `Update()`; ticks all registered `ITickable`s, with safe register/unregister during iteration.
- `GameBootstrapper` — scene entry point (composition root). `Awake()` creates `ServiceLocator`/`EventBus`, registers `EventBus`, `GameTicker`, and `PoolManager` into the locator, exposes `static Services`/`static Events` as the single intentional global access point.

Domain systems (Combat, Stage, War, Equipment, ...) depend on `Core`; `Core` never depends on them. Domain systems talk to each other only via `EventBus`, never by direct reference.

### D. Object Pooling (implemented)
`Assets/02. Script/Core/Pooling/` + `Assets/02. Script/Managers/PoolManager.cs`:
- `IPoolable` — optional `OnSpawned()`/`OnDespawned()` hooks for pooled components.
- `PooledInstance` — internal tag component recording a spawned instance's source prefab, so callers can `Release(instance)` without remembering which pool it came from.
- `ObjectPool<T>` — generic `Stack<T>`-based pool; create/get/release/destroy are all caller-supplied callbacks, capacity/max size are constructor params (no magic numbers).
- `PoolManager` (`IManager`, `IService`) — keeps one `ObjectPool<GameObject>` per registered prefab. `RegisterPool(prefab, defaultCapacity, maxSize)`, `Get(prefab, position, rotation)`, `Release(instance)`. Registered into `GameBootstrapper.Services` at startup.
- Any system that repeatedly spawns/destroys GameObjects (monsters, projectiles, damage numbers, soldiers, VFX) must go through `PoolManager` instead of `Instantiate`/`Destroy` directly.

### E. Character Foundation (implemented)
`Assets/02. Script/Character/` — the shared component set Player and Monster are both composed from (no inheritance hierarchy):
- `CharacterStatsSO` — data asset (`MaxHealth`/`AttackPower`/`MoveSpeed`/`AttackInterval`), read-only, never mutated at runtime.
- `RuntimeStats` — plain C# copy of a `CharacterStatsSO`'s values with settable properties, so buffs/equipment can modify it later without touching the source asset.
- `CharacterStatsProvider` — holds the `CharacterStatsSO` reference and lazily builds/exposes `RuntimeStats` via `Stats` (lazy so it doesn't depend on component `Awake()` order).
- `Health` (`IPoolable`) — `TakeDamage`/`Heal`, clamps to `[0, MaxHealth]`, publishes `CharacterHealthChangedEvent` on change and `CharacterDiedEvent` on death via `GameBootstrapper.Events`. `OnSpawned()` resets current health and `IsDead` for pooled reuse.
- `CharacterMover` (`ITickable`) — moves toward a `Target` transform at `Stats.MoveSpeed`; registers with `GameTicker` in `OnEnable`. Doesn't know about "Player" — the target is assigned by whichever system spawns it (Stage/Monster spawner, later).
- `Character/Events/CharacterHealthChangedEvent.cs`, `CharacterDiedEvent.cs` — `readonly struct` events other systems (Stage, Combat, UI) subscribe to; `Health` never references those systems directly.

Note: `CharacterStatsSO`/`RuntimeStats` also carry `AttackRange` (added for Combat).

### F. Combat (implemented)
`Assets/02. Script/Combat/` — 2D physics-based auto-combat, built only on `Health`/`RuntimeStats`/`GameTicker`; never references Stage/Rank/Loot:
- `Attacker` (`ITickable`) — range-scan attacker. Accumulates `deltaTime`; every `Stats.AttackInterval`, runs `Physics2D.OverlapCircleAll(position, Stats.AttackRange, targetLayerMask)`, picks the nearest live `Health`, calls `TakeDamage(Stats.AttackPower)`. Used by Player to auto-attack Monsters.
- `ContactAttacker` (`ITickable`) — trigger-based attacker. Tracks the `Health` currently overlapping via `OnTriggerEnter2D`/`OnTriggerExit2D` (2D triggers — this is a 2D mobile game, no 3D physics), attacks it every `Stats.AttackInterval` while in contact. Used by Monsters to damage the Player on contact.
- Neither component publishes its own events or knows about factions by name — target filtering is purely via `LayerMask` set per-prefab in the Inspector (Player prefab targets the Monster layer, Monster prefab targets the Player layer), so adding Soldier/War later needs no Combat code changes.
- Damage application and death/health events remain owned by `Character.Health` — Combat only decides *who* and *when* to hit.

### G. Stage (implemented)
`Assets/02. Script/Stage/` — runs a single stage (e.g. "1-1") and detects clear; knows nothing about Rank/Loot/UI:
- `StageSO` — data asset: `Chapter`, `StageNumber`, `SpawnEntries` (`MonsterSpawnEntry[]`).
- `MonsterSpawnEntry` (`[Serializable]`, not a SO) — one spawn wave: `MonsterPrefab`, `Count`, `SpawnInterval`.
- `MonsterSpawner` (`ITickable`) — walks `SpawnEntries` in order, spawns via `PoolManager.Get`, sets the spawned `CharacterMover.Target` to the player, and calls `StageProgressTracker.RegisterSpawned` for each instance. Off-screen placement is a scene/spawn-point concern, not code — `spawnPoints` are Inspector-assigned `Transform`s.
- `StageProgressTracker` — tracks exactly the monsters it was told about via `RegisterSpawned` (a `HashSet<GameObject>`, not tag/string based) against `CharacterDiedEvent`; when the registered count all die, publishes `StageClearedEvent` and unsubscribes itself (`Dispose`).
- `Stage/Events/StageClearedEvent.cs` — `readonly struct` carrying the cleared `StageSO`; Rank/progression-advance systems will subscribe to this later.
- `StageController` (MonoBehaviour, composition root for one stage) — `LoadStage(stageSO)` calls `PoolManager.EnsurePool` per monster prefab (idempotent — see below), builds `StageProgressTracker` + `MonsterSpawner`, registers the spawner with `GameTicker`. Tears down the previous stage's spawner/tracker first.
- **`PoolManager.EnsurePool(prefab, defaultCapacity, maxSize)`** (added to `Managers/PoolManager.cs`) — registers a pool only if one doesn't already exist for that prefab; needed because multiple stages reuse the same monster prefabs and `RegisterPool` throws on duplicate registration.

### H. Attack Behaviors: Melee / Ranged (implemented)
`Assets/02. Script/Combat/` — pluggable "what happens on hit" strategies for `Attacker`, swapped by composition (attach the desired behavior component, no subclassing):
- `IAttackBehavior` — `Execute(origin, target, attackPower)`. `Attacker.Awake()` caches `GetComponent<IAttackBehavior>()` and calls it instead of applying damage directly; if none is attached, the attack silently no-ops.
- `WeaponSwing` (`ITickable`) — attached to a "weapon socket" child transform (e.g. `Player/WeaponAnchor`). `Play()` rotates the socket 0→`swingAngle`→0 over `swingDuration` via a sine curve, ticking only while actively swinging. This anchor is where the future Equipment system will parent the equipped weapon's visual — swinging the socket swings whatever is attached to it, without Combat needing to know about Equipment.
- `MeleeAttackBehavior` — applies damage immediately (hit detection stays in `Attacker`'s range scan) and calls `WeaponSwing.Play()` for the visual. The swing is purely cosmetic and does not gate the hit.
- `Projectile` (`ITickable`) — spawned by `RangedAttackBehavior`, homes toward a captured `Health` target each tick, applies damage and self-releases to the pool on arrival (`hitDistance`) or if the target dies/vanishes first (fake-null-safe).
- `RangedAttackBehavior` — spawns/launches a `Projectile` from `PoolManager` instead of dealing damage directly; damage lands only when the projectile arrives.
- Both `Character/Monster` test prefabs demonstrate this: Player currently has `MeleeAttackBehavior` wired to `WeaponAnchor`'s `WeaponSwing`; `RangedAttackBehavior` + `Assets/04. Prefab/Projectile.prefab` were verified working the same way (swap the attached behavior component to switch).

**Known gotcha — Script Execution Order:** Unity processes `Awake`→`OnEnable` per-GameObject in scene order, not as two separate global passes. Any component that reads `GameBootstrapper.Services`/`Events` in `OnEnable` (a one-shot check) can silently and permanently fail to acquire it if that GameObject initializes before `GameBootstrapper`'s GameObject does (this actually happened with `RangedAttackBehavior` failing to get `PoolManager`). Fix applied: `GameBootstrapper`'s script execution order is set to `-1000` (stored in `Core/GameBootstrapper.cs.meta`, committed) so its `Awake()` always runs first. Keep this in mind if new components read `GameBootstrapper.Services`/`Events` in `OnEnable`/`Awake`.

### I. Loot & Currency (implemented)
`Assets/02. Script/Loot/` — turns monster death into gold/equipment drops; knows Character (via `CharacterDiedEvent`) and the `Equipment` data type, nothing else:
- `MonsterLootSO` — data asset: `MinGold`/`MaxGold`/`DropChance` for gold, plus `EquipmentDropEntry[] EquipmentDrops` for the equipment drop table (each entry independently rolled).
- `EquipmentDropEntry` (`[Serializable]`, not a SO) — one drop table row: `EquipmentSO Equipment`, `DropChance`.
- `MonsterLootProvider` (MonoBehaviour) — attached to monster prefabs, holds the `MonsterLootSO` reference so `LootDropper` can identify "this dead GameObject was a monster with loot" without any tag/name check.
- `LootDropper` (plain C# class, constructed once in `GameBootstrapper`, not per-stage) — subscribes `CharacterDiedEvent`; if the dead GameObject has a `MonsterLootProvider`, rolls gold (`GoldEarnedEvent`) and each equipment entry (`ItemDroppedEvent`) independently.
- `CurrencyService` (`IManager`, `IService`) — subscribes `GoldEarnedEvent`, accumulates `CurrentGold`, publishes `GoldChangedEvent` on any change. Also exposes `TrySpendGold(amount)` (used by `Enhancement`) which fails without side effects if the balance is insufficient.
- `Loot/Events/GoldEarnedEvent.cs`, `GoldChangedEvent.cs`, `ItemDroppedEvent.cs` — the three event types other systems subscribe to.

### J. Equipment (data only, implemented)
`Assets/02. Script/Equipment/` — currently just the item data shape; no equip/stat-application logic yet (that's a future system):
- `EquipmentSO` — `ItemName`, `EquipmentType`.
- `EquipmentType` — `enum { Weapon, Armor, Accessory }`, a slot placeholder for the future equip system.

### K. Inventory (implemented)
`Assets/02. Script/Inventory/` — holds dropped equipment; knows nothing about `Loot` except the event type it subscribes to:
- `InventoryService` (`IManager`, `IService`) — subscribes `ItemDroppedEvent`, appends to an internal `List<EquipmentSO>` exposed as `Items` (read-only), publishes `InventoryChangedEvent` (added item + new total count).

### L. Enhancement (implemented)
`Assets/02. Script/Enhancement/` — spends gold to raise the player's base stats; deliberately does not know "Player" exists:
- `EnhancementStatType` — `enum { AttackPower, MaxHealth }`.
- `EnhancementConfigSO` — per-stat data asset: `StatType`, `BaseCost`, `CostIncreasePerLevel`, `ValuePerLevel`, `MaxLevel`. Cost for the next level is `BaseCost + CostIncreasePerLevel * currentLevel` — fully data-driven, no magic numbers in code.
- `EnhancementService` (`IManager`, `IService`) — constructed with `EventBus`, `CurrencyService`, and the full `EnhancementConfigSO[]` (assigned on `GameBootstrapper` in the Inspector). `TryEnhance(statType)` computes the next cost, calls `CurrencyService.TrySpendGold`, and on success increments the internal level and publishes `StatEnhancedEvent` (stat type + `ValuePerLevel` to apply + new level). Never touches any `RuntimeStats` itself.
- `Enhancement/Events/StatEnhancedEvent.cs` — the event `Character.StatEnhancementReceiver` subscribes to.
- `Character/StatEnhancementReceiver.cs` (MonoBehaviour, attached to the Player) — subscribes `StatEnhancedEvent`, adds `ValuePerLevel` to its own `CharacterStatsProvider.Stats.AttackPower`/`MaxHealth` depending on `StatType`. This is how "Enhancement doesn't know Player" and "Player's stats still get buffed" coexist — the receiver lives on the Character/Player side, not the Enhancement side.

### M. UI Shell (implemented)
`Assets/02. Script/UI/` — mobile-portrait presentation layer; every component here subscribes only to the events of the one domain it displays, never to each other:
- `Canvas`'s `CanvasScaler` is set to `ScaleWithScreenSize`, reference resolution 1080x1920 (portrait).
- `BottomMenuUI` — generic tab-bar controller: an inspector-assigned `(Button, GameObject panel)[]` array. Clicking a tab's button opens its panel (closing whichever was open); clicking the already-open tab's button closes it. Has zero knowledge of what any panel contains.
- `GoldDisplayUI` — subscribes `GoldChangedEvent`; reads `CurrencyService.CurrentGold` once on `OnEnable` for the initial value (since the event only fires on change, not on load).
- `EquipmentPanelUI` — subscribes `InventoryChangedEvent`; renders `InventoryService.Items` as a simple text list.
- `StatPanelUI` — subscribes `StatEnhancedEvent`; shows each stat's current level and next cost via `EnhancementService`, and wires its two buttons to `EnhancementService.TryEnhance`.
- `SoldierPanel`/`SkillPanel`/`RelicPanel` — scriptless placeholder panels ("준비 중") since the Soldier/Skill/Relic systems don't exist yet. Swap in real controllers when those systems are built, following the same subscribe-only-your-domain pattern.
- **Gotcha (new Input System projects):** UI `Button` clicks need an `EventSystem` + an input module in the scene. This project has `activeInputHandler: 1` (new Input System only, old Input Manager disabled), so the module must be `InputSystemUIInputModule`, not the legacy `StandaloneInputModule` — the legacy one silently fails to deliver clicks with the old Input Manager off.
- **Gotcha (teardown order):** any UI `MonoBehaviour` that touches `GameBootstrapper.Events`/`Services` in `OnDisable`/`OnDestroy` must null-conditional it (`GameBootstrapper.Events?.Unsubscribe(...)`). On exiting Play Mode, `GameBootstrapper.OnDestroy` can run and null out `Events` before a UI GameObject's own `OnDisable` fires, since the two aren't in the same teardown chain — this caused a real `NullReferenceException` in `GoldDisplayUI.OnDisable` during testing.

## 4. Execution Workflow (Strict Rule)
Do NOT write full implementation code at once. Follow this iterative approval process:

1. Requirements Analysis
2. Implementation Plan
3. Class Diagram / UML Outline
4. System Architecture Explanation
5. Target Folder/File Path Mapping
6. **PAUSE and Wait for User Approval.**

After approval: implement ONE atomic feature -> verify compile/errors (via UnityMCP `refresh_unity` + `read_console`) -> explain -> await next approval.

### Commit policy
Do NOT `git add`/`git commit` after implementing a feature by default. Only commit when the user explicitly says so (e.g. "커밋해줘", "커밋하고 진행해") — the user wants to review code before it's committed.

### Language rule
- Conversation with the user, and code comments (including XML doc comments), must be in Korean.
- Everything else — identifiers, class/variable/function names, file names — must never use Korean; English only.
