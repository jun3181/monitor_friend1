# 가상속 Mate — Phase 1: VRM 데스크톱 Mate 핵심 기능 구현 지시서

> 이 문서는 로컬 ChatGPT Codex가 Unity 프로젝트에서 **VRM 기반 데스크톱 Mate의 핵심 기능**을 구현할 때 따라야 하는 기획·설계·작업 지시서다.
>
> 이번 단계의 핵심은 다음 두 가지를 함께 검증하는 것이다.
>
> 1. Mate가 사람처럼 자연스럽게 움직이는가?
> 2. Mate가 Windows 바탕화면 위에서 실제 데스크톱 캐릭터처럼 이동하고 상호작용할 수 있는가?

---

## 0. 작업 원칙

1. 이 문서의 범위만 구현한다.
2. Desktop Mate는 **동작 구조와 설계 아이디어만 참고**한다.
3. Desktop Mate의 코드, 에셋, 애니메이션, 모델을 복사하거나 프로젝트에 포함하지 않는다.
4. 프로젝트에 이미 존재하는 구조가 있다면 먼저 조사하고, 불필요하게 새 구조를 중복 생성하지 않는다.
5. 실제 프로젝트 상태와 이 문서가 충돌하면 현재 프로젝트를 우선하되, 변경 이유를 기록한다.
6. 외부 패키지 API는 설치된 버전을 직접 확인한 후 사용한다. 오래된 예제의 API를 추측해서 작성하지 않는다.
7. 컴파일되지 않는 코드나 Inspector 연결이 불가능한 상태를 완료로 간주하지 않는다.
8. 애니메이션 파일이 없다면 임의의 상용·타 프로그램 에셋을 복사하지 않는다. 시스템과 슬롯을 구성하고, 부족한 에셋을 명확히 기록한다.

---

## 1. 프로젝트 목표

Unity 3D 프로젝트에서 VRM 캐릭터를 Mate로 사용하고 다음 기능을 하나의 Windows 데스크톱 프로그램으로 통합한다.

### 자연스러운 Mate 움직임

- 기본 대기 자세
- 여러 대기 동작의 자연스러운 전환
- 반복이 눈에 띄지 않는 랜덤 행동
- 고개와 눈의 자연스러운 시선 이동
- 불규칙한 눈 깜빡임
- 호흡과 몸 중심의 미세한 움직임
- VRM의 머리카락·옷·장식 물리
- 화면 안 걷기와 정지 상태 전환

### Windows 데스크톱 동작

- 바탕화면 위에서 배경이 투명한 창
- 필요 시 다른 창보다 위에 표시
- Mate가 없는 투명 영역은 클릭 통과
- Mate를 직접 조작할 때는 입력 수신
- Windows 로그인 후 자동 실행 선택 기능

### 사용자 상호작용

- 마우스로 Mate 끌기
- 클릭 반응
- 쓰다듬기 반응
- 걷기, 잡힘, 드래그, 반응 후 Idle 복귀

최종적으로 Mate는 단순한 Unity 씬 속 모델이 아니라, **Windows 바탕화면 위에서 자연스럽게 움직이고 사용자 입력에 반응하는 데스크톱 캐릭터**처럼 동작해야 한다.

---

## 2. Phase 1 범위 구분

Phase 1 안에서 다음 하위 단계로 나누어 구현한다.

```text
Phase 1A — VRM Import와 자연스러운 움직임
Phase 1B — Windows 투명 데스크톱 창
Phase 1C — 화면 이동과 마우스 상호작용
Phase 1D — Windows 자동 실행과 통합 검증
```

다음 기능은 Phase 1에서 구현한다.

- Windows 바탕화면 투명 창
- 클릭 통과
- 항상 위 표시
- Windows 시작 프로그램 등록
- 화면 안 걷기 또는 단순 경로 이동
- 마우스로 Mate 끌기
- 쓰다듬기와 클릭 반응

다음 기능은 Phase 1에서 구현하지 않는다.

- 마이크 입력
- 음성 인식(STT)
- LLM 대화
- 음성 합성(TTS)
- 립싱크
- 사용자 장기 기억
- 다중 Mate
- 캐릭터 선택 UI
- 파일 탐색기를 통한 VRM 선택
- 네트워크 통신
- 복잡한 장애물 인식
- 다른 Windows 창 위에 올라서기
- 모니터 간 고급 경로 탐색
- 자율 행동용 LLM 또는 AI 의사결정

단, 이후 음성 대화와 LLM 기능을 추가하기 어렵게 만드는 강한 결합은 피한다.

---

## 3. Desktop Mate에서 참고할 부분

제공된 Desktop Mate 배포 파일은 Unity `6000.3.8f1` 기반 IL2CPP 빌드다. 파일과 메타데이터에서 다음 움직임 관련 구성 요소가 확인되었다.

### 확인된 개념

- `CharacterAnimationManager`
- `CharacterLookAtIkManager`
- `CharacterArmIkManager`
- `CharacterFacialManager`
- `CharacterSwayer`
- `CharacterManager`
- `CharacterLifetimeScope`
- `MainStateMachine`
- `StandState`
- `SitState`
- `StayStateV2`
- `PickedState`
- `DraggedState`
- `StrokedState`
- `MotionManifest`
- `MotionTypeDatabase`
- `AnimData`
- `FacialData`
- `BlinkData`
- `MouthAnimationData`
- `SwayParameter`
- `VRMExpressionHelper`
- `LookAtSoftClip`
- `UpdateRandomIdleAnimation`
- `UpdateLookAtIK`
- `UpdateArmIK`
- `VRM10Blinker`
- `VRM10AutoExpression`
- Final IK 관련 구성
- FastSpringBone / Magica Cloth 관련 구성

### 데스크톱 기능에서 확인된 개념

- `UniWindowController`
- `WindowClickThroughView`
- `AppMoveHandle`
- `UniWindowMoveHandle`
- `WindowsStartupLaunchSetter`
- `IStartupLaunchSetter`
- `DraggedState`
- `PickedState`
- `StrokedState`
- `CharacterInteractionView`
- `DraggingView`
- `PickView`
- `WindowsNativeWindowEnumerator`
- `PlatformNativeWindowSynchronizer`
- `CharacterMonitorQuery`

Desktop Mate의 코드나 에셋을 복사하지 않고, 다음 설계 원리만 참고한다.

- OS 창 제어와 Mate 애니메이션을 별도 계층으로 분리한다.
- 창 전체가 아니라 Mate가 표시된 영역만 입력을 받을 수 있게 한다.
- 드래그와 쓰다듬기 같은 입력은 Mate 상태 전환 요청으로 변환한다.
- 화면 좌표 이동과 3D 월드 좌표 이동을 명확히 구분한다.
- 자동 실행 기능은 Windows 전용 인프라 기능으로 격리한다.

### 여기서 가져올 설계 원리

Desktop Mate의 자연스러움은 하나의 고품질 애니메이션만으로 만드는 것이 아니다. 다음 계층을 동시에 합성하는 방식으로 판단된다.

```text
큰 상태
  └─ 서 있기 / 앉기 / 잡힘 / 쓰다듬기 등

기본 애니메이션
  └─ 현재 상태에 맞는 반복 동작

랜덤 보조 행동
  └─ 여러 Idle 또는 짧은 일회성 동작

절차적 보정
  ├─ 고개와 눈의 시선
  ├─ 몸의 미세한 흔들림
  └─ 필요 시 팔 IK

얼굴
  ├─ 눈 깜빡임
  └─ 표정

물리
  └─ 머리카락 / 옷 / 장식
```

우리 프로젝트는 이 원리를 참고하되, Phase 1에서는 필요한 최소 구성만 새로 구현한다.

---

## 4. Phase 1 완료 형태

### Mate 상태 흐름

```text
Initialize
    ↓
Idle
    ├─ Base Idle
    ├─ Random Idle Variant
    ├─ Look / Blink / Micro Motion
    ├─ ClickReaction
    ├─ StrokeReaction
    ├─ Walk
    └─ Picked / Dragged
```

권장 상태 전환:

```text
Idle
 ├─ 일정 시간 경과 → Walk → 목적지 도착 → Idle
 ├─ 클릭 → ClickReaction → Idle
 ├─ 쓰다듬기 → StrokeReaction → Idle
 └─ 마우스 누름·이동 → Picked/Dragged → 놓음 → Landing/Idle
```

### Windows 창 흐름

```text
Application Start
    ↓
Unity Window 생성
    ↓
투명 배경 적용
    ↓
테두리 제거
    ↓
항상 위 설정 적용
    ↓
Mate 영역 Hit Test
    ├─ Mate 위: 입력 수신
    └─ 빈 투명 영역: 클릭 통과
```

Phase 1에서는 과도하게 범용적인 AI 상태 머신을 만들 필요는 없다. 다만 `Idle`, `Walking`, `Picked`, `Dragged`, `Reacting` 상태의 책임은 분리하고, 이후 `Listening`, `Talking`, `Thinking` 상태가 추가될 수 있도록 설계한다.

---

## 5. 필요한 에셋

### 필수

- 테스트용 VRM 0.x Humanoid 모델 1개: `1456081260089804624.vrm`
- 기본 Idle 애니메이션 1개 이상
- Unity Humanoid로 리타게팅 가능한 애니메이션 클립

### 권장

- 기본 Idle 1개
- Idle Variant 3개 이상
- 짧은 일회성 동작 2개 이상
  - 주변 둘러보기
  - 자세 바꾸기
  - 기지개
  - 팔이나 옷 정리
- 호흡용 Additive 애니메이션 1개
- 걷기 애니메이션 1개
- 잡힘 또는 드래그 중 자세 애니메이션 1개
- 클릭 반응 애니메이션 1개
- 쓰다듬기 반응 애니메이션 1개

### 에셋이 부족할 때

- 시스템 구현은 계속 진행한다.
- Inspector 또는 ScriptableObject에 클립을 연결할 슬롯을 만든다.
- 존재하는 클립만으로 기능을 검증한다.
- 없는 애니메이션을 임의로 다른 프로그램에서 추출하지 않는다.
- 완료 보고서에 부족한 에셋을 적는다.



### 제공된 테스트 VRM 파일 정보

이번 Phase 1에서 우선 사용할 파일은 다음과 같다.

```text
1456081260089804624.vrm
```

파일 내부 메타데이터를 확인한 결과는 다음과 같다.

- 형식: VRM 0.x
- VRM 메타 버전: `0.7`
- Exporter: `UniVRM-0.44`
- Humanoid Bone: 54개
- BlendShape Group: 15개
- Spring Bone Group: 28개
- Collider Group: 12개
- 내장 Animation Clip: 0개
- 모델 제목: `Shinobu`
- 제작자 표기: `Akko`
- 상업적 이용: 불가로 설정됨

따라서 이 파일은 다음 용도로 사용한다.

- Unity 및 UniVRM Import 검증
- Humanoid Avatar 및 Bone Mapping 확인
- 표정, Blink, Spring Bone 작동 확인
- 외부 Humanoid Animation 리타게팅 테스트
- Mate 움직임 시스템의 개발용 테스트 모델

이 VRM에는 자체 동작 애니메이션이 포함되어 있지 않다. 자연스러운 사람 움직임은 다음 요소를 별도로 연결해 구현해야 한다.

```text
VRM 모델
  + Humanoid Animation Clip
  + Animator Controller
  + Idle 전환 시스템
  + Look At / IK
  + Blink / Expression
  + Spring Bone
```

라이선스 메타데이터상 상업적 이용이 허용되지 않으므로, 이 파일은 개발 및 개인 테스트용으로만 취급한다. 배포용 Mate 모델은 별도의 사용 허가가 명확한 VRM으로 교체한다.

---

## 6. 기술 방향

### Unity

- 현재 프로젝트의 Unity 버전을 사용한다.
- 새 프로젝트라면 Unity 6 계열을 기본 후보로 삼되, 이미 설치된 안정 버전을 우선한다.
- 렌더 파이프라인은 URP를 권장하지만 Phase 1의 핵심 요구사항은 아니다.

### 대상 플랫폼

- Phase 1의 최종 대상은 Windows 10/11 64비트다.
- Unity Editor에서는 VRM과 애니메이션 기능을 우선 검증한다.
- 투명 창, 클릭 통과, 항상 위, 자동 실행은 Windows Player 빌드에서 반드시 별도로 검증한다.
- Windows 전용 코드는 플랫폼 조건부 컴파일 또는 별도 어셈블리로 격리한다.
- 다른 OS에서 컴파일이 깨지지 않도록 Windows API 호출부를 직접 흩뿌리지 않는다.

### Windows 창 처리

- 투명 창 구현은 설치된 Unity 버전과 렌더 파이프라인에서 실제 동작하는 방법을 조사한 후 결정한다.
- 우선순위는 검증된 오픈소스 Unity Windows 투명 창 라이브러리 또는 최소한의 Win32 래퍼다.
- 외부 라이브러리를 채택할 경우 라이선스와 Unity 6 호환성을 기록한다.
- 투명 배경, 테두리 제거, 항상 위, 클릭 통과 기능을 하나의 `DesktopWindowController` 계층에서 관리한다.
- 클릭 통과는 영구 활성화하지 않는다. Mate를 조작해야 하는 순간에는 입력을 받을 수 있어야 한다.
- 투명 영역 판정은 가능한 경우 렌더 알파 또는 Mate 전용 Hit Collider/Raycast 결과를 사용한다.

### VRM

- 프로젝트 Unity 버전과 호환되는 UniVRM을 사용한다.
- 이번 테스트 모델은 **VRM 0.x** 형식이므로, UniVRM 설치 시 VRM 0.x Import 기능이 포함된 구성을 사용한다.
- VRM 1.0 전용 API만 기준으로 구현하지 않는다. 현재 모델의 실제 Import 결과와 설치된 UniVRM API를 우선한다.
- 설치된 UniVRM의 실제 API와 공식 샘플을 먼저 확인한다.
- Phase 1에서는 VRM 파일 선택 UI를 구현하지 않는다.
- 가장 단순한 방법으로 `1456081260089804624.vrm`을 Unity `Assets` 아래에 Import하고, 생성된 Prefab을 테스트 씬에 배치한다.
- 이후 Runtime VRM 로딩으로 교체할 수 있도록, Mate 제어 코드가 특정 프리팹 경로 또는 특정 모델 이름에 직접 의존하지 않게 한다.

### IK

- Desktop Mate가 사용한 Final IK를 그대로 요구하지 않는다.
- Phase 1에서는 다음 우선순위를 따른다.

1. Unity `Animator.OnAnimatorIK`로 시선 구현 가능 여부 확인
2. UniVRM의 LookAt 기능 사용 가능 여부 확인
3. 필요하면 Unity Animation Rigging 패키지 검토

유료 패키지를 필수 의존성으로 추가하지 않는다.

### 물리

- VRM 모델에 포함된 Spring Bone을 우선 사용한다.
- 별도의 Cloth 시스템은 Phase 1 필수가 아니다.
- 머리카락과 옷이 지나치게 흔들리지 않도록 물리 강도를 점검한다.

---

## 7. 권장 폴더 구조

기존 프로젝트 규칙이 없다면 다음 구조를 사용한다.

```text
Assets/
└─ _Project/
   └─ Mate/
      ├─ Runtime/
      │  ├─ Core/
      │  ├─ Animation/
      │  ├─ Look/
      │  ├─ Face/
      │  ├─ Physics/
      │  ├─ Movement/
      │  ├─ Interaction/
      │  └─ Desktop/
      ├─ Platform/
      │  └─ Windows/
      ├─ Data/
      ├─ Prefabs/
      ├─ Animations/
      ├─ Scenes/
      └─ Debug/
```

Phase 1에서 과도한 Assembly Definition 분리는 하지 않는다. 프로젝트에 이미 Assembly Definition 규칙이 있다면 그 규칙을 따른다.

---

## 8. 권장 책임 분리

아래 이름은 권장안이며 프로젝트 기존 명명 규칙에 맞게 바꿀 수 있다.

### `MateController`

Mate 전체를 조율하는 진입점이다.

책임:

- 필요한 하위 컴포넌트 참조 확인
- 초기화 순서 제어
- 현재 Mate 상태 전달
- Phase 1에서는 Idle 상태 진입

하지 말아야 할 일:

- 랜덤 시간 계산
- 눈 깜빡임 구현
- 직접 본 회전 조작
- 모든 기능을 한 클래스에 작성

### `MateAnimationController`

Animator와 애니메이션 상태 전환을 담당한다.

책임:

- Base Idle 재생
- Idle Variant 전환
- CrossFade 처리
- 애니메이션 재생 상태 제공
- 외부에서 특정 행동을 요청할 수 있는 진입점 제공

### `MateIdleScheduler`

언제 어떤 Idle Variant를 재생할지 결정한다.

책임:

- 다음 행동까지의 랜덤 대기 시간
- 가중치 기반 선택
- 같은 동작 연속 선택 방지
- 행동 쿨다운
- 현재 상태가 Idle일 때만 요청

Animator를 직접 조작하지 않고 `MateAnimationController`에 요청한다.

### `MateLookController`

고개와 눈의 시선을 담당한다.

책임:

- 현재 시선 목표 관리
- 카메라 또는 임시 사용자 타깃 보기
- 주변 임의 지점 보기
- 부드러운 전환
- 시선 각도 제한
- 시선 목표가 없을 때 자연스럽게 정면 복귀

### `MateBlinkController`

VRM 표정 시스템을 이용한 눈 깜빡임을 담당한다.

책임:

- 불규칙한 간격으로 깜빡임
- 짧은 닫힘과 열림 보간
- 낮은 확률의 연속 두 번 깜빡임
- 다른 표정이 눈을 제어하는 동안 충돌 방지 가능 구조

### `MateExpressionController`

Phase 1에서는 Neutral 표정 유지와 Blink 충돌 방지 정도만 담당한다.

이후 감정 시스템이 표정을 요청할 수 있는 진입점을 남긴다.

### `MateScreenMovementController`

화면 안에서 Mate가 걷고 정지하는 동작을 담당한다.

책임:

- 화면 안 목적지 선택
- 목적지 방향으로 Mate 회전
- 걷기 애니메이션과 실제 위치 이동 동기화
- 화면 경계 안에서만 이동
- 목적지 도착 후 Idle 전환
- 드래그나 반응 상태에서는 이동 중단

Phase 1에서는 NavMesh 기반 복잡한 경로 탐색보다 **2D 화면 평면 위의 단순 목적지 이동**을 우선한다.

### `MateDragController`

마우스로 Mate를 잡고 이동시키는 입력을 담당한다.

책임:

- Mate Collider 또는 Hit Area에서 Pointer Down 감지
- 드래그 시작 임계값 적용
- 화면 좌표를 Mate 위치로 변환
- 드래그 중 `Picked` 또는 `Dragged` 상태 요청
- 놓았을 때 화면 경계 보정
- 단순 클릭과 드래그 구분

### `MateInteractionController`

클릭과 쓰다듬기 같은 상호작용을 해석한다.

책임:

- 짧은 클릭 감지
- 클릭 위치 또는 Collider 영역에 따른 반응 요청
- 일정 거리 이상의 연속 마우스 이동을 쓰다듬기로 판정
- 쓰다듬기 쿨다운과 반복 제한
- 반응 애니메이션 종료 후 Idle 복귀

Phase 1에서는 머리, 몸 등 세부 부위 분리는 선택 사항이다. 우선 Mate 전체 Collider로 동작을 검증하고 이후 확장 가능하게 한다.

### `DesktopWindowController`

Windows Player 창의 동작을 담당한다.

책임:

- 투명 배경
- 창 테두리 제거
- 항상 위 On/Off
- 클릭 통과 On/Off
- 창 크기와 위치 관리
- Windows 외 플랫폼에서 안전하게 비활성화

Mate의 애니메이션이나 상태를 직접 제어하지 않는다.

### `DesktopInputHitTest`

현재 마우스 위치가 Mate 입력 영역인지 판단한다.

책임:

- Mate Collider 또는 전용 Interaction Layer Raycast
- UI가 있다면 UI 입력 우선 처리
- 빈 투명 영역일 때 클릭 통과 요청
- Mate 위에 있을 때 입력 수신 요청
- 필요 이상으로 Win32 상태를 매 프레임 반복 변경하지 않음

### `WindowsStartupController`

Windows 로그인 시 자동 실행 설정을 담당한다.

책임:

- 자동 실행 등록
- 자동 실행 해제
- 현재 등록 상태 확인
- 실행 파일 경로 변경에 안전하게 대응
- 관리자 권한 없이 현재 사용자 범위에서 동작

자동 실행 등록 여부는 사용자가 선택할 수 있어야 한다. 개발 중 무조건 등록하지 않는다.

### `MateMotionProfile` (`ScriptableObject` 권장)

코드 수정 없이 자연스러움 관련 값을 조정하기 위한 데이터다.

포함할 후보:

- Base Idle 클립 또는 Animator 상태 이름
- Idle Variant 목록
- 각 Variant 가중치
- 최소·최대 대기 시간
- CrossFade 시간
- 같은 동작 반복 방지 여부
- 시선 변경 간격
- 시선 최대 각도
- 시선 보간 속도
- Blink 최소·최대 간격
- Blink 닫힘·유지·열림 시간
- Double Blink 확률
- 미세 움직임 강도
- 걷기 속도
- 걷기 발생 최소·최대 간격
- 화면 가장자리 여백
- 목적지 도착 판정 거리
- 드래그 시작 거리
- 클릭 최대 시간
- 쓰다듬기 판정 이동 거리와 시간
- 상호작용 쿨다운

설정값을 여러 MonoBehaviour에 중복 저장하지 않는다.

---

## 9. Animator 설계

### 최소 레이어 구성

```text
Base Layer
  ├─ BaseIdle
  ├─ IdleVariant_01
  ├─ IdleVariant_02
  ├─ IdleVariant_03
  ├─ Walk
  ├─ Picked
  ├─ Dragged
  ├─ ClickReaction
  ├─ StrokeReaction
  └─ ReturnToIdle

Additive Layer (선택)
  └─ Breathing / MicroMotion
```

### 전환 원칙

- Idle Variant는 Base Idle에서 부드럽게 들어가고 다시 Base Idle로 돌아온다.
- `Any State` 전환을 남발하지 않는다.
- 전환 조건은 명확한 Trigger 또는 상태 요청으로 관리한다.
- Exit Time과 CrossFade가 겹쳐 예상치 못한 이중 전환이 생기지 않게 한다.
- 애니메이션이 In-Place인지 확인한다.
- Idle 상태에서 Root Motion으로 Mate 위치가 밀리지 않게 한다.
- 발이 바닥에서 뜨거나 미끄러지면 클립 Import 설정과 Foot IK 적용 여부를 확인한다.

### 랜덤 선택 초기값

다음 값은 최종값이 아니라 최초 튜닝 출발점이다.

- Idle Variant 발생 간격: 7~18초
- CrossFade: 0.25~0.6초
- 동일 Variant 연속 재생 금지
- 긴 행동 재생 후 최소 쿨다운: 8초

시간은 매번 범위 안에서 새로 뽑는다. 고정 주기로 반복하지 않는다.

---

## 10. 자연스러운 움직임 세부 설계

### 10.1 Base Idle

Base Idle 자체에 다음 요소가 포함된 클립을 우선 사용한다.

- 약한 호흡
- 어깨의 작은 움직임
- 중심축의 작은 이동
- 완전히 고정되지 않은 팔과 손

Base Idle의 움직임이 너무 크면 Variant와 구분이 어려워지고, 너무 작으면 정지 화면처럼 보인다.

### 10.2 Idle Variant

Idle Variant는 의미 있는 짧은 행동이다.

예시:

- 체중을 반대쪽 다리로 이동
- 고개를 살짝 돌려 주변 확인
- 팔 위치 변경
- 자세 정돈
- 짧은 기지개
- 잠시 아래를 봄

선택 규칙:

- 가중치 기반 랜덤
- 같은 행동 연속 재생 금지
- 짧은 행동과 긴 행동의 쿨다운 분리 가능 구조
- 현재 다른 중요한 행동이 재생 중이면 새 행동을 예약하지 않음

### 10.3 시선

Phase 1 시선 모드는 최소 두 가지다.

1. 카메라 또는 임시 사용자 타깃 바라보기
2. 주변 임의 지점 바라보기

권장 동작:

- 대부분은 정면 또는 카메라 근처를 본다.
- 일정 시간마다 시선을 조금 옮긴다.
- 가끔 화면 옆이나 아래를 잠깐 본다.
- 새 목표로 즉시 순간이동하지 말고 짧게 보간한다.
- 머리 전체가 과하게 회전하지 않도록 각도를 제한한다.
- 눈과 머리가 완전히 같은 비율로 움직이지 않게 조정한다.

최초 튜닝값 예시:

- 목표 유지 시간: 2~6초
- 좌우 최대 각도: 약 25도
- 위아래 최대 각도: 약 12도
- 머리 가중치보다 눈 가중치를 조금 높게 시작

값은 모델 비율에 따라 반드시 조정한다.

### 10.4 눈 깜빡임

권장 동작:

- 고정 간격 사용 금지
- 2.5~6초 사이의 불규칙한 간격부터 시작
- 낮은 확률로 Double Blink
- 닫힘은 빠르게, 열림은 약간 느리게

최초 튜닝값 예시:

- 닫힘: 0.06~0.10초
- 닫힘 유지: 0.02~0.06초
- 열림: 0.10~0.16초
- Double Blink 확률: 8~15%

VRM 모델마다 Blink Expression 이름과 동작이 다를 수 있으므로 설치된 UniVRM API와 모델 Expression 구성을 확인한다.

### 10.5 호흡과 미세 흔들림

우선순위:

1. Additive 호흡 애니메이션
2. Animation Rigging 기반 미세 보정
3. 마지막 수단으로 `LateUpdate` 본 보정

직접 본을 수정할 경우 Animator와 충돌할 수 있으므로 다음을 지킨다.

- Animator 평가 이후 적용
- 작은 각도만 사용
- Spine, Chest, Head에 과도한 회전 금지
- 프레임마다 새로운 GC 할당 금지
- 시간 기반 부드러운 곡선 사용
- 기능을 끌 수 있는 옵션 제공

몸 흔들림은 눈에 띄는 효과가 아니라, 완전 정지를 피하기 위한 보조 요소다.

### 10.6 Spring Bone

- VRM 모델 로드 후 Spring Bone이 정상 초기화되는지 확인한다.
- 머리카락이나 치마가 계속 떨리면 감쇠와 강성을 조정한다.
- Mate가 정지해 있을 때도 미세 움직임 때문에 물리가 폭주하지 않아야 한다.
- Collider 누락으로 머리카락이 몸을 관통하는지 확인한다.


### 10.7 화면 안 걷기

Phase 1의 걷기는 복잡한 3D 월드 탐색이 아니라 **현재 데스크톱 창 내부의 수평 영역을 이동하는 기능**이다.

권장 동작:

- Idle 상태에서 일정 확률로 새 목적지를 선택한다.
- 목적지는 화면 가장자리에서 일정 여백을 둔다.
- 목적지 방향으로 몸을 먼저 회전한 뒤 걷기 시작한다.
- 이동 속도와 Walk 애니메이션 속도를 가능한 범위에서 맞춘다.
- 목적지에 도착하면 자연스럽게 Idle로 CrossFade한다.
- 사용자가 Mate를 잡거나 클릭 반응이 시작되면 걷기를 즉시 중단한다.
- Phase 1 기본안은 In-Place Walk + 코드 위치 이동이다.

### 10.8 마우스로 Mate 끌기

- Mate Collider 위에서 마우스를 누른 경우에만 드래그 후보가 된다.
- Pointer Down 즉시 드래그로 보지 않고 최소 이동 거리 임계값을 둔다.
- 임계값을 넘으면 `Dragged` 상태로 전환한다.
- Mate의 기준점이 마우스 위치로 순간이동하지 않도록 클릭 오프셋을 유지한다.
- 드래그 중에는 자동 걷기, Idle Variant, 랜덤 시선을 일시 중단한다.
- 놓았을 때 화면 밖이면 가장 가까운 유효 영역으로 보정한다.

### 10.9 클릭 반응

- 짧은 Pointer Down/Up이며 드래그 임계값을 넘지 않은 입력을 클릭으로 본다.
- 클릭 중복으로 애니메이션이 계속 재시작되지 않도록 쿨다운을 둔다.
- Phase 1에서는 최소 한 종류의 클릭 반응을 구현한다.
- 기본안은 반응 중 추가 입력을 무시하고 종료 후 Idle로 돌아가는 방식이다.

### 10.10 쓰다듬기 반응

쓰다듬기는 한 번의 클릭이 아니라 Mate 위에서 일정 시간 동안 왕복 또는 연속 이동한 입력으로 판정한다.

최소 판정 요소:

- Mate 위에서 Pointer Down 유지
- 누적 이동 거리
- 입력 유지 시간
- 이동 속도의 상한과 하한
- 마지막 반응 이후 쿨다운

Phase 1에서는 정교한 제스처 인식이 아니라 오작동이 적은 단순 판정을 우선한다.

### 10.11 투명 창과 클릭 통과

- Unity 카메라 배경과 Windows 창 배경이 모두 투명하게 보여야 한다.
- Mate가 없는 영역에서는 바탕화면과 다른 프로그램을 정상적으로 클릭할 수 있어야 한다.
- Mate 위에서는 드래그, 클릭, 쓰다듬기 입력을 받을 수 있어야 한다.
- 클릭 통과 상태 전환으로 입력이 깜빡이거나 연속 클릭이 유실되지 않게 한다.
- 설정 또는 디버그 키로 클릭 통과를 강제로 끌 수 있는 안전장치를 둔다.

### 10.12 항상 위 표시

- 기본값을 항상 위로 할지 여부는 설정값으로 둔다.
- 개발 중에는 On/Off를 즉시 비교할 수 있어야 한다.
- 최소 목표는 일반 Windows 창과 바탕화면 위에서 안정적으로 유지되는 것이다.

### 10.13 Windows 자동 실행

- 현재 사용자 범위에서 등록한다.
- 자동 실행 여부를 사용자가 켜고 끌 수 있게 한다.
- 자동 실행으로 시작됐는지 구분할 수 있는 실행 인자를 고려한다.
- 자동 실행 시 개발용 디버그 창이나 설정 창을 강제로 띄우지 않는다.
- Editor Play 시에는 실제 시작 프로그램을 변경하지 않는다.

---

## 11. 컴포넌트 간 데이터 흐름

```text
MateController
    ├─ MateAnimationController
    │       ↑
    │  MateIdleScheduler
    │
    ├─ MateLookController
    ├─ MateBlinkController
    ├─ MateExpressionController
    ├─ MateScreenMovementController
    ├─ MateDragController
    ├─ MateInteractionController
    └─ MateMotionProfile

DesktopWindowController
    ↑
DesktopInputHitTest
    ↑
Mate Collider / Interaction Layer

WindowsStartupController
    └─ Windows 자동 실행 설정만 담당
```

규칙:

- Scheduler는 Animator를 직접 알지 않는다.
- ScreenMovementController는 Windows API를 직접 호출하지 않는다.
- DragController는 창 투명화 구현을 알지 않는다.
- DesktopInputHitTest는 Mate의 애니메이션을 직접 재생하지 않는다.
- WindowsStartupController는 Mate 상태에 관여하지 않는다.
- 공통 설정은 Profile 또는 설정 데이터에서 읽는다.
- 각 기능은 Inspector 또는 개발 설정에서 켜고 끌 수 있어야 한다.

---

## 12. 작업 순서

각 하위 단계가 독립적으로 동작하는 것을 확인한 뒤 다음 단계로 넘어간다.

### Phase 1A — VRM Import와 자연스러운 움직임

#### Step 1. 프로젝트 조사

확인 항목:

- Unity 버전과 렌더 파이프라인
- UniVRM 버전과 VRM 0.x 지원 여부
- Windows Player 빌드 설정
- 투명 창 관련 기존 패키지 또는 코드
- Animator Controller와 Humanoid 애니메이션
- 기존 입력 시스템
- 폴더 및 Assembly Definition 규칙

#### Step 2. VRM Import 및 Mate 표시

완료 조건:

- VRM Prefab이 오류 없이 생성된다.
- Humanoid Avatar가 유효하다.
- BlendShape, Spring Bone, Collider를 확인할 수 있다.
- 외부 Humanoid 애니메이션을 연결할 수 있다.

#### Step 3. Base Idle과 Random Idle

완료 조건:

- Base Idle이 반복된다.
- 최소 2개 이상의 Variant 슬롯을 지원한다.
- 같은 Variant 연속 선택 방지가 작동한다.
- CrossFade 후 Base Idle로 복귀한다.

#### Step 4. 시선, Blink, 미세 움직임, Spring Bone

완료 조건:

- 시선 추적과 각도 제한이 작동한다.
- 불규칙 Blink가 작동한다.
- 호흡 또는 미세 흔들림이 있다.
- Spring Bone이 안정적으로 반응한다.

### Phase 1B — Windows 투명 데스크톱 창

#### Step 5. 투명 창과 테두리 제거

완료 조건:

- Windows 빌드에서 Unity 배경이 투명하다.
- 창 테두리와 제목 표시줄이 보이지 않는다.
- Mate 외곽선과 반투명 소재가 심하게 깨지지 않는다.

#### Step 6. 항상 위와 클릭 통과

완료 조건:

- 항상 위 표시를 On/Off 할 수 있다.
- 빈 투명 영역을 통해 바탕화면을 클릭할 수 있다.
- Mate 위에서는 입력을 받을 수 있다.
- 클릭 통과 강제 해제 안전장치가 있다.

### Phase 1C — 화면 이동과 마우스 상호작용

#### Step 7. 화면 안 걷기

완료 조건:

- Mate가 화면 내부 목적지까지 걷는다.
- 방향 전환과 Walk 전환이 자연스럽다.
- 도착 후 Idle로 돌아간다.
- 상호작용 시작 시 걷기가 중단된다.

#### Step 8. Mate 끌기

완료 조건:

- Mate 위에서만 드래그가 시작된다.
- 클릭과 드래그가 임계값으로 구분된다.
- 드래그 중 전용 상태가 적용된다.
- 클릭 오프셋이 유지된다.
- 놓았을 때 화면 밖으로 사라지지 않는다.

#### Step 9. 클릭과 쓰다듬기 반응

완료 조건:

- 짧은 클릭에 최소 한 가지 반응을 한다.
- 연속 입력 쿨다운이 작동한다.
- 누적 이동 거리와 시간으로 쓰다듬기를 판정한다.
- 쓰다듬기에 최소 한 가지 반응을 한다.
- 반응 종료 후 Idle로 복귀한다.

### Phase 1D — 자동 실행과 통합

#### Step 10. Windows 시작 프로그램 등록

완료 조건:

- 사용자가 자동 실행을 켜고 끌 수 있다.
- 현재 등록 상태를 확인할 수 있다.
- Editor에서는 실제 등록을 변경하지 않는다.
- Windows 로그인 후 프로그램이 한 번 실행된다.

#### Step 11. 통합 검증

최소 10분 동안 다음을 함께 검증한다.

- Idle과 Random Idle
- 시선과 Blink
- Spring Bone
- 투명 창
- 클릭 통과
- 항상 위
- 화면 안 걷기
- 드래그
- 클릭 반응
- 쓰다듬기 반응
- 자동 실행 후 정상 초기화

기록 항목:

- 클릭 통과가 잘못 전환되는 위치
- Mate를 클릭할 수 없는 프레임
- 드래그 시작 시 위치 튐
- 걷기와 실제 이동 속도 불일치
- 화면 밖으로 나가는 경우
- 반응 상태가 종료되지 않는 경우
- 자동 실행 중복 여부
- 머리카락 또는 옷 물리 폭주

## 13. 디버그 기능

화려한 UI는 만들지 않는다. 개발 중 확인 가능한 최소 디버그 정보만 제공한다.

권장 표시 항목:

- 현재 Mate 상태
- 현재 Animator State
- 현재 재생 중인 Idle Variant
- 다음 행동까지 남은 시간
- 현재 시선 모드
- Blink 타이머
- 현재 화면 목적지
- 걷기 여부와 이동 속도
- Pointer가 Mate 위에 있는지
- 클릭 통과 상태
- 항상 위 상태
- 드래그 판정 거리
- 쓰다듬기 누적 거리
- 자동 실행 등록 상태
- 각 기능 On/Off

Editor Inspector 또는 간단한 Development 전용 Overlay 중 프로젝트에 맞는 방식을 선택한다.

Release 빌드에서는 쉽게 제거하거나 비활성화할 수 있어야 한다.

---

## 14. 성능 및 안정성 기준

- `Update`에서 불필요한 LINQ 사용 금지
- 매 프레임 문자열 생성 금지
- 매 프레임 배열 또는 리스트 새로 생성 금지
- `Find`, `GetComponent`, `Camera.main`을 매 프레임 호출하지 않는다.
- 코루틴 또는 비동기 작업이 오브젝트 파괴 후 계속 실행되지 않게 한다.
- Animator Parameter 이름은 반복적인 문자열 조회보다 해시 사용을 고려한다.
- VRM Expression 접근은 설치된 API에 맞는 캐시 구조를 사용한다.
- 기능 비활성화 시 해당 업데이트가 실행되지 않게 한다.
- Null 참조가 발생하면 어떤 참조가 빠졌는지 명확한 오류를 출력한다.
- Win32 상태 변경 함수는 상태가 실제로 바뀔 때만 호출한다.
- 마우스 Hit Test를 위해 매 프레임 전체 씬을 탐색하지 않는다.
- Collider와 LayerMask 참조를 캐시한다.
- 멀티 모니터와 DPI 문제를 기록하고 우선 주 모니터에서 검증한다.
- Editor Play만으로 자동 실행 등록이 변경되지 않게 한다.

---

## 15. 완료 판정 체크리스트

### 모델

- [ ] `1456081260089804624.vrm`이 VRM 0.x 모델로 정상 Import된다.
- [ ] 생성된 Prefab이 정상 렌더링된다.
- [ ] Humanoid Avatar와 Animator 연결이 정상이다.
- [ ] 모델 크기와 바닥 위치가 맞다.
- [ ] BlendShape/Expression 데이터를 확인할 수 있다.
- [ ] Spring Bone과 Collider가 오류 없이 작동한다.
- [ ] 모델에 내장 Animation Clip이 없으므로 외부 Humanoid Clip을 사용한다.
- [ ] 해당 VRM을 개발·개인 테스트용으로만 사용한다.

### 애니메이션

- [ ] Base Idle이 자연스럽게 반복된다.
- [ ] 랜덤 Idle Variant 구조가 작동한다.
- [ ] 같은 Variant가 연속 재생되지 않는다.
- [ ] CrossFade 중 큰 튐이 없다.
- [ ] Root Motion으로 위치가 밀리지 않는다.

### 시선

- [ ] Mate가 테스트 타깃을 바라볼 수 있다.
- [ ] 시선 이동이 부드럽다.
- [ ] 머리가 비정상적으로 꺾이지 않는다.
- [ ] 주변 랜덤 시선이 동작한다.

### 얼굴

- [ ] 불규칙한 Blink가 작동한다.
- [ ] Double Blink가 낮은 확률로 작동한다.
- [ ] Neutral 표정과 Blink가 충돌하지 않는다.

### 미세 움직임

- [ ] 호흡 또는 작은 몸 흔들림이 있다.
- [ ] 움직임이 과장되지 않는다.
- [ ] 머리카락과 옷이 안정적으로 반응한다.

### Windows 데스크톱 창

- [ ] Windows 빌드에서 배경이 투명하다.
- [ ] 창 테두리가 제거되어 있다.
- [ ] 항상 위 표시를 켜고 끌 수 있다.
- [ ] Mate가 없는 영역은 클릭 통과된다.
- [ ] Mate 위에서는 클릭과 드래그 입력을 받을 수 있다.
- [ ] 클릭 통과 강제 해제 안전장치가 있다.

### 이동

- [ ] Mate가 화면 내부 목적지까지 걸어간다.
- [ ] Walk 애니메이션과 실제 이동이 심하게 어긋나지 않는다.
- [ ] 화면 경계 밖으로 나가지 않는다.
- [ ] 목적지 도착 후 Idle로 복귀한다.
- [ ] 상호작용 중 자동 이동이 중단된다.

### 사용자 상호작용

- [ ] 클릭과 드래그가 구분된다.
- [ ] Mate를 마우스로 끌 수 있다.
- [ ] 드래그 시작 시 위치가 튀지 않는다.
- [ ] 클릭 반응이 작동한다.
- [ ] 쓰다듬기 판정과 반응이 작동한다.
- [ ] 반응 종료 후 Idle로 복귀한다.

### Windows 자동 실행

- [ ] 사용자가 자동 실행을 켜고 끌 수 있다.
- [ ] 현재 등록 상태를 확인할 수 있다.
- [ ] Windows 로그인 후 한 번만 실행된다.
- [ ] Editor에서는 시작 프로그램을 변경하지 않는다.

### 구조

- [ ] 기능별 책임이 분리되어 있다.
- [ ] 주요 조정값이 Profile 또는 Inspector에 노출된다.
- [ ] 이후 Listening/Talking/Thinking 상태를 추가할 수 있다.
- [ ] Windows 기능과 Mate 애니메이션 계층이 분리되어 있다.
- [ ] 이후 Listening/Talking/Thinking 상태를 추가할 수 있다.
- [ ] 마이크·LLM·TTS 기능은 아직 구현하지 않았다.

---

## 16. Codex 작업 결과 보고 형식

작업 후 다음 형식으로 결과를 남긴다.

```markdown
# Phase 1 작업 결과

## 확인한 프로젝트 환경
- Unity 버전:
- Render Pipeline:
- UniVRM 버전:
- 사용한 VRM: `1456081260089804624.vrm`
- 확인된 VRM 규격: VRM 0.x / Meta 0.7
- Import 결과: Prefab / Avatar / Expression / Spring Bone
- 사용 가능한 Animation Clip:

## 생성 또는 수정한 파일
- 경로: 역할

## 구현된 기능
- [ ] VRM 표시
- [ ] Base Idle
- [ ] Random Idle Variant
- [ ] Look At
- [ ] Blink
- [ ] Micro Motion
- [ ] Spring Bone 확인
- [ ] 투명 창
- [ ] 클릭 통과
- [ ] 항상 위 표시
- [ ] 화면 안 걷기
- [ ] Mate 드래그
- [ ] 클릭 반응
- [ ] 쓰다듬기 반응
- [ ] Windows 자동 실행

## Inspector 연결 방법
1.
2.
3.

## 현재 부족한 에셋 또는 미완료 항목
-

## 테스트 결과
-

## 다음 단계에서 고려할 사항
-
```

---

## 17. Codex에 전달할 최종 실행 지시

아래 순서대로 진행한다.

1. 현재 Unity 프로젝트와 Windows 빌드 환경을 먼저 조사한다.
2. `1456081260089804624.vrm`의 VRM 0.x Import와 Mate 표시부터 시작한다.
3. Phase 1A 자연스러운 움직임을 먼저 독립적으로 완성한다.
4. Phase 1B 투명 창, 항상 위, 클릭 통과를 Windows 빌드에서 검증한다.
5. Phase 1C 걷기, 드래그, 클릭, 쓰다듬기 반응을 상태 전환과 연결한다.
6. Phase 1D 자동 실행을 추가하고 전체 통합 테스트를 진행한다.
7. 각 단계마다 컴파일 오류와 실행 오류를 해결한 후 다음 단계로 넘어간다.
8. Windows 전용 기능이 Mate 애니메이션 코드에 직접 섞이지 않게 한다.
9. 애니메이션 에셋이 부족하면 무단 추출하지 말고 슬롯과 제어 구조를 완성한다.
10. 현재 VRM은 개발·개인 테스트용으로만 취급한다.
11. 마이크, STT, LLM, TTS는 Phase 1에서 구현하지 않는다.
12. 작업 완료 후 `Phase 1 작업 결과` 형식으로 보고한다.

---

## 최종 한 줄 목표

> VRM Mate가 Windows 바탕화면 위에서 자연스럽게 호흡하고 움직이며, 화면 안을 걷고, 사용자가 클릭하거나 쓰다듬거나 끌 수 있는 살아 있는 데스크톱 캐릭터처럼 동작하게 만든다.
