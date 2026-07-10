# AGENTS.md

## 기본 원칙

- 간단한 작업이 아니면 오케스트라 지휘자처럼 에이전트를 적극 활용한다.
- 구현 전에 애매한 가정은 드러내고, 필요하면 질문한다.
- 요청 범위를 넘는 기능, 추상화, 리팩터링을 하지 않는다.
- 기존 스타일을 따른다.
- 변경은 요청과 직접 관련된 부분만 한다.
- 버그 수정은 먼저 원인을 분석하고, 검증 가능한 기준을 세운다.
- 작동하지 않을 때는 보강보다 원인 분석을 먼저 한다.
- 원인 분석은 코드 구조, 참조, 인스펙터 연결 포인트 기준으로 점검한다.
- 다단계 작업은 간단한 계획과 검증 방법을 둔다.
- 답변은 기본적으로 간결하게 한다.

## 코드 규칙

- OOP를 준수한다.
- 인터페이스 관련 스크립트 이름은 항상 `I`로 시작한다.
- Unity 런타임 스크립트는 담당 영역의 `02. Script` 아래에 둔다.
- 씬/프리팹 연결은 코드에서 억지로 보강하기보다 Inspector 참조가 드러나게 구성한다.
- 코드로만 구현하기보다 프리팹, 계층 구조, 씬 직접 배치를 기본으로 한다.

## 작업 범위

- ParkHanSol 담당 멀티 기능 작업은 기본적으로 아래 경로에서 진행한다.
  - `Assets/02. ParkHanSol_TeamLeader_Build & Multi/`
- 멀티 기능 때문에 필요한 공용 설정은 정상 변경으로 취급한다.
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`
  - `ProjectSettings/EditorBuildSettings.asset`
- MCP 설치물만 Git에 들어가지 않게 한다.
- Codex/MCP 같은 로컬 AI 설정 파일은 커밋하지 않는다.

## MCP / Git 규칙

- MCP 로컬 설치물은 커밋하지 않는다.
  - `Assets/02. ParkHanSol_TeamLeader_Build & Multi/MCPForUnityLocal/`
  - `Assets/02. ParkHanSol_TeamLeader_Build & Multi/MCPPackages~/`
  - `Library/MCPForUnity/`
  - `Library/PackageCache/com.anklebreaker.unity-mcp*/`
- 로컬 AI 설정 파일은 커밋하지 않는다.
  - `.codex/`
  - `.codex.json`
  - `.codex.toml`
  - `.mcp.json`
  - `mcp.json`
- MCP 때문에 `Packages/packages-lock.json`에 `com.anklebreaker.unity-mcp`가 다시 생기면 오염으로 보고 커밋하지 않는다.
- MCP 외의 멀티 구현 파일, 패키지 설정, 빌드 설정은 정상적으로 Git 변경에 포함한다.
- 커밋 전에는 MCP 파일이 stage되지 않았는지 확인한다.
- 커밋 전에는 요청 범위 밖 Unity 자동 변경이 stage되지 않았는지 확인한다.
- 다른 작업자의 변경을 되돌리지 않는다.

## 멀티플레이 방향

- 현재 멀티 기본 스택은 `Netcode for GameObjects + Unity Transport`이다.
- 4인 이상 온라인 협동은 `Relay + Lobby` 기반으로 확장한다.
- 기본 최대 인원은 8명으로 둔다. 필요하면 Inspector에서 조정한다.
- Photon/Fusion/PUN을 쓰려면 별도 SDK import와 AppId가 필요하므로, 도입 전 명시적으로 확인한다.

## 검증 기준

- 컴파일 에러가 없어야 한다.
- 씬에 필요한 참조가 Inspector에 연결되어 있어야 한다.
- 멀티 씬은 최소한 Host 시작과 로컬 플레이어 스폰을 확인한다.
- Relay/Lobby는 Unity Services 프로젝트 연결 전에는 실제 온라인 접속 실패가 정상일 수 있으므로, 패키지/코드/참조 검증과 실제 서비스 검증을 구분한다.
