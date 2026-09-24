# CreatureManager 구조 개선 및 검증 기록

2026-09-24. 실제 `main` 체크아웃에서 검토·수정·Debug 검증·커밋했다. 기준 HEAD는 `1f1b365b76b673ef5dc25021228494285ea3dc9a`이며, 코드 변경의 최종 커밋은 `935a402`다. 버전은 **1.1.14**로 유지했다. 브랜치/worktree 생성, push, Release 빌드, ZIP 생성, 게시를 하지 않았다.

## 기준 상태와 검토 범위

- 전역 `C:\Users\blizz\.codex\AGENTS.md`, 사용자 작업 지침, 프로젝트 README·빌드 설정·기존 호환성/레벨 보존 문서를 확인했다. 상위 디렉터리와 프로젝트에서 추가 AGENTS.md는 발견하지 못했다.
- 기존 설명은 현재 소스와 대조했다. `docs/valheim-1.0.7.md`는 1.1.13의 게임 1.0.7 대응, `docs/level-persistence-fix-2026-09-15.md`는 이후 저장 레벨 보존 변경의 근거다. 기존 외부 모드 분석 문서를 현재 모드의 실행 구조로 간주하지 않았다.
- 전역 `C:\Users\blizz\.codex\references\valheim\INDEX.md`에서 역할별 원본·추출 상태를 확인했다. 현재 설치/빌드 참조는 **Windows x64 Valheim 1.0.15 클라이언트 b25390630**이다. 설치된 `assembly_valheim.dll`과 보존 원본의 SHA-256이 `59F53FB55D99D22A33E8ED094EEC8D21E9F133543BCE92BC3D80DCE44033ADB1`로 일치하며, 해당 원본의 `Version.CurrentVersion`도 `1.0.15`다. 서버 검사는 **1.0.15 b25390671**의 보존 원본을 사용했다. 이는 현재 참조와 격리 검사 기준이며 실제 실행 지원 범위를 새로 선언하는 변경이 아니다.
- 새 게임 자료 수집·전체 재추출·전 버전 재검사는 하지 않았다. 이번에 관련된 기존 원본의 `BaseAI`, `Character`, `VisEquipment`, `ZRoutedRpc`, `ZNet`과 관리형 검사 대상만 참고했다.
- 편집·빌드·커밋은 주 세션만 담당했고, 병렬 검토는 읽기 전용으로 Domain/YAML, Level/Karma/권한, HUD/외형/텍스처 영역을 나눴다.

### 실제 실행·빌드 경로

| 대상 | 확인한 경로/책임 |
| --- | --- |
| 플러그인 | `CreatureManager.sln` → `CreatureManager.csproj`, .NET Framework 4.8 라이브러리. `Plugin.cs`의 `CreatureManagerPlugin : BaseUnityPlugin`, `[BepInPlugin("sighsorry.CreatureManager", ...)]`. 프리로더 패처가 아니다. |
| 초기화/정리 | `Awake` → 설정 bind, Harmony 패치, localization/domain 초기화, watcher/RPC 등록. `Update` → pending 작업 및 0.5초 간격 maintenance. `OnDestroy` → `CleanupRuntime` → 구독 해제·watcher/manager 정리·패치 해제. 초기화 실패 cleanup도 유지한다. |
| 패치 진입 | `CreatureHarmonyPatches.cs`의 게임 lifecycle/행동/UI 훅, `CreatureSwiftMovementSystem.cs`의 이동 transpiler, 기존 미커밋 `CreatureLoot.cs`의 드롭 transpiler. Unity 메시지와 동적 optional patch는 직접 참조 검색 외에 별도 확인했다. |
| 병합 | `ILRepack.targets:ILRepacker`가 플러그인 + `bin/Debug/Libs/ServerSync.dll` + `YamlDotNet.dll`을 최종 `bin/Debug/CreatureManager.dll`로 Internalize 병합한다. |
| 고정 라이브러리 | `Libs/ServerSync.dll`과 실제 병합 입력의 SHA-256은 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`; 전역 `valheim-1.0.7-r1` manifest와 일치. YamlDotNet 16.3.0. 라이브러리를 교체하지 않았다. |
| 게임 접근 | `build/GameReferences.targets`는 기존 방식대로 `assembly_valheim`/`assembly_guiutils`의 **obj 내 컴파일 사본**만 publicize한다. 분석과 Mono 검사는 원본 DLL을 사용했다. 최종 접근 특성도 검사했다. 원본 접근 제한과 컴파일 성공은 별개이며 이번 변경은 새 비공개 게임 접근을 추가하지 않는다. |
| Debug 배포 | `DeployGamePlugin`은 ILRepacker 뒤 최종 DLL만 `C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CreatureManager.dll`에 복사한다. 각 코드 단계에서 원본/복사본 SHA-256 일치를 확인했다. |
| 패키지 | manifest의 BepInEx 의존성은 이미 `denikson-BepInExPack_Valheim-5.4.2350`. 버전/의존성/Release 패키징은 변경하지 않았다. |
| 도구 | 솔루션에 net8 `YamlSyncContractCheck` 포함. `GameCompatibilityCheck`는 별도 net48 실행 도구이며 게임 Mono에서 원본 참조로 격리 검사한다. `ModifierIconPreview`는 별도 개발 도구로 이번 변경·실행 대상이 아니다. |

설정 검토에는 `Plugin.cs`의 BepInEx 키/기본값/live 이벤트, Domain의 YAML 로드·검증·동기화·rollback, 생성 reference 경로를 포함했다. `creatures`, `attacks`, `projectile`, `ai`, `levels`의 base/split YAML과 단일 `factions.yml`/`karma.yml` 정책을 유지했다. DTO의 omitted/null/empty 구분, ZDO snapshot v2, localization payload, texture manifest/chunk 형식은 변경하지 않았다. embedded 영/한 번역, sample YAML, PNG의 등록·사용 경로를 확인했고 최종 리소스 해시를 비교했다.

선택적 연동은 FeedLikeGrandma의 동적 타입/메서드 탐색과 재시도, ExpandWorldData의 위치 조회, ServerDevcommands의 선행 권한 검사 경계, 기존 드롭 작업의 DropNSpawn 감지/Drop That 패치 조합을 확인했다. 네 모드(CLLC, StarLevelSystem, MonsterDB, MonsterModifiers)의 BepInIncompatibility도 유지했다. 이 연동을 hard dependency로 바꾸지 않았다. Jotunn 참조는 없다.

### 기존 미커밋 작업과 제외 범위

시작 때 아래 10개 파일에 기존 드롭 기능 작업이 있었다. 전부 내용 SHA-256을 기록하고 마지막에 동일함을 확인했다. 이번 커밋에 포함하지 않았다. **이번 Debug DLL과 전체 검사에는 이 기존 작업도 포함되어 있으므로, 산출물을 커밋 HEAD만으로 만든 DLL이라고 보아서는 안 된다.**

```text
 M CreatureManager.csproj
 M Plugin.cs
 M README.md
 M tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj
 M tools/GameCompatibilityCheck/Program.cs
 M tools/GameCompatibilityCheck/run_mono.py
?? CreatureLoot.cs
?? docs/loot-scaling.md
?? tools/GameCompatibilityCheck/DropThatLootContracts.cs
?? tools/GameCompatibilityCheck/LootContracts.cs
```

`bin`, `obj`, publicized 컴파일 사본, NuGet·외부 라이브러리 구현, 기존 Release ZIP은 구조 개선 대상에서 제외했다. 병합/접근 경로 확인을 위해 산출물과 라이브러리 metadata는 읽었다. 게임·외부 모드 전체 소스, 모든 전투 modifier의 수치·정책 조합, 모든 리소스 내부/PNG 시각 품질은 전수 검토하지 않았다. Faction은 snapshot 검증/게시와 호출 경계를 확인했으며 모든 관계 조합은 실행하지 않았다. Linux, 실제 Unity 월드, 멀티플레이/크로스플레이, 프레임 시간·할당량 측정도 미검증이다.

## 영역별 구조 판정

| 영역 | 판정과 근거 |
| --- | --- |
| Domain 적용 조정 | **책임 집중이 큼.** 디스크/서버 수신, clone 준비, prefab 적용, 시각 상태·reference가 모인다. 그러나 `TrySetActiveDefinitions` → `ApplyDefinitionsToGameDataCore` → 실패 복원의 transaction과 baseline/clone 상태가 함께 움직인다. 당장 대형 분할하면 private 상태 이동과 탐색 지점이 늘어난다. bool 중복만 제거했다. |
| YAML / Definitions / Baseline / Registry | **대체로 균형.** 문법·DTO·원본 복원·Unity 등록 수명주기가 다르다. `20cf91d`의 projectile random-spawn 기능이 이 파일들을 함께 바꾼 것은 저장/검증/적용/복원의 실제 계약 때문이다. DTO presence 필드나 ObjectDB/ZNetScene의 서로 다른 조회 우선순위를 억지로 합치지 않았다. |
| Level / Karma | **Level은 대체로 균형, Karma는 책임 집중.** 생성 정책·저장 복원·서버 조회·owner 적용을 조정한다. `ccf801d`의 상태 결정/후처리 분리, `1f1b365`의 저장 레벨 복원 수정은 현재 경계가 주는 이익이다. Karma의 지연 사망/스캔/소환 상태를 전면 이동할 근거는 부족하다. |
| Modifier / HUD | **전투·표시 책임 집중과 국소 중복.** 상태를 넓게 공개하는 파일 분리보다 공통 HUD 갱신을 한곳에 배치했다. 일반/보스 컨테이너의 배치 정책과 cache 소유권은 유지했다. |
| Harmony / Swift / Console | **경계가 적절함.** 패치 대상·우선순위·`__state`·Finalizer와 위임 호출을 함께 추적할 수 있다. Swift는 thread-local 이전 상태를 Finalizer에서 복원하고 예외를 그대로 전달한다. 성장/알/번식 및 로컬/원격 콘솔은 조건·권한이 달라 통합하지 않았다. |
| Appearance / TextureRegistry / TextureSync | **분리가 유효함.** 원본/prefab 상태, 살아 있는 visual, Unity texture 소유권, 인증된 전송/캐시의 수명주기가 다르다. texture override 분리 후 synchronized texture를 prune하는 순서와 캐시한 비공개 delegate를 유지했다. |
| localization / Compendium / reference | **대체로 균형.** embedded 번역, 서버 권위·rollback, 표시 fallback은 다른 책임이다. 도감의 텍스트 준비와 TMP 배치 후 아이콘 생성도 시점이 다르다. 남은 Sprite 참조만 제거했다. reference 생성은 frame hot path가 아니므로 추가 cache/계층을 만들지 않았다. |

전체적으로 과도한 파일 분할보다는 일부 manager의 책임 집중과 작은 중복이 문제다. 파일 수·줄 수를 목표로 삼지 않았고 새 런타임 파일·인터페이스·상태 객체·캐시를 추가하지 않았다.

## 구현한 변경과 단계별 검증

### 1. `9d326c3` — AI bool 정책 단일화

- **문제/호출:** `CreatureYaml.ValidateAiDefinition` → `ValidateStringTuple` → `TryParseFlexibleBool`과 `CreatureDomainManager.ApplyBaseAiDefinition`/`ApplyMonsterAiDefinition` → `TryParseBool`이 같은 별칭 규칙을 별도로 구현했다. 두 규칙은 `0644a58`부터 중복이었다.
- **최소 변경:** 기존 YAML 파서를 internal nullable 입력으로 공유하고, Domain wrapper는 파서 호출과 기존 실패 경고만 담당한다. 새 파서 계층은 없다.
- **효과/비용:** 허용 토큰을 변경할 때 정책 수정 지점이 두 곳에서 한 곳으로 줄어든다. 기존 wrapper에서 호출 한 번이 추가되지만 상태나 파일 이동은 없다. 성능 개선을 주장하지 않는다.
- **위험/보존:** `true/false`, `1/0`, `yes/no`, `y/n`, `on/off`, 대소문자·공백, 실패 출력값/경고를 보존한다. 보스·캐릭터의 strict bool과 빈 tuple 토큰 처리 정책은 별도로 유지한다.
- **검증:** 기존 YAML 도구에 AI 별칭 20개, invalid 5개, boss 허용/거부 10개를 추가해 변경 전후 통과. Mono 도구에 최종 DLL의 실제 순수 파서 반환값 20개를 추가해 기준/수정 DLL에서 통과. Debug/배포 해시·호환 검사·diff와 읽기 전용 리뷰 통과 후 커밋했다.
- **범위 한계:** Domain wrapper를 직접 호출한 초기 시험은 기존 DLL에서도 정적 `MaterialPropertyBlock` 생성에 필요한 Unity native 호출 때문에 실패했다. 게임 코드나 초기화 정책을 바꾸지 않고 순수 파서 검사로 범위를 좁혔다. AI 컴포넌트 실제 적용을 실행한 검사는 아니다.

### 2. `1b11c1f` — 일반/보스 HUD의 공통 갱신 배치

- **문제/호출:** `CreatureManagerEnemyHudUpdateHudsPatch.Postfix` → `UpdateEnemyHuds` → `UpdateEnemyHud`/private `UpdateBossHud`. 별·수식어 렌더링이 두 경로에 중복됐고 `0fce808`에서 빈 HUD 조건과 개별 별 표시를 함께 수정한 사례가 있다.
- **최소 변경:** boss 여부에 따른 반대 컨테이너 숨김과 `EnsureLevelContent`/`EnsureBossLevelContent` 선택을 보존하고, `SetActive` → `UpdateStarBadge` → `EnsureIconContainer` → `UpdateModifierIcons`를 한곳에 배치했다. private `UpdateBossHud`만 삭제했다.
- **효과/비용:** 별·수식어 표시 변경 지점이 하나가 되고 간접 호출 하나가 사라진다. 새로운 추상화/파일 이동은 없다. 보스 전용 위치·폭·이름 규칙을 공통화하지 않는다.
- **위험/보존:** 일반/보스/Enforcer 판정의 단락 평가, 반대 컨테이너→vanilla→빈 대상 숨김 순서, modifier-only, `content == null` 반환, mount/level-off/owner 처리와 cache를 유지한다. 삭제한 메서드는 private 일반 helper이며 프로젝트의 Unity 메시지·Harmony·문자열 리플렉션 대상이 아니다. 임의 외부 비공개 reflection의 부재까지 보증하지는 않는다.
- **검증:** 수정 전후 실제 소스의 render tail을 추출하여 경계 호출만 기록하는 임시 .NET fixture에서 **168개** 조합을 비교했다. level -1/0/1/2/3/4/6 × boss 2 × boss-only 2 × modifier 3 × content null 2; 호출 순서와 모든 표시 인자가 일치했다. Debug/배포 해시·원본 호환 검사·diff와 읽기 전용 리뷰 통과 후 커밋했다. Unity 렌더링 자체는 검사하지 않았다.

### 3. `935a402` — 도감 중간 모델의 Sprite 상태 제거

- **문제/호출:** `BuildEntries` → 지역 `List<CompendiumModifierEntry>` → `BuildPageText`는 문자열만 소비한다. `3954def`에서 아이콘 갱신이 `RefreshPageContentIcons`의 직접 조회로 바뀐 뒤 Sprite 필드가 남았다.
- **최소 변경:** private nested readonly struct의 Sprite 필드/생성자 인자만 삭제하고 `TryGetModifierSprite(..., out _)` 호출과 존재 필터·초기화 부작용을 유지했다.
- **효과/비용:** 텍스트 중간 모델이 Unity 자산을 보유하던 불필요한 상태가 사라진다. 실제 `RefreshPageContentIcons` → `AttachBodyIcon` 경로, 새 간접 호출·파일은 없다.
- **위험/검증:** 이 모델은 Unity component/직렬화/public API/프로젝트 내 reflection 대상이 아니다. 단순한 참조 검색만으로 삭제하지 않고 소비 흐름과 Git 변경을 확인했다. Debug/배포 해시·최종 client/server 호환 검사·diff와 읽기 전용 리뷰 후 커밋했다. 구현을 복제하는 별도 테스트는 추가하지 않았다.

## 유지한 계약과 보류한 개선

- **권한/중복 처리:** Level의 서버 Karma 조회는 read-only 요청인 반면 응답 적용은 request ID·server sender·현재 owner를 검증한다. Karma 사망 RPC는 수신 당시 owner/위치/중복/한도를 확인한 뒤 지연 처리에서 epoch·ZDOMan·사망·owner를 다시 확인한다. 반복 검사지만 시점과 역할이 다르므로 제거하지 않았다.
- **보상:** `DropStoredEnforcerLootCore`는 실제 `DropItems` 전에 처리 표식을 기록한다. 부분 생성 실패 후 재시도로 복제되는 것을 막는 기존 at-most-once 정책이다. 표식을 생성 뒤로 옮기거나 재시도를 추가하지 않았다.
- **RPC 수명:** 원본 `ZRoutedRpc.Register`는 Dictionary.Add이며 unregister API가 없다. `RegisteredRoutedRpc`를 세션 reset 때 비우면 동일 instance 중복 등록 위험이 있어 유지했다. 콘솔 관리자와 네트워크 owner도 같은 권한으로 합치지 않았다.
- **Unity/이벤트:** Domain/localization의 ValueChanged·SourceOfTruthChanged·ClientManifestReady 정리, watcher Dispose, Appearance의 OnDisable 관련 Forget와 전체 Reset, texture 소유별 Destroy 정책을 유지했다. resource texture는 게임 소유이므로 모드 소유 texture와 같은 방식으로 파괴하지 않는다.
- **Enforcer 스캔 reset 공동 배치:** `ResetRuntimeState`와 `ResetEnforcerBootstrapScan`의 중복은 후보지만 이번에는 이익이 작아 보류했다. `CompleteEnforcerBootstrapScan`의 pending 보존 정책은 다르다. 향후 이 상태 자체를 바꿀 때 기존 reset 함수 재사용부터 검토할 수 있다.
- **단일 enum reflection helper:** `TrySetEnumField`의 현재 한 호출은 public `BaseAI.m_pathAgentType`로 직접 접근할 수 있다. 다만 현재 `GetType().GetField`는 외부 파생형의 동명 field를 선택할 수 있으므로 계약 보존 근거가 약하다. 변경 이익도 reload 경로의 작은 단순화라 유지했다.
- **과도한 분할 보류:** transaction/rollback, prefab baseline, 저장 복원, HUD cache를 여러 새 클래스로 옮기지 않았다. 테스트 없이 private 상태를 넓게 공개하는 비용을 늘릴 이유가 없다.

### 반복 실행 비용

HUD modifier 값은 0.25초 cache, hover/sneak 판정은 frame cache, UI 객체 상태는 ConditionalWeakTable을 사용한다. 캐릭터·월드 정리 시 해당 상태를 비운다. icon slot/layout rebuild는 표시 변경에 한정되어 매 프레임 전체 UI를 다시 만들지 않는다. `Physics.RaycastAll`의 배열/정렬 비용은 남지만 non-alloc 버퍼로 바꾸려면 잘림·동률·정리 정책 검증이 필요하다. 실제 측정이 없어 이번에 바꾸지 않았다.

Level/Karma의 `ToArray` 기반 유지보수는 Plugin.Update의 **0.5초 간격** 뒤 실행된다. 매 프레임 전체 검색으로 분류하지 않았다. Domain은 pending 없으면 반환하고 TextureSync는 chunk 상한/backpressure를 가진다. Appearance 비공개 접근자와 외부 위치 조회 reflection은 캐시되어 있다. 저항 텍스트는 같은 언어의 hot reload도 반영하므로 언어 이름만으로 갱신을 생략하지 않았다. **이번 변경의 효과는 유지보수 비용 감소이며 FPS·할당 개선을 측정한 결과가 아니다.**

## 검증 결과와 남은 실행 확인

| 구분 | 수행 결과 |
| --- | --- |
| 기준 빌드 | 수정 전 `dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true`와 별도 GameCompatibilityCheck 빌드 성공, 경고/오류 0. 기존 실패 없음. |
| 단계별 빌드 | 세 코드 변경마다 같은 Debug/배포 명령 성공, 경고/오류 0, 복사 후 SHA-256 일치. 마지막 DLL 해시 `28CB18CA80E36E8F232A73833C9226AA7BFE0049957FAF6055C3FA6BB6911A06`. |
| YAML 자동 검사 | 기준/최종 DTO round-trip, strict parser, sample/동기화/locale 기존 계약과 새 AI bool 검사 통과. |
| 원본 Mono 격리 검사 | client/server 각각 기준 7,462 / 최종 **7,456** game/Unity IL 참조 확인. 양쪽 모두 **98 Harmony 대상·6 transpiler 적용**, 원본 로드 경로·최종 접근 특성·기존 managed 계약·loot/Drop That 조합 검사 실패 0. native detour 설치 없음. |
| 변경 전후 계약 비교 | Cecil로 exported API signature, Harmony/BepIn attribute blob, embedded resource 해시 **203개 기록**을 비교해 동일. 설정 키·저장/연동 형식과 권한 경로는 변경하지 않았으며 기존 미커밋 10개 파일도 SHA-256 동일. |
| HUD 제어 흐름 | 소스 추출 fixture 168개 호출 trace 동일. Unity 경계는 stub이므로 실제 TMP/UI 배치 검증과 구분한다. |
| 실제 게임 실행 | **수행하지 않음.** 월드/호스트/서버 접속, 실제 AI 적용·UI·아이템 생성·owner 이전·저장/재접속·Steam/PlayFab 수송 성공을 주장하지 않는다. |

관리형 검사 재현의 기본 명령:

```powershell
dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true
dotnet run --project tools/YamlSyncContractCheck/YamlSyncContractCheck.csproj -c Debug --no-build
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
& 'C:\Users\blizz\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' `
  tools/GameCompatibilityCheck/run_mono.py `
  'C:\Program Files (x86)\Steam\steamapps\common\Valheim' `
  bin/Debug/CreatureManager.dll `
  'C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\core' `
  'C:\Users\blizz\AppData\Roaming\com.kesomannen.gale\valheim\profiles\adminclient\BepInEx\plugins\ASharpPen-Drop_That\Valheim.DropThat.dll'
```

서버 원본 검사는 game 인자를 `C:\Users\blizz\.codex\references\valheim\snapshots\dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored\original`로 바꿨다. 이 명령의 loot 관련 추가 기능은 기존 미커밋 검사 작업에 속한다. 기준 DLL·검사 로그·일회성 `check_hud.py`/`compare-contracts.ps1`·보존 해시는 로컬 임시 폴더 `C:\Users\blizz\AppData\Local\Temp\CreatureManager-structure-baseline-20260924T080655Z`에 있다. HUD 스크립트의 기준 source는 이 실행 당시 HEAD인 `9d326c3`였다.

다음 실제 실행 확인은 변경 단위별로 분리하면 된다.

1. AI YAML의 별칭/공백·잘못된 값 reload, 새 개체의 base/monster AI 값, 기존 개체 보존과 경고 확인.
2. 일반·보스·Enforcer HUD의 별 0/1/2/3+, modifier-only/빈 HUD, mount·level 시스템 off/on, HUD 재생성과 월드 재입장 확인.
3. 도감의 항목/정렬/번역·아이콘 유무, 페이지 반복 열기/닫기와 TMP 배치 확인.
4. 기존 고위험 경계는 호스트·데디케이트에서 owner 이전, 사망 RPC 중복/지연, 설정 권한, Enforcer 보상·저장·접속 해제/재접속을 확인해야 한다. 이번 구조 변경이 해당 정책을 수정하지 않았어도 격리 검사만으로 전체 실행이 보장되지는 않는다.

각 코드 커밋은 앞 단계에 의존하는 새 추상화가 없어 개별적으로 검토·되돌릴 수 있다. 필요하면 역순의 정상 revert 후 Debug/해당 검사를 실행한다. 문서 커밋은 실행 코드를 바꾸지 않는다.

## 별도로 남긴 결함/정책 후보

이번 범위에서 새로 확정한 게임 런타임 결함은 없다. 다음은 구조 개선과 섞지 않았다.

- 기존 Drop That 3.1.6 fixture에서 확인된 AmountLimit 처리: global DropLimit 조건이 발동하지 않는 사례에서 개별 AmountLimit이 적용되지 않는다. 외부 라이브러리 기존 동작이며 CreatureManager refactor로 수정하지 않았다.
- AI `movement.pathAgentType`는 YAML 단계에서 비어 있지 않은 이름만 검사하고 실제 적용에서 invalid enum을 경고한다. 잘못된 enum 때문에 bundle 전체를 거부하도록 바꾸는 것은 입력 처리 정책 변경이다.
- Enforcer 보상 일부 생성 후 예외는 기존 중복 방지 정책상 일부 소실 여지가 있다. 재시도/transaction 도입은 별도 아이템 보존 정책과 실제 실패 복구 검증이 필요한 작업이다.

최종 작업 트리에는 시작 때의 기존 10개 변경만 남겼고, 이번 코드/검사/검토 문서는 커밋했다. 기존 사용자 작업을 이동·폐기·일괄 커밋하지 않았다.
