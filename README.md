# MiniTranslator

.NET 11 · C# · WPF로 만든 Windows 상주형 미니 번역기입니다.
전역 단축키(기본 `Ctrl + Space`)로 어디서든 번역 창을 열고, 로그인이나 API 키 없이 Google 번역으로 번역합니다.

## 주요 기능

| 기능 | 설명 |
| --- | --- |
| 전역 단축키 변경 | 설정 → 단축키 입력란을 클릭하고 원하는 조합을 누르면 기록됩니다. `Ctrl`·`Alt`·`Win` 중 하나 이상 필요 (Shift 단독 불가). 다른 앱이 이미 사용 중이면 이전 단축키가 그대로 유지됩니다. |
| 출발/도착 언어 | 번역 창 상단 또는 설정에서 변경. 기본값 **영어 → 한국어**. 출발 언어는 `자동 감지` 가능, 같은 언어를 고르면 자동으로 서로 바뀝니다. |
| Windows Fluent UI/UX | .NET 11 WPF 내장 Fluent 테마 사용 — Fluent 타이포그래피·카드·강조 버튼·인라인 오류 안내. 넓은 창에서는 원문/결과가 나란히 배치됩니다. |
| 앱 테마 | 설정 → 모양에서 **시스템 / 라이트 / 다크 / Tomorrow Night Blue / Solarized Light / Solarized Dark**를 콤보박스로 선택합니다. 열린 모든 창에 즉시 적용·자동 저장하며, 다음 실행에도 유지됩니다. 시스템은 Windows 테마를 따릅니다. |
| 선택 텍스트 바로 번역 | 단축키를 누르면 다른 앱에서 선택한 텍스트를 복사해 즉시 번역 (기존 클립보드 텍스트는 복원). 설정에서 끌 수 있습니다. |
| 트레이 상주 | 알림 영역 아이콘 클릭 → 번역 창, 우클릭 → 설정/종료. 중복 실행 방지. |

번역 창 단축키: `Ctrl+Enter` 번역 · `Ctrl+Shift+S` 언어 바꾸기 · `Ctrl+Shift+C` 결과 복사 · `Ctrl+,` 설정 · `Esc` 숨기기

언어 선택: `Alt+O` 출발 언어 · `Alt+T` 도착 언어. `Tab`으로 원문, 번역, 읽기 전용 결과, 복사에 접근할 수 있습니다.

## UI/UX 기준

[WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)에서 사용하는 Windows Fluent 디자인 지침을 WPF 화면에 적용합니다. 실행 프레임워크는 WPF를 유지하며, WinUI 3 컨트롤이나 Windows App SDK로 전환한 것은 아닙니다.

- [타이포그래피](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography): 제목 28, 섹션 제목 20, 본문 14, 보조 안내 12 DIP. 시스템 테마의 색상 리소스와 기본 컨트롤 템플릿을 사용합니다.
- 번역 창: 언어 라벨, 원문/결과 카드, 빈 상태 안내, 번역 중 표시를 제공합니다. 콘텐츠 너비 720 DIP부터 두 카드를 가로로 배치하고, 그보다 좁으면 세로로 배치합니다.
- [상태 안내](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/infobar): 오류를 해당 화면에 표시하고, 복사·번역 진행·오류 메시지는 UI Automation 라이브 영역으로 알립니다.
- [설정 컨트롤](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/combo-box): 테마는 콤보박스에서 선택해 즉시 적용·저장합니다. 취소해도 선택한 테마는 유지되며, 테마 저장이 실패하면 이전 테마로 복원하고 오류를 표시합니다. 다른 옵션은 저장 버튼으로 적용합니다. 저장/취소 버튼은 스크롤 영역 밖에 고정합니다.
- [접근성](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview): 입력 라벨과 접근 키, 단축키 정보, 읽기 전용 결과의 키보드 선택·복사를 제공합니다.

> 한국어/중국어 IME에서 `Ctrl + Space`가 입력기 전환에 쓰이고 있다면 다른 조합(예: `Ctrl + Alt + T`)으로 바꾸세요.

## 번역 서비스

Google 번역의 공개 엔드포인트(`translate.googleapis.com/translate_a/single`, `client=gtx`)를 사용하므로 **Google 계정 로그인, API 키, Google Cloud 프로젝트가 필요 없습니다.**

번역은 URL 인코딩한 텍스트를 GET 요청으로 전송합니다. 공개 엔드포인트는 Google의 자동화 요청 차단이나 네트워크 단위 제한으로 HTTP 429를 반환할 수 있습니다. 이 경우 잠시 후 다시 시도하세요. 요청 방식을 수정해도 Google 측 차단을 해제할 수는 없습니다.

> 이 엔드포인트는 공식 지원 API가 아닙니다. 사전 공지 없이 바뀌거나, 짧은 시간에 요청이 많으면 일시적으로 차단(HTTP 429)될 수 있습니다.

설정 저장 위치: `%APPDATA%\MiniTranslator\settings.json`

## 워크스페이스 구조

하나의 솔루션(`MiniTranslator.slnx`) 안에 앱(`apps/`), 재사용 가능한 패키지(`packages/`), 테스트(`tests/`)를 두는 모노레포입니다.
공통 빌드 설정은 루트의 `Directory.Build.props`, 모든 NuGet 버전은 `Directory.Packages.props`(Central Package Management), SDK 버전은 `global.json`에서 한 번만 관리합니다.

```
MiniTranslator/
├── MiniTranslator.slnx
├── Directory.Build.props            공통 속성 · TFM 변수(PortableTargetFramework / WindowsTargetFramework)
├── Directory.Packages.props         패키지 버전 중앙 관리
├── global.json                      .NET SDK 고정
├── apps/
│   └── MiniTranslator.Wpf/          실행 파일 · Views · Controls · AppShell · CompositionRoot
├── packages/
│   ├── core/
│   │   ├── MiniTranslator.Domain/          순수: 값 객체 · 규칙 · Result/Error         (net11.0, 의존성 0)
│   │   └── MiniTranslator.Application/     유스케이스 · 포트(Abstractions)              (net11.0, Domain만)
│   ├── presentation/
│   │   └── MiniTranslator.Presentation/    ViewModels(MVVM) · KeyChord, WPF 비의존      (net11.0)
│   └── adapters/
│       ├── MiniTranslator.Adapters.GoogleTranslate/   ITranslator  — HTTP               (net11.0)
│       ├── MiniTranslator.Adapters.JsonSettings/      ISettingsRepository — 파일        (net11.0)
│       └── MiniTranslator.Adapters.Windows/           IGlobalHotkey · IClipboard · ITextSelectionReader — Win32 (net11.0-windows)
└── tests/
    ├── Directory.Build.props        *.Tests 프로젝트에 xUnit 공통 적용
    ├── MiniTranslator.Testing/      공유 테스트 더블(가짜 포트)
    ├── MiniTranslator.<패키지>.Tests/   패키지마다 1:1
    └── MiniTranslator.Architecture.Tests/   의존성 규칙 자동 검증
```

## 아키텍처

Clean Architecture — 의존성은 항상 안쪽(Domain)을 향합니다.

```
              apps/MiniTranslator.Wpf  (Composition Root: 유일하게 모든 패키지를 앎)
               │            │                    │
               ▼            ▼                    ▼
        Presentation    Adapters.*  ──implements──►  Application.Abstractions (포트)
     (ViewModels)       Google · Json · Windows          │
               │            │                            │
               └────────────┴──────►  Application  ◄─────┘
                                           │
                                           ▼
                                        Domain
```

| 패키지 | 책임 | 참조 가능 |
| --- | --- | --- |
| `Domain` | 불변 값 객체와 규칙. 생성 시 검증되는 "항상 유효한" 타입(`HotkeyGesture`, `LanguagePair`, `TranslationRequest`). 오류는 예외 대신 `Result<T>`/`Error` 값. | 없음 |
| `Application` | 유스케이스(`TranslateText`, `ChangeLanguages`, `CaptureSelection`), 설정 편집 세션(`SettingsEditor`)과 포트. 부수효과의 순서·복구를 조율하며 DI 컨테이너에도 의존하지 않습니다. | Domain |
| `Presentation` | UI 프레임워크와 무관한 ViewModel. 입력·화면 상태·현지화를 담당하며 설정 작업은 `SettingsEditor`에 요청합니다. WPF 없이 단위 테스트됩니다. | Domain, Application |
| `Adapters.*` | 포트 구현. 어댑터끼리는 서로 모르며, 각자 `AddXxx()` 확장 메서드 하나만 공개합니다(구현 클래스는 `internal`). | Domain, Application |
| `Wpf` (앱) | XAML 뷰, 트레이 셸, `CompositionRoot`에서 전체 조립. | 전부 |

위 "참조 가능" 열은 `tests/MiniTranslator.Architecture.Tests`가 어셈블리 참조를 검사해 강제합니다. 예를 들어 Application에 `System.Text.Json`을 쓰면 테스트가 실패합니다.

### 순수 코드와 부수효과 코드 (Functional Core / Imperative Shell)

부수효과가 있는 클래스는 얇은 껍데기로 두고, 판단 로직은 순수 함수로 분리해 단위 테스트합니다.

| 패키지 | 순수 (결정) | 부수효과 (실행) |
| --- | --- | --- |
| Domain | 전부 — `HotkeyGesture`, `LanguagePair`, `AppSettings.MergeDraft`·`WithTheme`(설정 병합·검증), `TranslationResult.DetectionNote`, `HotkeyModifiers.Names()` | — |
| Application | Domain의 규칙으로 변경할 값을 결정 | `SettingsEditor`(설정 편집·저장·자동 실행 복구), `SettingsStore`(상태+저장), `HotkeyManager`(등록 상태), 번역·선택 텍스트 유스케이스 |
| GoogleTranslate | `GoogleTranslateProtocol` (GET URI 생성, 응답 파싱) | `GoogleTranslator` (HTTP) |
| JsonSettings | `SettingsMapper` (문서 ↔ 도메인) | `JsonSettingsRepository` (파일) |
| Windows | `CopyShortcut` (키 상태 값 → 주입할 키 순서), `ClipboardCapture`(복사 결과 검증), `KeyboardLayoutAliases` (한글 자판 Ctrl+Space → 한자 키) | `ClipboardSelectionReader`(키 상태 조회·SendInput), `Win32GlobalHotkey`, `WpfClipboard`, `WindowsStartupRegistration` |

설정 화면이 알고 있던 저장소·단축키·자동 실행·저장 유스케이스 네 가지 의존성은 `SettingsEditor` 하나로 모았습니다. 편집 세션은 화면을 열 때의 설정과 자동 실행 상태를 보관하며, 화면은 사용자 입력과 결과 표시만 담당합니다. `SettingsEditor`는 화면마다 새로 만들고(`Transient`), `SettingsStore`와 `HotkeyManager`는 앱 전체에서 공유합니다(`Singleton`).

설정 병합은 순수 함수 `current.MergeDraft(draft, original)`로 처리합니다. 사용자가 실제로 수정한 필드만 적용하므로 편집 중 다른 화면이 저장한 언어·동작 설정과 즉시 저장한 테마를 보존합니다. 병합은 저장 순서가 도달했을 때 최신 설정으로 다시 계산합니다. UI 언어·테마 검증도 Domain에서 수행해 화면 외 호출에도 같은 규칙을 적용합니다.

자동 실행 변경과 단축키 등록 후 JSON을 저장하고, 저장 실패·취소·예외 시 적용한 OS 설정을 복구합니다. 저장 오류와 복구 오류를 별도 값으로 반환하므로 화면에서 각각 현지화할 수 있으며, 자동 실행 복구 실패 후 재시도도 실제 적용된 상태를 기준으로 처리합니다. 파일과 OS 등록은 단일 원자적 트랜잭션이 아니므로 다른 앱이 단축키를 점유하거나 OS 등록 복구가 실패하면 복구 오류를 사용자에게 표시합니다.

아키텍처 테스트는 계층의 어셈블리 의존성과 설정 화면의 단일 Application 의존성을 검사합니다. Domain 테스트는 순수 병합·검증을, Application 테스트는 가짜 포트로 부수효과·복구·취소를, Presentation/WPF 테스트는 화면 동작을 검증합니다.

## 빌드 · 테스트 · 실행

Windows와 `global.json`에 지정된 .NET SDK가 필요합니다. 전체 검증 후 실행:

```powershell
.\scripts\run.ps1 --show
```

Windows의 Git Bash/MSYS에서는:

```bash
bash scripts/run.sh --show
```

두 스크립트는 의존성 복원 → 솔루션 전체 C# 포맷팅 → 린팅 검사 → 빌드 → 테스트 → 앱 실행 순서로 동작합니다.
포맷팅은 소스 파일을 수정하며, 어느 단계든 실패하면 즉시 중단합니다. 추가 인자는 앱에 전달됩니다.
인자 없이 실행하면 트레이에 상주하며, 트레이 메뉴의 **종료**로 프로세스를 끝냅니다.

명령을 따로 실행하려면:

```powershell
dotnet build
dotnet test
dotnet run --project apps/MiniTranslator.Wpf
```

실행 인자: `--show` (시작 시 번역 창 열기), `--settings` (시작 시 설정 열기)

설정 → 언어 → **UI 언어**에서 **한국어 / English**를 선택하고 **저장**을 누르면 메뉴와 화면 언어가 바로 바뀝니다.
기본 UI 언어는 한국어이며, 선택은 다음 실행에도 유지됩니다. 취소하면 언어가 바뀌지 않습니다.
UI 언어는 번역할 출발/도착 언어와 별개입니다.

Windows 로그인 시 자동 실행하려면 설정 → 동작 → **Windows 로그인 시 자동 실행**을 켜고 **저장**을 누릅니다.
기본값은 꺼짐이며, 현재 사용자에게만 적용됩니다. 로그인 후에는 창을 열지 않고 트레이에서 실행됩니다.
취소하면 변경되지 않으며, 끄고 저장하거나 앱을 제거하면 자동 실행 등록이 삭제됩니다.
자동 실행 상태는 Windows 등록값에서 읽으며 JSON 설정에는 중복 저장하지 않습니다.
실행 파일을 옮겼다면 옵션을 끄고 저장한 뒤 다시 켜서 새 경로를 등록하세요.
Windows 설정이나 작업 관리자에서 시작 앱을 별도로 차단했다면 해당 화면에서 MiniTranslator를 다시 사용하도록 설정해야 합니다.

특정 패키지만 테스트: `dotnet test tests/MiniTranslator.Presentation.Tests`

WPF 화면 회귀 검사: `dotnet test tests/MiniTranslator.Wpf.Tests`. 실제 XAML을 로드해 여섯 테마의 콤보박스 접근성, 즉시 적용·저장 및 실패 시 복원, 열린 창의 색상 변경과 새 테마의 글자 대비, 최소 창 크기, 가로/세로 배치 전환, 진행/오류/결과 상태와 접근성 속성을 확인합니다.

### 릴리스 설치 파일 만들기

Windows와 .NET SDK, [NSIS 3](https://nsis.sourceforge.io/Download)가 필요합니다.
`makensis.exe`를 PATH에 추가하거나 NSIS를 기본 `Program Files` 폴더에 설치합니다.

```powershell
.\scripts\release.ps1
```

Windows의 Git Bash/MSYS에서는 `bash scripts/release.sh`를 실행합니다.
두 진입점은 같은 PowerShell 작업을 사용하며, 복원 → 포맷팅 → 린팅 → Release 빌드 → 전체 테스트 → 단일 실행 파일 번들링 → NSIS 설치 파일 생성 순서로 진행합니다.
실패하면 즉시 중단하며, 포맷팅은 C# 소스 파일을 수정합니다.

- 번들: `artifacts/publish/win-x64/MiniTranslator.exe`
- 설치 파일: `artifacts/MiniTranslator-<Version>-win-x64-Setup.exe`

버전은 앱 프로젝트의 MSBuild `Version` 속성에서 읽습니다(기본값은 `Directory.Build.props`의 `0.2.0`).
[런타임을 포함한 단일 파일 배포](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)를 사용하므로 대상 PC에 .NET을 별도로 설치할 필요가 없습니다.
설치 위치 기본값은 `%LOCALAPPDATA%\Programs\MiniTranslator`이며, 관리자 권한 없이 시작 메뉴 바로가기와 제거 항목을 등록합니다.
설치·제거 전에 실행 중인 앱을 트레이 메뉴에서 종료하세요. 제거 시 사용자 설정은 보존하며, 생성한 실행 파일에는 코드 서명을 하지 않습니다.

### 새 패키지 추가하기

1. `packages/<계층>/MiniTranslator.<이름>/`에 프로젝트를 만들고 `TargetFramework`는 `$(PortableTargetFramework)`를 기본으로 씁니다(Win32/WPF가 꼭 필요할 때만 `$(WindowsTargetFramework)`).
2. NuGet 버전은 `Directory.Packages.props`에만 적고, csproj에는 `<PackageReference Include="..." />`만 씁니다.
3. `tests/MiniTranslator.<이름>.Tests/`를 만들고 `MiniTranslator.slnx`에 둘 다 등록합니다.
4. `Architecture.Tests`의 `AllowedWorkspaceReferences`에 허용 의존성을 추가합니다.
