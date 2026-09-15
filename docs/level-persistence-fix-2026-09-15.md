# 처리 완료 개체의 레벨 복원 수정

2026-09-15. FeedLikeGrandma 급식으로 올린 레벨이 존 재로드 뒤 내려가는 제보에 대한 CreatureManager 수정이다. 사용자와 합의한 정책은 **이미 처리한 개체의 레벨을 다시 결정하거나 이전 목표값으로 강제하지 않고, 게임의 현재 저장 레벨로 런타임 상태만 복원하는 것**이다.

## 변경

`CreatureLevelManager.SynchronizeStoredRuntimeState`는 이제 `Math.Max(1, zdo.GetInt(ZDOVars.s_level, 1))`을 사용한다. 기존에는 `CreatureManager_DesiredLevel`이 있으면 게임의 `s_level`보다 우선했다. 이 별도 키가 급식 전 레벨을 보유한 경우 재로드에서 `Character.m_level`을 낮추던 경로를 제거했다.

호출 범위는 기존과 같다.

- `TryApplyLevelState`의 처리 완료 및 기존 추첨값 보존 분기.
- `UpdatePendingApplications`의 대응 분기. 비owner 인스턴스의 복원도 같은 레벨 선택을 사용한다.

최초 생성의 레벨 선택·Karma·명령·교배·성장 정책은 변경하지 않았다. `CreatureManager_DesiredLevel`은 기존 초기/명시적 할당 기록과 관련 처리에서 유지하며 이번 재로드 코드에서는 읽거나 갱신하지 않는다. 새로운 저장 키·네트워크 메시지·매 프레임 처리·외부 모드 키 쓰기를 추가하지 않았다.

게임 기본값과 같이 `s_level`이 없으면 1이며, 1 미만의 값은 1로 제한한다. 두 레벨의 최댓값을 선택하지 않으므로 의도적인 레벨 하향도 유지한다. 기존 Unity 객체의 메모리 값이 오래됐어도 그것을 저장값보다 우선하지 않는다.

`RestoreStoredHealthMultiplier`, 호출자의 외형 갱신, 이미 저장된 modifier의 런타임 적용은 유지했다. 레벨 처리 완료 표시를 보고 전체 초기화를 생략하지 않는다. modifier 처리에는 별도 적용 완료 표시가 있으므로 최초 modifier 적용과 기존 modifier 복원 경계도 바꾸지 않았다.

## 검증

`tools/GameCompatibilityCheck/ManagedContracts.cs`에 레벨 보존 회귀 검사를 추가했다.

- 새 검사로 기존 최종 Release 1.1.13 DLL을 검사하면 `Level reload: fed level survives stale initial level`에서 실패한다.
- 수정한 최종 Debug DLL은 클라이언트와 데디케이트의 원본 Managed DLL·게임 Mono 각각에서 통과한다.
- 게임 저장 레벨 상승 / 의도적 하향 / CM 할당 키 없음 / 게임 레벨 키 없음 / 잘못된 0 값의 5가지 사례, 완료 표시 2가지로 총 10개 저장 상태를 각각 2회 읽어 총 20회 확인했다.
- 게임 및 CM 레벨 저장값, 처리 표시, 저장된 체력·공격력 배율, ZDO DataRevision을 변경하지 않는지 확인했다.
- 기존 게임/Unity 참조 검사, Harmony 대상 97개 및 transpiler 적용 5개, 기존 관리형 계약 검사도 실패 0이었다.

검사는 최종 DLL의 `SynchronizeStoredRuntimeState`에서 **레벨을 선택하는 실제 IL 구간**을 추출해 실행한다. 테스트에 선택 공식을 다시 작성하지 않았다. 이전 구현의 Character.GetLevel fallback은 정수 fixture로 공급하며, 실제 ZDO 읽기는 원본 게임 API를 호출한다. 읽기 구간을 벗어난 제어 흐름·새 호출이 나오면 검사는 실패한다. 원본 DLL이나 모드 DLL을 다시 쓰지 않는다.

전체 Character 메서드를 독립 실행하려는 초기 fixture는 Character 정적 초기화의 Unity Animator native 호출 때문에 실행할 수 없었다. 테스트에서 전체 Unity 엔진을 대신 구현하지 않고 위의 읽기 구간으로 검증 범위를 좁혔다. 따라서 **실제 Character 생성/파괴, owner 이전, 건강/외형 적용, Harmony detour, 월드 저장·로드 및 RPC 수송을 실행한 테스트가 아니다.** 완료 표시를 넣은 fixture도 호출자 분기 자체를 실행한 것은 아니며 공통 복원 함수의 읽기 동작을 확인한다.

실행 명령:

```powershell
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
dotnet build CreatureManager.csproj -c Debug -p:DeployToGame=true
& 'C:\Users\blizz\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' `
  tools/GameCompatibilityCheck/run_mono.py `
  'C:\Program Files (x86)\Steam\steamapps\common\Valheim' `
  bin/Debug/CreatureManager.dll `
  'C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\core'
```

서버 검사는 같은 명령의 게임 경로를 `C:\Users\blizz\.codex\references\valheim\snapshots\dedicated-server-b25253791-windows-x64-20260912T115223Z\original`로 바꿔 실행했다. 보존된 1.0.12 서버 원본과 Mono를 사용했으며 설치 게임을 서버 DLL로 덮어쓰지 않았다.

## 빌드·배치

- Debug 빌드: 경고 0, 오류 0. 최종 ILRepack 성공 후 일반 plugins 폴더로 자동 복사했다.
- 최종 DLL: `C:/Users/blizz/RiderProjects/CreatureManager/bin/Debug/CreatureManager.dll`.
- 로컬 배치: `C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/plugins/CreatureManager.dll`.
- 양쪽 SHA-256: `97E5D826B86259D8855BC43B7892A6B64CD6B59BE9211671FE81CCC8DE72E013`.
- 버전은 1.1.13 그대로다. 이번 요청에서는 Release·ZIP·게시·커밋·푸시를 수행하지 않았다. 제보한 원격 데디케이트 서버에는 배포하지 않았다.

기존 프로젝트의 원본 기반 `obj` 전용 publicizer 빌드와 최종 접근 특성을 유지했다. 게임 원본을 분석용으로 publicize하지 않았고 게임 원본 DLL을 수정하지 않았다. 원본 메타데이터의 Character.m_level 접근 제한은 private이며, 이 변경은 프로젝트가 이미 사용하던 직접 필드 접근 경로를 유지한다. 전체 참조/접근 방식을 일괄 교체하지 않았다.

## 실제 게임 확인과 복구 범위

복사본 월드에서 CM 처리된 동물을 길들이고 존을 한번 재로드한 뒤 급식으로 레벨을 올린다. 같은 개체의 ZDOID, owner, `GetLevel()`, `s_level`, `CreatureManager_DesiredLevel`을 기록하고 모든 플레이어가 영역을 떠나 실제 언로드를 유도한 뒤 돌아온다. 게임 저장 레벨이 메모리에 그대로 복원되는지 확인한다. 반복 재로드·서버 재시작·두 클라이언트의 owner 이전과 의도적 레벨 하향을 구분해 시험한다. 기존 특성/배율/외형, 식량 수량과 Orb 포획/해제도 확인한다.

게임 `s_level`에 높은 레벨이 남아 있으면 이후 복원에서 그 값을 사용한다. 이미 추가 급식·다른 저장 경로로 `s_level`이나 Orb payload까지 낮아졌다면 이 패치가 과거 레벨을 추정해 되살리지는 않는다. 이미 살아 있는 인스턴스를 즉시 수정하는 전역 복구 루프도 추가하지 않았다. 게임 및 서버 프로세스 재시작으로 새 DLL을 로드한 뒤 검증해야 한다.

## 후속 1.1.14 Release

사용자의 별도 릴리스 요청으로 버전을 1.1.14로 올리고 changelog/manifest를 갱신했다. `dotnet build CreatureManager.csproj -c Release`는 경고 0·오류 0으로 성공했다. 최종 Release DLL로 클라이언트·데디케이트 원본/Mono 호환성 검사, 레벨 보존 검사와 기존 관리형 계약 검사를 다시 실행해 실패 0이었다. YAML 동기화 DTO/strict parser 검사도 통과했다. 실제 월드/서버 세션은 실행하지 않았다.

- DLL: `bin/Release/CreatureManager.dll`, assembly version `1.1.14.0`, SHA-256 `DFB27EA5140E36EAF07B28AD9EC5B609C81B70D5E10003F77A6C5802C8D05B17`.
- ZIP: `Thunderstore/CreatureManager_v1.1.14.zip`, SHA-256 `07480F4AC70AC6A6B0DD5BA0BA6A44905CBAC2DEB0AED0A3CE999C1A0897339F`.
- ZIP의 허용 파일 6개, manifest 버전/BepInEx 의존성, 패키지 DLL과 빌드 DLL 해시, README/changelog/번역 원본 일치를 확인했다.

앞선 Debug DLL 배치 기록과 이 Release 산출물은 별개다. 일반 Release 빌드의 기존 패키징을 사용했으며 업로더 설정·대기열·사이트 게시 상태는 이번 요청에서 검사하지 않았다.
