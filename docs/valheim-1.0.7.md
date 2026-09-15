# CreatureManager — Valheim 1.0.7 대응

2026-09-10. 기준 소스는 `3954def` (CreatureManager 1.1.12), 대응 릴리스는 CreatureManager **1.1.13**이다. 목표는 Windows x64 클라이언트 Steam 빌드 **25185596**, 데디케이트 서버 **25185644**, 게임 **1.0.7**, Unity **6000.0.75**다. 0.221.x 실행 호환 계층은 추가하지 않는다.

## 확인한 원인과 변경

| 영역 | 확인한 근거 | 최소 변경과 보존한 계약 |
| --- | --- | --- |
| 초기화 / ServerSync | 제공 로그의 `CreatureManager 1.1.12` 로드 직후 `CreatureServerLocalization.Initialize` → `CustomSyncedValue`에서 `ZRoutedRpc.Everybody` 예외. 게임 원본에서 `static long`이 `const long 0`으로 변경됨. | 검토된 공통 기준본 `valheim-1.0.7-r1`을 내장. 원본 참조 재컴파일로 구 필드 읽기를 제거하고 공개 `ZNet.IsAdmin`을 사용하며, 초기 설정 동기화 중 새 player-history/admin-list 메시지의 순서를 보존. RPC 이름, 전송 형식, 관리자 정책, 버전 검사와 flush 순서는 유지. |
| 실제 컴파일 참조 | 기존 프로젝트가 설치 폴더의 오래된 `publicized_assemblies`를 참조함. 원본만으로 컴파일하면 기존 비공개 타입/멤버 접근에서 실패함. | 원본 `assembly_valheim`, `assembly_guiutils`에서 `obj` 아래 컴파일 전용 사본을 생성. 기존 접근 코드를 일괄 리플렉션으로 바꾸지 않음. `assembly_utils`는 원본을 직접 참조. BepInEx publicizer 0.4.2의 MSBuild task와 생성된 접근 특성을 사용하며, 최종 병합 DLL에서도 특성을 검사. 게임 원본은 수정하거나 패키징하지 않음. |
| 콘솔 | `ConsoleCommand` 두 생성자에 `hideBehindDevCommands`, `ConsoleEventArgs` 생성자에 명령 객체가 추가됨. | 신규 원본으로 재컴파일하고 원격 `cm:spawn`/`cm:karma` 인자에 해당 등록 명령을 전달. cheat/network/server/admin/remote 플래그와 서버의 선행 인증 경로를 유지. |
| 외형 / 원복 / 출력 | `VisEquipment.m_hairItem`, `m_beardItem`, `SetHairItem`이 문자열에서 `int` 해시로 변경됨. `Humanoid` 필드는 여전히 문자열. | YAML 이름은 유지하고 VisEquipment 경계에서만 해시로 변환. 빈 이름은 0. baseline은 해시를 정확히 보관/복원. 원본의 private 필드는 캐시한 Harmony FieldRef로 접근. 프리팹·래그돌의 로컬 필드 변경과 소유자 ZDO 변경을 구분. `cm:full` 출력은 이름을 역조회하며, 찾지 못하면 잘못된 숫자/빈 이름을 저장하지 않고 기존 출력 파일을 보존한 채 오류를 보고. |
| ZDO 상태 캡처 | `ZDOExtraData.GetFloats/GetVec3s/...` 7개 열거 API가 제거되고 `GetData`로 변경됨. | 캡처와 정리에서 한 번씩 `GetData` 호출. 스냅샷 버전 2, 7개 타입의 순서, 허용 키, 정렬, 크기/중복/후행 데이터 검사, 복원 소유권 검사는 유지. 다른 모드의 키는 포함/삭제하지 않음. |
| Karma 지역 탐색 | 게임 zone은 `Vector2s`, `FindSectorObjects`는 `SimulationDistance`를 사용함. | 게임 API 경계에서 좌표를 변환하고 내부 좌표/저장 키는 기존 `Vector2i` 유지. `SimulationDistance(1, 0, classic: true)`로 기존 정사각형 3×3 탐색을 유지. 새 sector 묶음 조회 뒤의 실제 위치·거리 필터도 유지. |
| 영혼 피해 / 보상 | `Character.AddSpiritDamage(float, short)`와 `CharacterDrop.DropItems(..., bool cheated)`로 변경됨. | 원본 private 영혼 피해 메서드의 캐시 delegate에 현재 hit의 variant 전달. 보상은 현재 CharacterDrop의 cheated 표시를 전달. 사망 보상 생성 **전** 처리 완료 표식을 기록하는 기존 중복 방지 순서는 유지. |
| 불필요해진 우회 | 1.0.7 `Humanoid.OnStopMoving`은 소유권·유효성·`m_currentAttack != null`을 검사함. | 0.221.x의 잘못된 null 분기를 보정하던 Harmony transpiler 1개를 삭제. 일반 기능 개선과 달리 게임에서 고친 버그에 대한 우회 제거다. 다른 보호 코드는 제거하지 않음. |
| 패키지 의존성 | manifest의 BepInExPack이 5.4.2333이었음. | 1.1.13 manifest와 Release ZIP에서 5.4.2350으로 정정. 설치된 BepInEx DLL은 교체하지 않음. |

새 빌드 target은 오래된 수동 publicized 파일의 버전 불일치를 없애기 위한 것이다. 런타임 추상화나 매 프레임 검색은 추가하지 않았다. 외형의 새 접근자는 한 번 생성하며, 이름 역조회는 scaffold 출력 경로에만 있다. 패치 대상/상태 전달/우선순위, Unity 정리 및 이벤트 해제, 선택적 연동 탐색·실패 격리, 네트워크 권한/소유권/중복 보호의 나머지 코드는 유지했다.

## 빌드와 자동 검사

```powershell
dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true
dotnet run --project tools/YamlSyncContractCheck/YamlSyncContractCheck.csproj -c Debug --no-build
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
python tools/GameCompatibilityCheck/run_mono.py '<Valheim 설치 폴더>' bin/Debug/CreatureManager.dll '<BepInEx/core 폴더>'
python tools/GameCompatibilityCheck/run_mono.py '<Valheim dedicated server 설치 폴더>' bin/Debug/CreatureManager.dll '<BepInEx/core 폴더>'
```

`python`은 실제 Python 3 실행 파일이어야 한다. 검사 exe는 게임의 Mono에서 실행한다. Windows .NET Framework에서 직접 실행하면 신규 Unity의 default interface 구현을 로드하지 못한다. 검사 도구는 원본 DLL을 참조하며 publicize하지 않는다.

수행 결과:

- Debug 솔루션 빌드: **경고 0, 오류 0**. ILRepack 후 최종 DLL만 `BepInEx/plugins/CreatureManager.dll`로 자동 복사. 원본/배치본 SHA-256 `FE1B9E26BBF187A1EC162BCE18DDB2C747E7FEED5BF08156451F7658C925A574` 일치.
- Release 솔루션 빌드: **경고 0, 오류 0**. `bin/Release/CreatureManager.dll`과 `Thunderstore/CreatureManager_v1.1.13.zip` 생성. ZIP manifest/의존성, 6개 허용 파일, DLL 일치 여부를 검사.
- YAML 동기화 DTO·strict parser 계약 검사 통과.
- 클라이언트와 서버의 원본/Mono 각각: 게임·Unity IL 참조 **7,452곳**, Harmony 대상 **97곳** (CreatureManager 86 + 내장 ServerSync 11), 실제 원본 IL에 대한 transpiler 적용 **5곳**, 실패 0.
- Harmony 일반 인자/결과/인스턴스/필드 주입/상태 타입 검사. 동적 Localization 대상 및 외형 delegate, AI·원거리·은신 조회 경로 확인.
- 원본 private 외형 필드 접근과 `assembly_guiutils`의 실제 private 번역 사전 접근을 게임 Mono에서 실행. 번역 적용/원복/타 모드 변경 보존 검사 통과.
- 원본 ZDO `GetData`와 실제 모드 serializer/deserializer로 스냅샷 v2의 7개 섹션, 외부 키 제외, 잘못된 입력/구버전/후행 데이터 거부 검사 통과. 이것은 월드 저장이나 네트워크를 사용하는 전체 capture/restore 호출의 실행 검증은 아니다.
- 내장 ServerSync의 CustomSyncedValue 초기화/값 변경, 콘솔 등록과 관리자 플래그, 좌표 변환 검사 통과.
- 내장 ServerSync는 공통 기준본 `valheim-1.0.7-r1`의 고정 DLL과 SHA-256이 일치. 해당 기준본의 소스·입력 해시·빌드·정적/격리 검증은 전역 ServerSync 자료에 보관. 클라이언트/서버 주요 원본 DLL 8개의 설치본·보관본·manifest SHA-256 일치 확인.

자동 검사는 독립 프로세스에서 BepInEx ThreadingHelper의 관리형 대기열만 구성한다. Unity 객체 생성, 예약된 ServerSync 패치 설치, native detour 설치, 월드/서버 실행은 하지 않는다. private 접근 표본의 성공을 모든 게임 실행 경로의 검증으로 확대하지 않는다.

## 실제 실행에서 남은 확인

제공 로그는 **수정 전** 초기화 실패의 증거다. 패치 후 Valheim 프로세스를 시작하거나 월드에 입장하지 않았다. 다음은 아직 필요한 실행 확인이며 자동 검사 통과와 구분한다.

1. 최소 모드 클라이언트: 최초 로드, 월드 입장/종료/재입장, 한국어/영어 전환과 번역 hot reload/원복, 텍스처·HUD·장비·머리/수염·래그돌 표시.
2. 로컬 호스트: 일반/명령/성장/알/번식/투사체 소환, 레벨 재적용·체력 보존, Swift의 걷기/비행/수영, Deathward와 지연 피해·variant 표시.
3. 데디케이트 서버 + 원격 클라이언트: 설정/언어/YAML/텍스처 동기화, 권한 없는 명령 거부, 관리자 명령, 접속 해제·소유권 이전·재접속·세션 초기화.
4. 저장/재로드·보상: modifier capture/restore, 타 모드 키 보존, 죽음 통지 중복·부분 실패·소유권 이전 때 보상 소실/복제 여부, cheated 표시 전파, Karma 실외/던전의 경계 및 3×3 범위.
5. 선택적 모드 조합: FeedLikeGrandma 포획/해제, ExpandWorldData 바이옴/위치, ServerDevcommands 권한 및 명령 순서. 제공 로그에 이들 모드 자체의 초기화 오류도 있어 실제 조합 성공을 확인한 상태는 아니다. 다른 모드 DLL은 수정하지 않았다.

생성물(bin/obj/ZIP), 게임·Unity·BepInEx 전체 구현, 텍스처/프리팹 리소스 내부 전체, 다른 모드 전체는 코드 수정 범위에서 제외했다. 기존 추출 자료의 관련 원본 C#/IL/접근 수준을 검토했으며 재추출·전체 리소스 분석·프레임 시간/할당 측정은 하지 않았다. 1.0.7 밖의 게임 버전이나 다른 플랫폼을 보증하지 않는다.

## 의존성 변경 재현 및 근거

ServerSync 기준본: `C:/Users/blizz/.codex/references/valheim/integrations/serversync/versions/valheim-1.0.7-r1`. ID `valheim-1.0.7-r1`, upstream commit `c57c2aa54e07cdcc7630d6068699ea781622323e`, DLL SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`. 기준본의 `README.md`, `manifest.json`, `src`, `Build.ps1`, 검증 자료가 재현 근거다. 프로젝트는 해당 고정 DLL을 `Libs`에 vendor하고 기존처럼 최종 모드 DLL에 병합한다. 게임 DLL이나 ServerSync를 별도 플러그인으로 설치하지 않는다.

전역 증거: `C:/Users/blizz/.codex/references/valheim/comparisons/0.221.12--1.0.7-windows-x64/creaturemanager.md`. 로컬 실행 로그/해시/IL 비교: `artifacts/Valheim107/` (Git 제외).

1.1.13 Release SHA-256: DLL `04E4F5CA564C92BD2A8090222A9A9B74AE8F596FAD0A68F3A0BB7B1D0D16FEBA`, ZIP `5ADECD7AF2EF3F47CE999B4C9CE21581C844B4EF812FBE66819C490A1214747C`.

안전한 변경 단위는 (1) 참조/내장 ServerSync 갱신, (2) 콘솔·외형 경계, (3) ZDO·Karma·피해/보상 경계, (4) 게임에서 고친 우회 제거, (5) 자동 검사 후 위의 실제 실행 순서다. 1.0.7 빌드가 성립하려면 필요한 API 변경을 함께 적용해야 하므로 중간 단계는 컴파일 진단 상태로 다루고, 배치본은 성공한 최종 빌드로만 갱신한다. 전체 소스와 해당 Debug DLL을 한 쌍으로 되돌릴 수 있다.
