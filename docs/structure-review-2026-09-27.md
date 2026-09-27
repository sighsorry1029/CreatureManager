# CreatureManager 구조 검토 및 적용 — 2026-09-27

## 기준과 범위

- 기준 브랜치 `main`, 시작 HEAD `42eb3909965c6f952bf1eadcf72c79bc2ef0784a`, 모드 버전 `1.1.17`.
- 시작 시 수정 파일 12개와 미추적 파일 6개가 있었다. 보스 감지, modifier 제한, 반사 상한, 던전 Enforcer 지연, DeepNorth, 주변 spawn 차단 관련 선행 작업이다. 기준 빌드와 최종 빌드는 **이 작업 트리 전체**를 사용한다. 이번 커밋에 그 변경을 포함하지 않았다.
- 선행 파일: `CreatureDomainManager.cs`, `CreatureHarmonyPatches.cs`, `CreatureKarmaManager.cs`, `CreatureLevelManager.cs`, `CreatureManager.csproj`, `CreatureModifierManager.cs`, `Plugin.cs`, `README.md`, `translations/English.yml`, `translations/Korean.yml`, `tools/GameCompatibilityCheck/{ManagedContracts,Program}.cs`, `CreatureSpawnBlocker.cs`, `docs/boss-blocker-discovery-2026-09-27.md`, `tools/GameCompatibilityCheck/{BossBlocker,EnforcerDelay,ModifierLimit,SpawnBlocker}Contracts.cs`.
- 작업 전 파일 사본과 SHA-256을 임시 디렉터리에 보관하고 종료 전 비교했다. 18개 모두 기존 바이트를 보존했다. `Program.cs`는 이번 검사 호출 한 줄만 추가했고 그 한 줄만 선택적으로 커밋했다. 원래 네 검사 호출은 미커밋 상태로 유지했다.
- 전역 `C:\Users\blizz\.codex\AGENTS.md`와 사용자가 제공한 지침을 확인했다. 프로젝트 및 상위 디렉터리에 별도 `AGENTS.md`는 없었다. 편집·빌드·커밋은 한 세션에서 수행했고 병렬 검토는 읽기 전용이었다.
- `docs/structure-review-2026-09-24.md`, README, 최근 Git 이력은 단서로 사용했다. 과거 문서의 게임 1.0.15 및 BepInEx 버전 설명을 현재 값으로 가정하지 않았다.

### 실제 빌드와 진입점

| 항목 | 현재 확인 결과 |
| --- | --- |
| 솔루션 | `CreatureManager.sln`: net48 플러그인과 net8 `YamlSyncContractCheck` |
| 플러그인 진입점 | `Plugin.cs`의 `CreatureManagerPlugin : BaseUnityPlugin`, BepInPlugin GUID `sighsorry.CreatureManager`; 프리로더 패처 아님 |
| 초기화/종료 | `Awake` 설정·이벤트·Harmony·선택적 연동·domain/watchers 등록 → `Update` 갱신 → `OnDestroy/CleanupRuntime` 구독 해제·watchers/state 정리·unpatch. 부분 초기화 실패 정리 유지 |
| 게임 참조 | 실제 GamePath는 설치된 Windows x64 Valheim 1.0.16 클라이언트 build 25527674. 원본 서버 검사 대상은 1.0.16 build 25527701 |
| 컴파일 접근 | 기존 `build/GameReferences.targets`의 AssemblyPublicizer 0.4.2가 `obj` 사본만 공개화. 분석 및 Mono 검사는 원본 DLL 사용. 기존 참조 체계를 일괄 변경하지 않음 |
| 최종 병합 | `ILRepack.targets/ILRepacker`: 플러그인 + 출력 `Libs/ServerSync.dll` + YamlDotNet 16.3.0 → Internalize된 `bin/Debug/CreatureManager.dll` |
| ServerSync | 직접 고정 Libs 참조. SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`; 전역 `valheim-1.0.7-r1`과 일치. 최종 병합 입력도 동일 |
| 로컬 설치 | `DeployGamePlugin`이 ILRepacker 성공 후 최종 DLL만 Valheim `BepInEx/plugins`로 복사 |
| 패키지 | 실제 원본 `Thunderstore/manifest.json`의 BepInExPack은 이미 5.4.2351. 이번에 버전·manifest·의존성·Release 패키지는 변경하지 않음 |
| 검사 도구 | 솔루션의 YAML 검사 외 `tools/GameCompatibilityCheck`는 별도 net48 프로젝트. `run_mono.py`로 원본 게임 Mono에서 실행 |

전역 `C:\Users\blizz\.codex\references\valheim\INDEX.md`에서 해당 역할·버전을 확인하고 보존된 원본/추출 자료만 재사용했다. 클라이언트 원본 경로는 `snapshots/client-b25527674-windows-x64-20260925T211211Z/original`, 서버는 `snapshots/dedicated-server-b25527701-windows-x64-20260925T211211Z-depot-restored/original`이다. 원본 `Version.cs`는 1.0.16이고 설치 `assembly_valheim.dll` SHA-256 `96CFC004F7F4A6F30D070BEF39EAFD79C466A137121C4665A2F19FB9C15C6127`은 클라이언트 원본과 일치한다. 이는 이번 실제 참조/검사 버전이며, 모든 이전 버전 지원이나 실제 1.0.16 플레이 성공을 선언하지 않는다.

설정 검토는 cfg 바인딩/live 변경, creatures/attacks/projectiles/AI/levels/factions/karma YAML, 파일 감시와 ServerSync 전달, 텍스처 동기화 및 복원 경계를 포함했다. 리소스는 임베드 영어·한국어, sample creatures/attacks YAML, PNG의 등록·소유·해제 경로를 확인했다. PNG 자체를 시각적으로 전수 검사하거나 번역 전체를 감수하지 않았다.

선택적 연동은 FeedLikeGrandma의 reflection/지연 탐지, EWD 지역·바이옴 질의, ServerDevcommands 패치 순서·관리자 정책, DropNSpawn/DropThat의 loot 양보 및 spawn 패치 조합을 확인했다. Jotunn 의존성은 없다. CLLC/StarLevelSystem/MonsterDB/MonsterModifiers 비호환 선언은 유지했다.

제외 범위는 `bin`, `obj`, 공개화 중간 DLL, NuGet·vendor 라이브러리 내부 구현, 배포 ZIP, 생성된 reference 문서의 전수 정비다. 외부 모드 전체, 게임 전체, 모든 modifier 전투 조합, 모든 prefab/resource도 전수 감사하지 않았다. Linux·크로스플레이·실제 Unity/네트워크 및 프로파일링은 미검증이다.

## 영역별 판단

전체적으로 **일부 manager에 책임이 집중되어 있지만, 과도한 파일 분리가 주된 문제는 아니다.** 파일 수보다 상태 소유와 변경 경로를 기준으로 아래와 같이 판단했다.

| 영역/호출 관계 | 판단과 유지 이유 |
| --- | --- |
| `Plugin` → `DomainManager` → YAML/Registry/Baseline | Domain에 파일·동기화·적용·복원이 집중됨. 그러나 검증 후 발행, 실패 복구, prefab 원본 복원이 같은 적용 단위에 속한다. 일괄 분리하면 transaction 상태를 여러 곳에 전달해야 하므로 유지 |
| `KarmaManager` ↔ modifier/death RPC/Enforcer | 집중도가 높음. 서버의 사망 증거, 지연 예약, 보스 감지, 카르마 소비가 연동된다. pre/post delay 보호 코드는 시점과 증거가 달라 삭제 대상 아님. 선행 미커밋 기능과도 겹쳐 구조 변경 보류 |
| `ModifierManager` → damage/passive/HUD | 전투·네트워크·표시 책임 집중. `1b11c1f`가 이미 creature/boss HUD의 실제 공통 후반부를 통합했음. 서로 다른 효과·보스·Enforcer 정책을 단일 일반화 계층으로 묶는 이익은 입증되지 않음 |
| `LevelManager`, `Loot` | 레벨 정책·저장 상태·owner 적용 및 loot 산식/외부 모드 양보 경계는 유효. 단순한 한 단계 wrapper 인라인은 이익이 작고 dirty 영역이라 유지 |
| Definitions/Yaml/Baseline/Registry | 대체로 균형. schema, 필드 존재/검증, 원본 capture/restore, Unity 등록의 책임·수명이 다름. `20cf91d`의 projectile random spawn 변경처럼 실제 외부 계약 확장이 여러 파일에 전달되는 것은 단순 중복과 구별 |
| TextureSync/TextureRegistry/Appearance | protocol·바이트, Texture2D 소유권, 살아 있는 visual 적용을 분리한 것은 적절. Sync 내부의 중복 membership만 제거 |
| FactionManager/공개 Faction API | 파일 경계 적절. 다만 검증 snapshot을 다시 네 static 필드로 분해하는 중복 상태 보관은 개선 |
| Harmony/Swift/Console | target·overload·순서, `__state`/finalizer와 thread-local 복원, 관리자 정책의 경계 유지. instance owner와 사용자 권한 검증을 합치지 않음 |
| Localization/Compendium/Reference/Icon | 대체로 균형. 임베드 fallback, 서버가 소유한 번역의 복원, UI 생성 수명이 다름. `935a402`에서 제거한 Sprite 보관을 재도입하지 않음 |
| SpawnBlocker | 주변 sector/ZDO 조회와 owner의 spawn 실행, boss/item/raid 제외 경계를 확인. 선행 미커밋 구현으로 보존하고 이번 구조 커밋에서 제외 |

구체적으로 Registry의 아이템 ObjectDB 우선 탐색과 projectile ZNetScene 우선 탐색은 다른 정책이다. texture의 리소스 이름과 wire PNG 이름 검사도 적용 조건이 다르다. `Appearance.Forget`의 마지막 hair-color 제거는 `ClearAppliedVisual → ApplyHairVisual → ApplyConfiguredHairMaterialColor`가 상태를 다시 넣을 수 있어 삭제하지 않았다. ZRoutedRpc 등록 cache도 실제 등록이 dictionary Add이고 unregister가 없어 일반 reset과 묶어 지우지 않았다.

## 구현한 개선과 비용 비교

### 1. 텍스처 전송 membership의 중복 상태 제거

- 문제: `RPC_TextureRequest → OutgoingChunks → Update/CompleteQueuedChunkLocked`에서 `QueuedTransfers`와 `RemainingTransferChunks`가 같은 키를 등록·삭제했다. 두 컬렉션은 같은 Ordinal comparer이며 부분 완료, 재큐잉, peer 제거, snapshot 교체/복원까지 같은 Sync lock 안에서 함께 유지됐다.
- 최소 변경: `QueuedTransfers`를 삭제하고 중복 요청은 `RemainingTransferChunks.ContainsKey`로 검사. peer 정리도 그 키를 열거한다. 청크 큐 자체와 완료 수는 계속 별개 책임이다.
- 기대 효과: 상태 불일치 가능 지점과 등록/완료/초기화 시 동시 수정해야 할 지점을 줄인다. HashSet 저장·수정 비용 제거는 예상 효과이며 프레임 성능은 측정하지 않았다.
- 추가 비용: 새 런타임 파일·인터페이스·호출 계층·cache 없음. 기존 사전 탐색으로 대체하므로 탐색 이동도 없음. 네트워크 경계의 회귀를 확인할 검사 코드는 추가했다.
- 보존 계약: RPC 이름/패키지/인증/전송 순서/중복 요청 지연/최근 serve 이력/lock/peer 필터/rollback/reset을 유지. 압력 때문에 청크를 재큐잉할 때 count가 남는 것도 동일.
- 위험: 조기 완료 또는 peer 제거가 다른 요청을 지우는 회귀. 기존/수정 DLL 모두 실제 request body(인증 조회만 fixture로 대체), 부분·최종·실패·stale 완료, UID 접두사 분리, 재요청, snapshot 교체/복원 검사 통과. 실제 소켓 재접속 검증은 남음.
- 이력: `ccf801d`에 전송 구현 도입, `1eff211`의 준비/실패 처리와 `3954def`의 serve-history 만료 최적화처럼 같은 상태 수명에 대한 변경이 이어졌다. 별도 전송 계층보다 중복 저장을 줄이는 편이 작고 명확하다.
- 커밋 `e084575`: `CreatureTextureSync.cs`, 새 `StateOwnershipContracts.cs`, Program의 검사 호출 한 줄.

### 2. FactionSnapshot을 그대로 활성 상태로 발행

- 문제: `TryBuildSnapshot → Load`가 네 사전으로 만든 `FactionSnapshot`을 다시 네 static 필드에 풀어 저장했다. `TryGetFaction`, `TryGetRegisteredFaction`, `SetupBaseAi`, `TryIsEnemy`는 그 네 필드를 읽었다.
- 실제 변경 사례: `8fb6084`의 runtime 이름 map 추가와 `ccf801d`의 별도 aggravated 상태 제거는 선언·발행·snapshot 구성을 함께 바꾸었다. 상태 정의 변경 시 중복 보관 지점이 실제로 늘어나는 구조였다.
- 최소 변경: 기존 snapshot 타입을 `ActiveSnapshot` 하나로 보관하고 조회만 해당 속성으로 연결. 새로운 타입/파일/계층은 추가하지 않는다.
- 기대 효과: 성공한 설정 발행이 한 대입으로 표현되어 함께 바뀌어야 할 사전 묶음이 분명해진다. 기존 lock도 이미 원자성을 보장했으므로 새로운 동시성 문제를 해결했다고 주장하지 않는다.
- 비용: 조회식에 `ActiveSnapshot` 접근 한 단계가 붙고 snapshot 객체를 유지한다. 그러나 네 backing field와 네 발행 대입이 없어지며 기존 타입을 재사용하므로 파일 이동·설계 탐색 비용은 늘지 않는다. 성능 향상 목적의 변경은 아니다.
- 보존 계약: 초기 빈 map 유지(즉시 default load 아님), 기존 8개 lock, 실패 시 이전 설정 유지, 성공 후 `RefreshLiveBaseAis`, 공개 등록 목록과 내부 runtime 이름 조회 범위 구분, canonical 이름·고정 ID·ZDO 키/저장·친선 정책을 유지.
- 위험: 초기화 시점 변화, 거부된 설정 일부 적용, runtime-only faction을 공개 API에 노출하는 회귀. 실제 `Load`와 공개 조회로 변경 전후 검증했다. 원본 `BaseAI.GetAllInstances`는 managed list를 반환하는 public 메서드이며 fixture에서는 비어 있다. 살아 있는 AI 친선 판정과 ZDO 적용까지 실행한 검사는 아니다.
- 커밋 `cdad217`: `CreatureFactionManager.cs`와 기존 StateOwnershipContracts에 faction 발행 검사 추가.

검사 파일은 새 런타임 추상화가 아니다. 공개 결과와 실제 상태 전이를 변경 전 DLL에도 적용해 동작 보존 근거를 만들었다. fixture 때문에 전체 production 코드를 복제하지 않았고, texture request의 인증 접속 조회만 치환했다. faction Load는 치환 없이 실행했다.

## 주기적 실행과 수명주기

- `Plugin.Update`의 domain/server-localization pending 처리는 매 프레임, 유지보수는 0.5초 간격이다. modifier HUD에는 0.25초 캐시, inactive 재확인에는 1초 간격, hover에는 프레임 캐시가 이미 있다. 표시 내용이 달라질 때 UI를 갱신하는 경계를 유지했다.
- owner/passive state/다음 실행 시각을 확인하는 character FixedUpdate 경로와 TextureSync의 tick당 최대 2청크/역압력 경계를 유지했다. 이번에 새 cache나 무효화 책임을 추가하지 않았다.
- 원본 `MonoUpdaters.Update → VisEquipment.CustomUpdate → UpdateEquipmentVisuals` 및 postfix 경로에서 configured ragdoll 색 적용의 `materials`, `GetComponentsInChildren<Renderer>` 비용이 남아 있다. 확인된 검색/배열 비용과 실제 병목은 다르다. 모델·장비 변경, 타 모드 교체, YAML reload, destroy를 모두 반영하는 cache를 설계하기 전에 실제 ragdoll 밀도에서 프로파일링하는 편이 낫다.
- hover의 RaycastAll 배열/정렬도 남는다. NonAlloc로 바꾸면 hit 수 제한과 순서 처리 문제가 생길 수 있어 계측 없이 변경하지 않았다.
- TextureRegistry의 게임 소유 resource texture와 모드 생성 texture는 해제 책임이 다르다. 교체된 synced texture가 ragdoll에 남을 수 있어 unload까지 보존하는 수명도 유지했다. Compendium 생성은 page/display 경로이며 매 프레임 전체 재생성으로 확인되지 않았다.

## 분리해서 남긴 후보와 위험

이번 두 변경은 구조 개선이며 확인된 게임 버전 오류나 밸런스 수정으로 묶지 않았다. 새로 확정한 런타임 결함은 없다.

- `RemovePeerTransfersLocked`의 큐 필터는 UID+RPC, count/history 제거는 UID 접두사다. 기존 정책이며 같은 UID의 이전/새 접속이 겹치는 실제 호출 순서가 문제를 만들 수 있는지 별도 재현이 필요하다. 이번에 peer 정리 정책을 바꾸지 않았다.
- Enforcer reward의 일부 생성 후 예외와 once-only 방어는 재시도/중복 방지 정책을 같이 검토해야 한다. 무조건 재시도는 아이템 복제를 유발할 수 있으므로 구조 변경에 넣지 않았다. 실제 소실/복제 발생을 이번에 재현한 것은 아니다.
- AI `movement.pathAgentType`의 더 엄격한 선행 검증은 전체 YAML bundle 거부 범위를 바꾸므로 별도 기능 판단 대상이다.
- 제공된 Drop That 3.1.6.0의 관리 코드 fixture에서는 global DropLimit가 발동하지 않으면 해당 경로의 개별 AmountLimit도 적용되지 않았다. 외부 라이브러리의 기존 동작을 확인한 것이며 CreatureManager 구조 변경과 구분해 남겼다.
- 카르마가 이미 붙은 쫄 레벨, Blamer 고유 동작, 권한 완화·동시 접근, FrozenKing 예외, modifier 최대치/지연/보상 정책은 변경하지 않았다.
- 직접 C# 참조 부재만으로 삭제를 결정하지 않았다. Harmony target/provider, Unity 메시지, FeedLikeGrandma 및 enum 확장 reflection, 공개 `CreatureManagerFactionApi` 호출 가능성은 유지했다. 알려지지 않은 외부 모드가 private 필드명을 reflection으로 참조하는지까지 전수 확인할 수는 없다.

## 검증 결과와 남은 실행 확인

| 구분 | 수행 결과와 한계 |
| --- | --- |
| 기준 빌드 | 수정 전 `dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true` 성공, 경고/오류 0; 기준 DLL 사본 보관 |
| 단계별 빌드 | texture와 faction 각 변경 후 같은 명령 성공, 각각 최종 병합 및 plugins DLL 갱신 성공; 경고/오류 0 |
| 검사 도구 빌드 | `dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug` 성공, 경고/오류 0 |
| 변경 전후 계약 | 새 texture/faction 검사는 기준 DLL과 최종 DLL 모두 통과. 최종 client/server 각각 7,635 game/Unity IL 참조, 104 Harmony 대상, 7 transpiler 적용 검사 및 기존 관리 코드 계약 검사 실패 0 |
| YAML | `dotnet run --project tools/YamlSyncContractCheck/YamlSyncContractCheck.csproj -c Debug --no-build` 성공 |
| 선택적 조합 | 현재 `DropNSpawn/bin/Debug/DropNSpawn.dll`의 group transpiler 조합 검사 양 역할에서 통과. DropNSpawn 실전 동작 검증은 아님 |
| DropThat 조합 | 과거 adminclient 경로는 DLL 부재로 실패했으나 사용자가 제공한 planbuild 경로의 **3.1.6.0**으로 재실행해 client/server 모두 실패 0. combined transpiler, entry reference/조건, 수량 제한 후처리, stack/ragdoll patch site 검사 통과. 실제 드롭 생성·게임 실행은 미검증 |
| 원본 접근 | 실행 시 원본 Managed 경로 확인, 최종 DLL의 기존 access attributes 확인, 일부 관리 코드 private 접근/델리게이트 실행. 모든 Unity native 경로의 접근 성공을 뜻하지 않음 |
| diff/기존 작업 | 단계별 diff 및 `git diff --check`, 읽기 전용 독립 리뷰 통과. 초기 18개 파일의 원래 내용 보존 확인 |
| 실제 실행 | 게임 프로세스, Unity world, 호스트/원격 클라이언트/데디케이트 세션, 네이티브 Harmony detour 설치, 크로스플레이, 저장/재접속, 성능 계측은 수행하지 않음 |

최종 DLL: `bin/Debug/CreatureManager.dll`. 로컬 복사 대상: `C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CreatureManager.dll`. 양쪽 SHA-256은 `7634E32F96989E78CD52318731B93F3F7544E92EE9D3048098FBFBBE5395BDE0`으로 일치했다. DLL에는 보존된 선행 미커밋 기능도 들어 있으므로 커밋만 checkout한 산출물과 동일하다고 주장하지 않는다.

DropThat 검사 입력은 `C:\Users\blizz\AppData\Roaming\com.kesomannen.gale\valheim\profiles\planbuild\BepInEx\plugins\ASharpPen-Drop_That\Valheim.DropThat.dll`, SHA-256 `F20E41B20D8B8C065F803A779A1759752604E26084FFB345CC0360B220ABFEB7`이다. 이 DLL은 검사 입력으로만 사용했으며 빌드 의존성·모드 패키지에는 추가하지 않았다.

재현 로그는 `%TEMP%/cm-structure-{baseline-client,baseline-characterized,texture-before,texture-after,faction-before,final-client-available,final-server-available,final-client-dropthat,final-server-dropthat}.log`에 있다. 원본 게임 스냅샷·추출 자료·외부 모드 DLL은 수정하지 않았다. 이 로컬 로그는 영구 저장소 자료가 아니며 핵심 결과와 한계를 이 문서에 기록했다.

후속 실전 확인은 (1) 원격 클라이언트의 다중 청크 텍스처 수신 중 접속 해제/재접속·서버 texture reload·클라이언트 시각 복원, (2) faction live reload 성공/실패 후 살아 있는 AI의 친선·aggravated/alerted 상태·저장/재접속 canonical 이름 유지, (3) 같은 확인을 호스트 및 데디케이트에서 수행하는 순서가 적절하다. 특히 실제 아이템 획득·보상 경로는 이번 변경 대상이 아니고 소실/복제 방지를 실전 검증한 것으로 보고하지 않는다.

## 안전한 적용 순서와 종료 상태

1. 기준 빌드와 원래 dirty 내용 보관.
2. texture 계약을 기존 DLL에 실행 → 중복 상태 제거 → Debug/Mono/diff/독립 리뷰 → `e084575` 커밋.
3. faction 계약을 기존 DLL에 실행 → snapshot 발행 변경 → Debug/원본 client·server Mono/YAML/diff/독립 리뷰 → `cdad217` 커밋.
4. 이 문서에 근거와 미검증 범위를 기록하고 문서만 별도 커밋.

생산 코드 두 변경은 다른 도메인 상태에 의존하지 않는다. 필요 시 각 코드 변경과 해당 검사 부분을 단독으로 되돌릴 수 있다. 두 번째 검사 커밋은 첫 번째에서 도입한 검사 파일을 확장하므로 전체 커밋 revert는 역순이 간단하다. 이번 작업에서 revert 자체를 수행하지 않았다.

종료 시 `main`을 유지하고 staging은 비웠다. 기존 수정 12개/미추적 6개만 남으며 이번 구조 변경은 모두 커밋했다. push, 버전 변경, Release 빌드/ZIP, 업로더 점검·게시를 수행하지 않았다.
