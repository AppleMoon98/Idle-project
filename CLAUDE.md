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
│   ├── Offline/
│   ├── Managers/
│   ├── Services/
│   └── Editor/
└── 03. SO/
    ├── Characters/
    ├── Monsters/
    ├── Items/
    ├── Skills/
    ├── Stages/
    └── Configs/
```


### C~ (계속). Implementation History (구현 히스토리 상세)
섹션 C부터의 시스템별 상세 구현 기록은 용량 문제로 별도 파일로 분리되어 있습니다. 아래 파일들을 함께 참고하세요 (연대순, 섹션 번호 이어짐):
@docs/implementation-log-1.md
@docs/implementation-log-2.md
@docs/implementation-log-3.md
@docs/implementation-log-4.md

**문서화 정책 (개발 일지 아님, 협업용 레퍼런스임):** 각 섹션은 "이 시스템이 지금 어떻게 동작하는가"만 남긴다 — 클래스/파일 구조, 책임, 공개 API, 다른 시스템과의 연결, 설계상 이유(왜 이렇게 만들었는지), 그리고 앞으로도 다시 발목 잡을 수 있는 구조적 함정(Unity API 특이 동작, 순서 의존성, 모호성 트랩 등)만 기록한다. "execute_code로 이런 값을 확인했다", "Play 모드에서 검증했다" 같은 검증/테스트 서술은 남기지 않는다 — 그건 결과가 아니라 과정이다. 이후 섹션이 이전 섹션의 기능을 완전히 대체했다면 이전 섹션은 삭제하고 현재 상태만 하나의 섹션에 남긴다(레터링은 건드리지 않음 — 중간에 빈 문자가 생겨도 상관없음).

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
