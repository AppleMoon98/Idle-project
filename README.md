# Idle Project

Unity 6 기반 2D 방치형(Idle) RPG. 자동 전투, 몬스터 스폰, 장비/스킬/병사 등 성장 시스템, 스테이지 진행과 랭크 승급, 챕터 클라이맥스(War) 전투를 다룬다.

## 개요

- **장르:** 2D 방치형 RPG (중세 판타지)
- **타겟 플랫폼:** 모바일 (Android / iOS)
- **화면 방향:** 세로(Portrait) 전용 — 가로 모드/자동 회전은 지원하지 않는다(`PlayerSettings.defaultInterfaceOrientation = Portrait`). HUD와 팝업 전부 `1080x1920` 세로 기준으로 설계돼 있어 가로 회전 시 레이아웃이 겹친다.
- **엔진 / 언어:** Unity 6 / C#
- **핵심 시스템:** 자동 전투, 몬스터 스폰, 장비/강화, 스테이지 진행, 랭크(계급) 승급, 병사 배치, War 클라이맥스 전투, 오프라인 보상

## 실행 환경

- Unity **6000.3.12f1** (`ProjectSettings/ProjectVersion.txt`에 명시된 버전 그대로)
- 필요한 패키지 목록/버전은 `Packages/manifest.json`, `Packages/packages-lock.json`에 고정되어 있음 — Unity Hub로 이 리포지토리를 열면 Package Manager가 동일한 버전을 그대로 복원한다

## 시작하기

1. 이 저장소를 클론한다.
2. Unity Hub에 프로젝트로 추가해 연다 — `ProjectSettings/`가 트래킹되어 있어 별도 설정 없이 `6000.3.12f1`로 열린다.
3. `Assets/Scenes/SampleScene.unity`가 메인 씬이다.

## 아트 에셋 안내

이 프로젝트의 스프라이트(`Assets/01. Sprite/`)는 [Tiny Swords(Pixel Frog)](https://pixelfrog-assets.itch.io/tiny-swords) 에셋을 사용한다. 라이선스가 있는 외부 에셋이라 저장소에는 포함하지 않았다 — 위 링크에서 받아 `Assets/01. Sprite/` 아래에 배치해야 실제 그래픽으로 실행된다. 배치하지 않으면 흰 사각형으로 표시된다.

## 프로젝트 구조 / 문서

- `CLAUDE.md` — 프로젝트 스펙, 아키텍처 규칙(디렉토리 구조, 이벤트 기반 통신, 코드 스타일 등), 작업 워크플로우.
- `docs/implementation-log-1.md` ~ `implementation-log-8.md` — 시스템별 상세 구현 기록(연대순, 섹션 번호 이어짐). "이 시스템이 지금 어떻게 동작하는가"를 남기는 협업용 레퍼런스이며, 개발 일지가 아니다.

코드 규칙(디렉토리 구조, 네이밍, 언어 등)과 협업 절차는 `CLAUDE.md`를 우선 참고한다.
