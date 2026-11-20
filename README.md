# MiniTranslator

.NET 11 · C# · WPF로 만든 Windows 상주형 미니 번역기입니다.  
전역 단축키(기본 `Ctrl + Space`)로 어디서든 Spotlight 스타일 번역 창을 열고, PC에 설치된 **Claude Code CLI** 또는 **Codex CLI**의 로그인으로 별도의 API 키 없이 실시간 스트리밍 번역을 수행합니다.

## 목차

- [주요 기능](#주요-기능)
- [요구 사항](#요구-사항)
- [사용법 및 단축키](#사용법-및-단축키)
- [번역 엔진](#번역-엔진)
- [아키텍처](#아키텍처)
- [빌드 · 테스트 · 릴리스](#빌드--테스트--릴리스)
- [데이터 저장 위치](#데이터-저장-위치)

## 주요 기능

- **전역 단축키 호출 및 토글**: 어느 앱에서나 단축키(기본 `Ctrl + Space`)로 번역 창을 열고 닫습니다. `Ctrl`·`Alt`·`Win` 중 하나 이상을 포함한 조합으로 자유롭게 변경할 수 있습니다(`Shift` 단독 불가).
- **선택 텍스트 자동 가져오기**: 단축키를 누르면 현재 활성 앱에서 선택한 텍스트를 복사해 입력란에 채우고 기존 클립보드를 복원합니다. `cmd`, PowerShell, Windows Terminal 등 터미널 창에서는 실행 중인 작업을 방해하지 않도록 자동 복사를 건너뜁니다. 가져온 텍스트 즉시 번역 여부는 설정에서 끌 수 있습니다.
- **실시간 스트리밍 번역**: Claude와 Codex 엔진 모두 부분 응답을 스트리밍으로 수신해 번역 결과를 실시간으로 표시합니다. 한 번에 최대 5,000자까지 번역합니다.
- **빠른 엔진·모델 전환**: 번역 창 상단 바의 엔진 버튼에서 `Claude · sonnet / opus / haiku` 및 `Codex · gpt-6.1-terra` 프리셋으로 즉시 전환할 수 있으며, 설정에서 모델명과 추론 강도(Effort)를 세밀하게 지정할 수 있습니다.
- **22개 언어 + 자동 감지**: 기본값은 `영어 → 한국어`이며, 출발 언어 `자동 감지`와 같은 언어 선택 시 자동 맞바꾸기를 지원합니다.
- **Spotlight 스타일 Fluent UI & 인라인 설정**: .NET 11 WPF Fluent 테마와 DWM Acrylic 배경(시스템/라이트/다크 테마), 둥근 모서리, 항상 위 고정(Pin) 버튼을 제공합니다. 설정은 별도 창 없이 번역 창 안에서 전환되며, 닫으면 이전 포커스와 스크롤 위치를 복원합니다. 창 너비 720 DIP 이상에서는 원문·결과가 가로로 나란히 배치됩니다.
- **테마 · 글꼴 · UI 언어**:
  - **테마(6종)**: `시스템`, `라이트`, `다크`, `Tomorrow Night Blue`, `Solarized Light`, `Solarized Dark` — 3×2 스와치 그리드에서 선택 즉시 적용·저장.
  - **앱 글꼴(5종)**: `시스템 기본값`, `맑은 고딕`, `바탕`, `굴림`, `Noto Sans KR` — 미리보기 지원.
  - **UI 언어**: `한국어` / `English` (번역 언어와 별개).
- **시스템 트레이 상주 & 절전 최적화**: Win32 네이티브 트레이 아이콘(`Shell_NotifyIconW`)으로 상주하며 Explorer 재시작 시 아이콘을 자동 복구합니다. 창을 숨기고 유휴 상태가 되면 Windows Efficiency Mode를 활성화하고 Working Set을 정리해 백그라운드 메모리 사용량을 낮춥니다. 사용자별 단일 인스턴스 뮤텍스로 중복 실행을 방지합니다.

## 요구 사항

- **OS**: 64비트 Windows (x64 또는 ARM64)
- **런타임**: .NET 11 Desktop Runtime (설치 파일 사용 시 미설치 환경에서 자동 다운로드·설치)
- **번역 CLI** (둘 중 하나 이상 설치 및 로그인 필요):
  - [Claude Code CLI](https://claude.com/claude-code) (`claude`) — 기본 엔진
  - [Codex CLI](https://github.com/openai/codex) (`codex`) — 선택 엔진
- **소스 빌드 시 추가 요구 사항**: `global.json`에 지정된 .NET 11 SDK (`11.0.100` 이상), 설치 파일 제작 시 [NSIS 3.04 이상](https://nsis.sourceforge.io/Download)

## 사용법 및 단축키

### 단축키

| 범위 | 단축키 | 동작 |
| --- | --- | --- |
| 전역 | `Ctrl + Space` (기본값) | 번역 창 열기 / 닫기 (선택 텍스트 가져오기 포함) |
| 번역 창 | `Ctrl + Enter` | 번역 실행 / 다시 번역 |
| 번역 창 | `Ctrl + Shift + S` | 출발·도착 언어 바꾸기 |
| 번역 창 | `Ctrl + Shift + C` | 번역 결과 복사 |
| 번역 창 | `Ctrl + ,` | 인라인 설정 열기 |
| 번역 창 | `Esc` | 엔진 팝업 닫기 · 설정 닫기 · 창 숨기기 |
| 설정 화면 | `Alt + S` | 설정 저장 |
| 설정 화면 | `Alt + O` / `Alt + T` | 출발 언어 / 도착 언어 선택으로 이동 |

> [!TIP]
> 한국어/중국어 IME에서 `Ctrl + Space`가 입력기 전환에 쓰이고 있다면 설정 → 단축키에서 다른 조합(예: `Ctrl + Alt + T`)으로 변경하세요.

### 실행 인자 및 트레이 동작

| 인자 | 동작 |
| --- | --- |
| *(인자 없음)* | 창을 열지 않고 트레이에서 백그라운드 실행 |
| `--show` | 시작 시 번역 창 열기 |
| `--settings` | 시작 시 설정 화면 열기 |

- **트레이 아이콘**: 좌클릭으로 번역 창을 열고, 우클릭 메뉴에서 **번역 창 열기 / 설정 / 종료**를 선택할 수 있습니다.
- **창 닫기**: 창을 닫거나 `Esc`를 누르면 프로세스가 종료되지 않고 트레이에 상주합니다. 완전 종료는 트레이 메뉴의 **종료**를 사용합니다.
- **동작 옵션** (설정 → 동작):
  - **Windows 로그인 시 자동 실행**: 기본 끔. 현재 사용자(`HKCU\...\Run`)에만 등록되며 로그인 시 트레이로 조용히 시작합니다. 상태는 레지스트리에서 직접 읽으며 JSON에 중복 저장하지 않습니다. 실행 파일 위치를 옮겼다면 옵션을 껐다 저장한 뒤 다시 켜세요.
  - **선택한 텍스트 가져오기**: 기본 켬.
  - **가져온 텍스트 즉시 번역**: 기본 켬. 끄면 단축키로 원문을 가져온 뒤 편집·확인 후 직접 번역할 수 있습니다.
  - **포커스를 잃으면 숨기기**: 기본 켬. 설정 화면이 열려 있을 때는 숨기지 않습니다.

### UI/UX 및 접근성

[WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)의 Windows Fluent 디자인 지침을 WPF 화면에 적용합니다(프레임워크는 .NET 11 WPF를 유지).

- **상태 알림**: 번역 진행·복사·오류 메시지는 `LiveTextBlock`을 통해 UI Automation 라이브 영역(`Polite` / `Assertive`)으로 알립니다.
- **즉시 적용 vs 저장 적용**: 테마와 Codex 로그인/로그아웃은 즉시 적용되며 취소를 눌러도 유지됩니다(테마 저장 실패 시 이전 테마로 복원하고 오류 표시). 그 외 설정은 하단 고정 바의 **저장** 버튼으로 적용합니다.
- **키보드 접근성**: `Tab` 순서, 접근 키(`Alt+S`, `Alt+O`, `Alt+T`), 읽기 전용 결과 창의 키보드 선택·복사를 지원합니다.

## 번역 엔진

MiniTranslator는 API 키를 직접 저장하거나 관리하지 않으며, 로컬에 설치된 CLI의 로그인 세션을 통해 번역을 실행합니다. 엔진을 전환하거나 모델·추론 강도·실행 경로를 변경하면 이전 엔진의 백그라운드 세션은 자동으로 해제됩니다.

### Claude 엔진 (기본값)

[Claude Code CLI](https://claude.com/claude-code)(`claude`)를 `--input-format stream-json --output-format stream-json --include-partial-messages`로 실행해 실시간 스트리밍 번역을 수행합니다.

- **준비**: Claude Code CLI를 설치해 PATH에 두고 터미널에서 `claude auth login`으로 로그인합니다.
- **동작**:
  - 기본 모델은 `sonnet`이며, 설정에서 `opus`·`haiku` 등 별칭이나 전체 모델명과 추론 강도(`low`, `medium`, `high`, `xhigh`, `max`)를 지정할 수 있습니다.
  - 번역 1건마다 세션을 종료해 독립된 프로세스로 처리하며(`MaxTurnsPerSession = 1`), 요청 시간 제한은 90초, 최대 출력 길이는 100,000자입니다.
- **보안 및 격리**:
  - 도구(`--tools ""`), MCP 서버(`--strict-mcp-config`), 사용자 설정·훅·메모리(`--setting-sources ""`), 슬래시 명령(`--disable-slash-commands`), 세션 저장(`--no-session-persistence`)을 모두 비활성화합니다.
  - 매 요청마다 고유 랜덤 XML 경계 태그로 원문을 감싸 프롬프트 인젝션을 방지합니다.
  - 허용 목록에 포함된 환경 변수만 전달하며, 로컬 드라이브 PATH의 안전한 `.exe`/`.com`만 실행하고 세션별 임시 작업 폴더(`%LOCALAPPDATA%\MiniTranslator\work\<guid>`)를 종료 시 정리합니다.
- **제한**: 번역할 때마다 Claude 구독(또는 CLI 인증 수단) 사용량을 소모합니다. `자동 감지` 시 감지된 언어를 반환하지 않으므로 그 상태에서는 언어 바꾸기를 사용할 수 없습니다.

### Codex 엔진

[Codex CLI](https://github.com/openai/codex)(`codex`)를 `codex app-server --listen stdio://` JSON-RPC 서버로 실행해 ChatGPT 구독 로그인으로 실시간 스트리밍 번역을 수행합니다.

- **준비**: Codex CLI를 설치합니다. PATH에 없을 때는 설정 → 번역 엔진 → **Codex 실행 파일 경로**에 로컬 드라이브의 `codex.exe` 전체 경로를 지정합니다(경로를 지정하면 해당 경로만 사용하며 PATH로 폴백하지 않습니다).
- **로그인**: 설정 → 번역 엔진에서 Codex를 선택하면 로그인 상태가 표시됩니다. **로그인**을 누르면 브라우저에서 OpenAI 인증 페이지가 열리고 승인 시 자동 반영됩니다(제한 시간 5분, 취소 가능). CLI가 반환한 URL이 기본 포트의 `https://auth.openai.com`이 아니면 브라우저를 열지 않습니다.
- **동작**:
  - 기본 모델은 `gpt-6.1-terra`이며, 설정에서 `gpt-6.2-sol`, `gpt-6.2-astra`, `gpt-6.1-sol`, `gpt-6-astra`, `gpt-6-luna` 등을 선택·입력하고 추론 강도(`low`, `medium`, `high`)를 지정할 수 있습니다.
  - 프로세스 하나를 재사용하면서 매 번역마다 새로운 ephemeral 스레드(`ephemeral: true`)를 생성합니다. 번역 20건마다, 3분 유휴 시, 옵션·경로·엔진 변경 시, 또는 오류·취소·시간 초과(90초) 시 프로세스를 종료·교체합니다.
- **보안 및 격리**:
  - MiniTranslator 전용 프로필(`%LOCALAPPDATA%\MiniTranslator\codex`)을 `CODEX_HOME`으로 사용해 평소 Codex 설정·기록과 분리합니다(이전 `%APPDATA%\MiniTranslator\codex`가 있으면 시작 시 자동 이동).
  - 허용 목록 기반 환경 변수 필터링으로 `OPENAI_API_KEY`, `CODEX_API_KEY`, `CODEX_ACCESS_TOKEN`, `OPENAI_BASE_URL` 등을 제거합니다.
  - `sandbox: read-only`, `approvalPolicy: untrusted`, `web_search: disabled`, `project_doc_max_bytes: 0`, MCP·스킬·셸·앱·플러그인·브라우저 등 에이전트 기능 16종을 모두 비활성화하며, 서버가 보내는 도구·승인 요청은 모두 거절(`-32601`)합니다.
- **제한**: 번역할 때마다 ChatGPT 구독 사용량을 소모합니다. `자동 감지` 시 감지된 언어를 반환하지 않아 그 상태에서는 언어 바꾸기를 사용할 수 없습니다. `codex app-server`는 실험적 인터페이스이므로 Codex CLI 버전에 따라 동작이 달라질 수 있습니다.

## 아키텍처

하나의 솔루션(`MiniTranslator.slnx`) 안에 앱(`apps/`), 재사용 패키지(`packages/`), 테스트(`tests/`)를 두는 모노레포이며, **Clean Architecture**와 **Functional Core / Imperative Shell** 원칙을 따릅니다.

### 워크스페이스 구조

```text
MiniTranslator/
├── MiniTranslator.slnx
├── Directory.Build.props                공통 빌드 속성 · TFM 변수 · 버전(0.2.0)
├── Directory.Packages.props             NuGet 중앙 버전 관리 (런타임 의존성은 CommunityToolkit.Mvvm 단일)
├── global.json                          .NET 11 SDK 버전 고정
├── apps/
│   └── MiniTranslator.Wpf/              WPF 실행 앱 · TranslatorWindow · SettingsView · AppShell · CompositionRoot
├── packages/
│   ├── core/
│   │   ├── MiniTranslator.Domain/       순수 도메인: 값 객체 · 규칙 · Result/Error         (net11.0, 외부 의존성 0)
│   │   └── MiniTranslator.Application/  유스케이스 · SettingsEditor/Store · 포트(Abstractions) (net11.0, Domain만)
│   ├── presentation/
│   │   └── MiniTranslator.Presentation/ MVVM ViewModels · KeyChord · 현지화(.resx), WPF 비의존 (net11.0)
│   └── adapters/
│       ├── MiniTranslator.Adapters.ClaudeCli/     ITranslator · ITranslatorSession (Claude CLI, net11.0)
│       ├── MiniTranslator.Adapters.CodexCli/      ITranslator · ITranslatorSession · ICodexAccount (Codex CLI, net11.0)
│       ├── MiniTranslator.Adapters.JsonSettings/  ISettingsRepository (JSON 파일, net11.0)
│       └── MiniTranslator.Adapters.Windows/       Win32 전역 단축키 · 트레이 · 선택 복사 · 자동 실행 · DWM · 절전 (net11.0-windows)
├── scripts/                             실행(run.ps1/sh) · 릴리스(release.ps1/sh) · NSIS 설치 스크립트
└── tests/
    ├── Directory.Build.props            *.Tests 프로젝트 xUnit 공통 설정
    ├── MiniTranslator.Testing/          공유 테스트 더블(가짜 포트)
    ├── MiniTranslator.<패키지>.Tests/   패키지·WPF 화면 단위/회귀 테스트
    ├── MiniTranslator.Architecture.Tests/ 계층 의존성 규칙 자동 검증
    └── *.Tests.ps1                      런타임 설치·실행·릴리스 스크립트 테스트
```

### 계층 의존성 규칙

의존성은 항상 안쪽(`Domain`)을 향하며, `tests/MiniTranslator.Architecture.Tests`가 어셈블리 참조를 자동으로 검사해 규칙 위반을 차단합니다. DI 컨테이너 없이 `apps/MiniTranslator.Wpf/CompositionRoot.cs`의 `AppContainer`에서 Pure DI로 전체를 조립하고, CLI 어댑터는 `Lazy<T>`로 지연 초기화합니다.

```text
              apps/MiniTranslator.Wpf  (Composition Root — Pure DI)
               │            │                    │
               ▼            ▼                    ▼
        Presentation    Adapters.*  ──implements──►  Application.Abstractions (포트)
        (ViewModels)    Claude · Codex · Json · Windows          │
               │            │                                    │
               └────────────┴─────────►  Application  ◄──────────┘
                                              │
                                              ▼
                                           Domain
```

| 패키지 | 책임 | 참조 가능 |
| --- | --- | --- |
| `Domain` | 불변 값 객체(`HotkeyGesture`, `LanguagePair`, `TranslationRequest`, `EngineOptions`)와 설정 병합·검증 규칙. 예외 대신 `Result<T>`/`Error` 반환. | 없음 |
| `Application` | 유스케이스(`TranslateText`, `ChangeLanguages`, `ChangeEngine`, `CaptureSelection`), 엔진 라우팅(`SelectedEngineTranslator`), 설정 세션(`SettingsEditor`), 상태 저장소(`SettingsStore`), 단축키 관리(`HotkeyManager`)와 포트. | `Domain` |
| `Presentation` | UI 프레임워크에 독립적인 ViewModel(`TranslatorViewModel`, `SettingsViewModel`)과 다국어 리소스(`Strings.resx` / `Strings.en.resx`). 설정 화면은 `SettingsEditor` 단일 의존성만 보유. | `Domain`, `Application` |
| `Adapters.*` | 포트 구현. 어댑터끼리는 서로 참조하지 않으며 각자 팩토리 진입점(`Create*`)만 공개하고 구현체는 `internal`로 캡슐화. | `Domain`, `Application` |
| `MiniTranslator.Wpf` | XAML 뷰(`TranslatorWindow`, 인라인 `SettingsView`), 트레이 셸(`AppShell`), 테마/글꼴 리소스, `AppContainer`. | 전체 |

### 순수 코드와 부수효과 분리

| 패키지 | 순수 로직 (판단·변환) | 부수효과 경계 (실행·I/O) |
| --- | --- | --- |
| `Domain` | 전부 — `HotkeyGesture`, `LanguagePair`, `AppSettings.MergeDraft`·`WithTheme`·`WithEngineSelection`, `EngineOptions`, `TranslationResult` | — |
| `Application` | Domain 규칙 기반 상태 전환 결정 | `SettingsEditor`(설정 저장·OS 자동 실행/단축키 롤백), `SettingsStore`, `HotkeyManager`, `SelectedEngineTranslator`, 유스케이스 |
| `ClaudeCli` | `ClaudeCliProtocol` (인자·스트림 JSON 생성·델타/결과 파싱) | `ClaudeCliTranslator` (세션·타임아웃·유휴 해제), `ClaudeProcess` (자식 프로세스·샌드박스) |
| `CodexCli` | `CodexCliProtocol` (인자·JSON-RPC 생성·파싱·인증 URL 검증) | `CodexCliTranslator`, `CodexCliAccount`, `CodexConnection`, `CodexProcess` |
| `JsonSettings` | `SettingsMapper` (JSON 문서 ↔ 도메인 모델) | `JsonSettingsRepository` (파일 읽기/쓰기) |
| `Windows` | `CopyShortcut`, `ClipboardCapture`, `KeyboardLayoutAliases`, `TerminalProcesses` | `ClipboardSelectionReader`, `Win32GlobalHotkey`, `Win32TrayIcon`, `WpfClipboard`, `WindowsStartupRegistration`, `WindowBackdrop`, `ProcessMemoryPolicy` |

- **동시 설정 보존**: `SettingsEditor`는 화면을 열 때의 원본 스냅샷(`Original`)을 보관하고 저장 시점에 최신 설정과 `current.MergeDraft(draft, original)`로 병합합니다. 편집 중 다른 곳에서 바뀐 언어나 즉시 저장한 테마를 덮어쓰지 않습니다.
- **OS 등록 복구**: 저장 시 자동 실행 레지스트리 변경 → 전역 단축키 재등록 → JSON 파일 저장 순서로 진행하며, 중간에 실패·취소되면 이미 적용한 단축키와 자동 실행 설정을 원복하고 복구 오류까지 별도로 보고합니다.

## 빌드 · 테스트 · 릴리스

### 전체 검증 후 실행

```powershell
# PowerShell
.\scripts\run.ps1
```

```bash
# Windows Git Bash / MSYS
bash scripts/run.sh
```

두 스크립트는 .NET 11 SDK 확인 → 복원 → `dotnet format` 포맷팅 → 린팅 검증(`--verify-no-changes`) → 빌드 → 전체 테스트 → 앱 실행(`--show` 기본 전달) 순서로 동작하며, 어느 단계든 실패하면 즉시 중단합니다. 포맷팅 단계는 C# 소스 파일을 수정합니다.

개별 명령으로 실행할 때:

```powershell
dotnet build MiniTranslator.slnx
dotnet test MiniTranslator.slnx
dotnet run --project apps/MiniTranslator.Wpf -- --show
```

### 테스트 실행

```powershell
# 전체 .NET 테스트 (Domain · Application · Presentation · Adapters · Architecture · WPF)
dotnet test MiniTranslator.slnx

# 특정 프로젝트만 실행
dotnet test tests/MiniTranslator.Presentation.Tests
dotnet test tests/MiniTranslator.Wpf.Tests

# PowerShell 스크립트 테스트
powershell -NoProfile -File tests/DesktopRuntimeInstaller.Tests.ps1
powershell -NoProfile -File tests/RunScripts.Tests.ps1
powershell -NoProfile -File tests/ReleaseScripts.Tests.ps1
```

- `MiniTranslator.Wpf.Tests`: 실제 XAML을 STA 스레드에 로드해 6개 테마 즉시 적용·저장 실패 복원, 글꼴 전환, UI 언어 전환, 인라인 설정 내비게이션, 가로/세로 반응형 레이아웃, 접근성 속성을 검증합니다.
- `DesktopRuntimeInstaller.Tests.ps1`: 실제 런타임을 설치하지 않고 아키텍처·버전 감지, 다운로드 호스트·Microsoft Authenticode 서명 검증, 권한/설치 오류, 재부팅 코드(`3010`)와 임시 파일 정리를 검증합니다.

### 릴리스 설치 파일 만들기

Windows, .NET 11 SDK, [NSIS 3.04 이상](https://nsis.sourceforge.io/Download)(`makensis.exe`를 PATH 또는 기본 `Program Files` 경로에 설치)이 필요합니다.

```powershell
# PowerShell
.\scripts\release.ps1
```

```bash
# Windows Git Bash / MSYS
bash scripts/release.sh
```

복원 → 포맷팅 → 린팅 → Release 빌드 → 전체 테스트 → `win-x64`·`win-arm64` framework-dependent 게시(ReadyToRun 활성화, `PublishSingleFile=false`) → NSIS 설치 파일 생성 순서로 진행합니다.

- **게시 폴더**: `artifacts/publish/win-x64/`, `artifacts/publish/win-arm64/`
- **설치 파일**:
  - `artifacts/MiniTranslator-<Version>-win-x64-Setup.exe`
  - `artifacts/MiniTranslator-<Version>-win-arm64-Setup.exe`

설치 파일 특징:
- 버전은 `Directory.Build.props`의 `<Version>` 속성(현재 `0.2.0`)에서 읽습니다.
- 설치 전 동일 아키텍처의 .NET 11 Desktop Runtime 존재 여부를 확인하고, 없으면 Microsoft 공식 배포처에서 다운로드해 Authenticode 서명을 검증한 뒤 자동 설치합니다(최초 런타임 설치 시 인터넷 연결 및 UAC 승인 필요, 이미 설치되어 있으면 생략).
- 앱 자체는 관리자 권한 없이 `%LOCALAPPDATA%\Programs\MiniTranslator`에 설치되며 시작 메뉴 바로가기와 제어판 제거 항목을 등록합니다.
- 설치·제거 전 실행 중인 앱을 트레이 메뉴에서 종료해야 합니다. 제거 시 자동 실행 레지스트리를 정리하며, 사용자 설정과 Codex 로그인 정보를 함께 삭제할지 확인합니다(기본값은 보존).
- 생성된 실행 파일과 설치 파일에는 코드 서명을 하지 않습니다.

### 새 패키지 추가하기

1. `packages/<계층>/MiniTranslator.<이름>/`에 프로젝트를 생성하고 `TargetFramework`는 기본적으로 `$(PortableTargetFramework)`를 사용합니다(Win32/WPF가 필수일 때만 `$(WindowsTargetFramework)`).
2. NuGet 패키지 버전은 `Directory.Packages.props`에만 선언하고, `.csproj`에는 버전 없이 `<PackageReference Include="..." />`만 작성합니다.
3. 대응하는 `tests/MiniTranslator.<이름>.Tests/` 프로젝트를 추가하고 둘 다 `MiniTranslator.slnx`에 등록합니다.
4. `tests/MiniTranslator.Architecture.Tests/DependencyRuleTests.cs`의 `AllowedWorkspaceReferences`에 허용 의존성을 등록합니다.

## 데이터 저장 위치

| 항목 | 위치 |
| --- | --- |
| 앱 설정 파일 | `%APPDATA%\MiniTranslator\settings.json` |
| Codex 전용 프로필 (`CODEX_HOME`) | `%LOCALAPPDATA%\MiniTranslator\codex` *(구 `%APPDATA%\MiniTranslator\codex` 자동 마이그레이션)* |
| CLI 세션 임시 작업 디렉터리 | `%LOCALAPPDATA%\MiniTranslator\work\<guid>` *(세션 종료 시 자동 삭제)* |
| 기본 설치 폴더 | `%LOCALAPPDATA%\Programs\MiniTranslator` |
| Windows 자동 실행 등록 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`MiniTranslator`) |

---

© 2026 kangnodle@gmail.com. All rights reserved.
