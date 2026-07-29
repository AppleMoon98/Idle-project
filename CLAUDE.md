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
- `GameBootstrapper` — scene entry point (composition root). `Awake()` creates `ServiceLocator`/`EventBus`, registers `EventBus` and `GameTicker` into the locator, exposes `static Services`/`static Events` as the single intentional global access point.

Domain systems (Combat, Stage, War, Equipment, ...) depend on `Core`; `Core` never depends on them. Domain systems talk to each other only via `EventBus`, never by direct reference.

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
