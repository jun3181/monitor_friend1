# 가상속 Mate — Phase 2: 로컬 음성인식(STT) 구현 지시서

> 이 문서는 로컬 ChatGPT Codex가 기존 Unity 데스크톱 Mate 프로젝트에 **마이크 입력과 한국어 음성인식 기능**을 추가할 때 따라야 하는 기획·설계·작업 지시서다.
>
> Phase 2의 목적은 사용자의 음성을 텍스트로 변환하여, 다음 단계의 LLM 시스템이 사용할 수 있는 **확정된 사용자 발화 데이터**를 만드는 것이다.

---

## 0. Codex 작업 원칙

1. 구현 전에 현재 Unity 프로젝트의 구조, Unity 버전, 입력 시스템, Assembly Definition, 기존 Mate 상태 구조를 먼저 조사한다.
2. Phase 1 기능을 임의로 삭제하거나 대규모로 재구성하지 않는다.
3. 기존 프로젝트 규칙이 이 문서의 예시 구조와 다르면 기존 규칙을 우선하고, 필요한 부분만 맞춰 적용한다.
4. 음성인식 기능을 Mate 애니메이션, Windows 투명 창, 자동 이동 코드에 직접 결합하지 않는다.
5. 마이크 입력, 발화 구간 감지, STT 제공자, 결과 전달을 서로 교체 가능한 책임으로 분리한다.
6. 현재 단계에서는 외부 서버에 음성 데이터를 전송하지 않는다.
7. 현재 단계의 기본 STT는 **로컬 whisper.cpp 계열**을 사용한다.
8. 패키지 설치 전에 현재 Unity 버전 및 Windows 빌드와의 호환성을 공식 저장소에서 확인한다.
9. API나 패키지 사용법을 추측하지 말고, 설치된 버전의 실제 코드와 문서를 확인한다.
10. 각 작업 단계가 독립적으로 정상 동작하는 것을 확인한 뒤 다음 단계로 넘어간다.

---

# 1. Phase 2 목표

다음 파이프라인을 완성한다.

```text
Windows 마이크 장치
    ↓
Unity 마이크 입력
    ↓
PCM 오디오 순환 버퍼
    ↓
A 키 Push-to-Talk 세션
    ↓
음성 시작·종료 감지(VAD)
    ↓
로컬 한국어 음성인식
    ├─ 중간 결과(Partial)
    └─ 최종 결과(Final)
    ↓
SpeechRecognitionResult
    ↓
다른 시스템에 전달하는 인터페이스
```

사용자가 A 키를 누르고 말했을 때 최종적으로 다음과 같은 데이터가 생성되어야 한다.

```text
사용자 음성:
"오늘은 무엇을 하면 좋을까?"

최종 결과:
text = "오늘은 무엇을 하면 좋을까?"
language = "ko"
isFinal = true
```

Phase 2 완료 시 LLM은 아직 연결하지 않는다.  
확정 텍스트는 디버그 UI와 로그뿐 아니라 **Mate 위의 사용자 음성 말풍선**에도 표시한다.  
말풍선을 통해 사용자는 자신의 말이 실제로 어떤 텍스트로 인식되었는지 즉시 확인할 수 있어야 한다.  
이후 LLM 시스템은 같은 확정 결과 인터페이스를 구독한다.

---

# 2. 테스트 입력 방식

## 2.1 기본 조작

테스트 단계에서는 **A 키 Push-to-Talk** 방식을 사용한다.

```text
A 키 누름
    ↓
Listening 세션 시작
    ↓
A 키를 누르고 말함
    ↓
A 키를 놓음
    ↓
현재 발화를 강제로 마감
    ↓
Transcribing
    ↓
최종 텍스트 출력
```

기본 동작은 다음과 같다.

- `A Key Down`: 음성 입력 세션 시작
- `A Key Held`: 마이크 입력과 발화 감지 유지
- `A Key Up`: 남은 발화를 마감하고 최종 인식 요청
- 녹음된 유효 음성이 없으면 STT를 실행하지 않음
- A 키를 매우 짧게 눌렀다 놓은 경우 빈 결과를 생성하지 않음

## 2.2 중요한 제한

Phase 2의 A 키 테스트는 **Unity 애플리케이션에 키보드 포커스가 있을 때만** 동작해도 된다.

이번 단계에서는 다음을 구현하지 않는다.

- Windows 전역 키보드 후킹
- 다른 프로그램을 사용하는 중에도 A 키를 가로채는 기능
- 전역 단축키 등록
- 호출어 감지

A 키를 전역으로 가로채면 일반 타이핑을 방해할 수 있으므로, 추후 사용자 지정 단축키 또는 항상 듣기 모드를 설계할 때 별도로 판단한다.

## 2.3 키 설정

A 키를 코드에 여러 곳에서 직접 하드코딩하지 않는다.

다음 중 현재 프로젝트 입력 방식에 맞는 하나를 사용한다.

- Unity Input System의 Input Action
- 기존 프로젝트의 입력 추상화
- 초기 검증용 단일 KeyCode 설정 필드

테스트 키는 Inspector 또는 설정 데이터에서 변경 가능하게 한다.

---

# 3. Phase 2 구현 범위

이번 단계에서 구현한다.

- Windows 마이크 장치 목록 조회
- 사용할 마이크 선택
- 선택한 장치 저장 및 복원
- 마이크 입력 시작
- 마이크 입력 중지
- PCM 오디오 데이터 읽기
- 순환 버퍼 또는 안전한 스트림 버퍼
- 입력 샘플레이트 확인
- 필요 시 16kHz Mono 변환
- A 키 Push-to-Talk
- 말하기 시작 감지
- 말하기 종료 감지
- 발화 앞부분이 잘리지 않도록 Pre-roll 유지
- 한국어 로컬 음성인식
- 중간 인식 결과
- 최종 인식 결과
- 중간 결과와 최종 결과의 명확한 구분
- 음소거 기능
- `Listening` 상태
- `Transcribing` 상태
- 에러 상태와 사용자 안내
- 최종 텍스트를 다른 시스템에 전달하는 인터페이스
- 디버그 상태 및 결과 UI
- Mate 위 사용자 음성 말풍선
- Partial 텍스트의 실시간 말풍선 갱신
- Final 텍스트의 확정 표시
- 말풍선 자동 숨김과 새 발화 초기화
- Windows Player 빌드 검증

---

# 4. Phase 2에서 구현하지 않는 것

다음 기능은 이번 단계에서 제외한다.

- LLM 연결
- Mate 답변 생성
- TTS 음성 합성
- 립싱크
- Mate의 말이 마이크로 재입력되는 Echo Cancellation
- 호출어 또는 Wake Word
- 항상 듣기 모드
- Windows 전역 단축키
- 클라우드 STT API
- 대화 기록 저장
- 장기 기억
- 감정 분석
- 문장 의도 분류
- 화자 분리
- 여러 사람 목소리 구분
- 고급 노이즈 제거
- 음성 파일 자동 저장
- 모델 자동 다운로드 UI
- LLM 답변용 Mate 대사 말풍선
- 대화 기록 창
- 여러 말풍선이 누적되는 채팅 UI

단, 이후 TTS와 LLM을 연결하기 쉽도록 다음 확장 지점을 남긴다.

```text
SetCaptureSuspended(bool suspended)
ISpeechRecognitionResultSource
OnFinalResult
OnPartialResult
OnStateChanged
```

---

# 5. 권장 기술 방향

## 5.1 Unity 마이크 입력

Unity의 `Microphone` API를 기본 입력 계층으로 사용한다.

필요 기능:

- 장치 목록 조회
- 특정 장치로 녹음 시작
- 현재 녹음 위치 조회
- 순환 녹음
- 녹음 중 여부 확인
- 녹음 종료

Unity `AudioClip` 전체를 매번 복사하지 말고, 이전 읽기 위치와 현재 `Microphone.GetPosition` 차이를 계산하여 새 샘플만 읽는다.

공식 참고:

- Unity Microphone API  
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Microphone.html

## 5.2 로컬 STT

Phase 2 기본 후보는 `whisper.unity`를 통한 `whisper.cpp` 로컬 실행이다.

공식 참고:

- whisper.unity  
  https://github.com/Macoron/whisper.unity
- whisper.cpp  
  https://github.com/ggml-org/whisper.cpp
- whisper.cpp microphone stream example  
  https://github.com/ggml-org/whisper.cpp/tree/master/examples/stream

선택 이유:

- 음성을 외부 서버로 보내지 않음
- API 키가 필요하지 않음
- Unity 애플리케이션에 포함하기 쉬움
- 이후 데스크톱 Mate 단일 프로그램 형태로 배포하기 유리함

주의:

- 설치 시점의 최신 버전이 현재 Unity 버전과 호환되는지 검증한다.
- Windows x64 네이티브 라이브러리 Import Setting을 확인한다.
- Editor에서는 동작하지만 Windows Player에서 DLL 로드가 실패할 수 있으므로 반드시 빌드 테스트한다.
- 패키지가 현재 프로젝트에서 작동하지 않으면 억지로 내부 코드를 크게 수정하지 말고 원인을 기록한다.
- 대체 구현은 별도 로컬 `faster-whisper` 프로세스지만, Phase 2 기본안이 실패했을 때만 검토한다.

## 5.3 Whisper 모델

한국어를 인식해야 하므로 영어 전용 `.en` 모델을 사용하지 않는다.

첫 테스트 권장 순서:

```text
1차: multilingual base 계열
2차: 한국어 정확도가 부족하면 multilingual small 계열
```

원칙:

- 모델 경로를 코드에 절대 경로로 하드코딩하지 않는다.
- 모델 파일의 존재 여부를 시작 시 검사한다.
- 모델이 없으면 명확한 오류와 예상 경로를 출력한다.
- 개발용 모델은 프로젝트 정책에 따라 `StreamingAssets` 또는 외부 모델 폴더에서 로드한다.
- 큰 모델을 Git에 바로 커밋하지 않는다.
- 모델 파일의 라이선스와 배포 크기를 기록한다.

예시 경로:

```text
Assets/
└─ StreamingAssets/
   └─ Models/
      └─ Whisper/
         └─ ggml-base.bin
```

실제 패키지 API가 요구하는 경로가 다르면 설치된 버전에 맞춘다.

---

# 6. 음성 처리 기본 규격

STT에 전달하는 최종 오디오는 다음 형식을 목표로 한다.

```text
Sample Rate: 16000 Hz
Channel: Mono
Sample Type: Float PCM 또는 provider 요구 형식
```

마이크 장치가 44.1kHz 또는 48kHz로 입력될 수 있으므로 입력 사양을 확인하고 필요한 경우 리샘플링한다.

규칙:

- Stereo 입력은 Mono로 다운믹스한다.
- 리샘플링은 한 책임에서만 수행한다.
- 프레임마다 큰 배열을 새로 할당하지 않는다.
- STT 실행 중에도 마이크 입력 버퍼가 유실되지 않아야 한다.
- 빈 오디오 또는 지나치게 짧은 오디오는 STT에 보내지 않는다.
- 최대 발화 길이를 설정하여 무제한 메모리 증가를 막는다.

초기 권장 설정값은 조정 가능한 설정으로 둔다.

```text
Target Sample Rate: 16000 Hz
Pre-roll: 250~400 ms
Post-roll: 150~300 ms
Minimum Speech Duration: 250~400 ms
End Silence: 600~1000 ms
Maximum Utterance: 20~30 sec
Partial Update Interval: 800~1500 ms
```

수치는 고정 정답이 아니며 실제 마이크와 환경에서 조정한다.

---

# 7. 권장 폴더 구조

현재 프로젝트 구조를 먼저 확인하고 가능한 범위에서 다음처럼 분리한다.

```text
Assets/
└─ Mate/
   ├─ Scripts/
   │  └─ Voice/
   │     ├─ Core/
   │     │  ├─ SpeechRecognitionState
   │     │  ├─ SpeechRecognitionResult
   │     │  └─ SpeechRecognitionCoordinator
   │     │
   │     ├─ Input/
   │     │  ├─ MicrophoneDeviceService
   │     │  ├─ MicrophoneCapture
   │     │  ├─ AudioRingBuffer
   │     │  └─ AudioResampler
   │     │
   │     ├─ Detection/
   │     │  ├─ IVoiceActivityDetector
   │     │  └─ EnergyVoiceActivityDetector
   │     │
   │     ├─ Recognition/
   │     │  ├─ ISpeechToTextProvider
   │     │  └─ WhisperSpeechToTextProvider
   │     │
   │     ├─ Interaction/
   │     │  └─ PushToTalkController
   │     │
   │     ├─ Presentation/
   │     │  ├─ SpeechRecognitionDebugView
   │     │  ├─ UserSpeechBubbleView
   │     │  └─ MateScreenAnchor
   │     │
   │     └─ Configuration/
   │        └─ SpeechRecognitionProfile
   │
   ├─ Settings/
   │  └─ SpeechRecognition/
   │
   └─ Tests/
      └─ Voice/

Assets/
└─ StreamingAssets/
   └─ Models/
      └─ Whisper/
```

폴더명은 예시다. 기존 프로젝트의 네이밍과 Assembly Definition 규칙을 우선한다.

---

# 8. 핵심 데이터 구조

## 8.1 `SpeechRecognitionState`

최소한 다음 상태를 구분한다.

```text
Uninitialized
Initializing
Ready
Listening
SpeechDetected
Transcribing
Muted
Suspended
Error
```

필수 상태:

- `Listening`: A 키 입력 세션이 활성화되어 마이크를 듣는 상태
- `Transcribing`: 수집된 발화를 STT 모델이 처리하는 상태

상태 의미:

### `Ready`

- 마이크와 STT 모델이 준비됨
- 현재 Push-to-Talk 입력을 기다리는 중

### `Listening`

- A 키가 눌려 있음
- 마이크 샘플을 수집 중
- 아직 음성이 감지되지 않았을 수도 있음

### `SpeechDetected`

- VAD가 실제 음성을 감지함
- 현재 발화 버퍼에 샘플을 추가 중

### `Transcribing`

- 하나의 발화가 종료됨
- STT 추론 중
- 동시에 같은 발화에 대한 중복 최종 요청을 생성하면 안 됨

### `Muted`

- 사용자 음소거가 활성화됨
- A 키를 눌러도 녹음을 시작하지 않음

### `Suspended`

- 향후 Mate가 TTS로 말하는 동안 시스템이 입력을 임시 차단하기 위한 상태
- 사용자 음소거와 구분한다

### `Error`

- 마이크 없음
- 마이크 권한 거부
- 모델 파일 없음
- 네이티브 라이브러리 로드 실패
- STT 초기화 실패
- 기타 복구 불가능한 오류

---

## 8.2 `SpeechRecognitionResult`

다른 시스템은 whisper.unity 내부 타입을 직접 받지 않는다.

공통 결과 데이터로 변환한다.

권장 필드:

```text
SessionId
UtteranceId
Text
Language
IsFinal
StartTime
EndTime
AudioDuration
ProviderName
Confidence 또는 품질 정보(제공 가능한 경우)
Error 정보(실패 결과를 같은 타입으로 표현할 경우)
```

필수 필드:

```text
Text
Language
IsFinal
UtteranceId
```

중간 결과:

```text
Text = "오늘은 무엇을"
IsFinal = false
```

최종 결과:

```text
Text = "오늘은 무엇을 하면 좋을까?"
IsFinal = true
```

중간 결과는 UI 표시용 임시 데이터이며 대화 기록이나 LLM 입력으로 사용하지 않는다.  
다음 단계의 LLM은 `IsFinal == true`인 결과만 받는다.

---

# 9. 컴포넌트 책임

## 9.1 `MicrophoneDeviceService`

책임:

- `Microphone.devices` 기반 장치 목록 제공
- 장치 이름을 UI에 전달
- 현재 선택 장치 관리
- 이전 선택 장치 복원
- 저장된 장치가 사라졌을 때 기본 장치로 대체
- 장치 변경 이벤트 제공

규칙:

- 장치 인덱스만 저장하지 않고 가능한 경우 장치 이름을 저장한다.
- 장치 목록이 비어 있으면 `Error` 상태와 사용자 안내를 제공한다.
- 실행 중 장치 연결 해제 상황을 안전하게 처리한다.

## 9.2 `MicrophoneCapture`

책임:

- 선택 장치 녹음 시작
- 선택 장치 녹음 중지
- 현재 캡처 상태 제공
- 새로 들어온 PCM 샘플만 추출
- Microphone 순환 버퍼 Wrap-around 처리
- 입력 채널과 샘플레이트 정보 제공
- 장치 변경 시 안전한 재시작

하지 않는 일:

- STT 실행
- VAD 판단
- UI 변경
- Mate 애니메이션 직접 제어

## 9.3 `AudioRingBuffer`

책임:

- 일정 시간 분량의 최신 PCM 유지
- 프레임 간 입력 샘플 유실 방지
- Pre-roll 데이터 제공
- 읽기·쓰기 위치 관리
- 최대 크기 제한
- 불필요한 메모리 할당 최소화

필수 검증:

- Microphone 버퍼가 끝에서 처음으로 돌아갈 때 샘플 순서가 깨지지 않아야 한다.
- 프레임 드랍이 있어도 읽지 않은 구간을 가능한 범위에서 회수해야 한다.
- Overflow 발생 시 정책과 로그가 있어야 한다.

## 9.4 `AudioResampler`

책임:

- 입력 오디오를 provider가 요구하는 샘플레이트로 변환
- 여러 채널을 Mono로 변환
- 입력과 출력 샘플 수 계산
- 출력 클리핑 방지

초기 구현은 정확성과 안정성을 우선한다.  
성능 문제가 확인되기 전에는 과도한 최적화를 하지 않는다.

## 9.5 `IVoiceActivityDetector`

오디오 샘플이 음성인지 침묵인지 판정한다.

Phase 2 기본 구현은 **음량 또는 RMS 기반 단순 VAD**다.

입력:

```text
PCM Frame
Sample Rate
```

출력:

```text
IsSpeech
Energy
Threshold
```

VAD 설정:

- 음성 시작 임계값
- 음성 종료 임계값
- 최소 음성 지속 시간
- 종료 침묵 시간
- 노이즈 플로어
- Pre-roll
- Post-roll

초기 VAD가 실제 환경에서 지나치게 불안정할 경우에만 Silero VAD 같은 모델 기반 구현을 후속으로 검토한다.

## 9.6 `PushToTalkController`

책임:

- A 키 Down/Held/Up 처리
- Push-to-Talk 세션 시작과 종료 요청
- 음소거 상태 확인
- 키 입력 중복 방지
- 앱 포커스 손실 시 안전한 세션 종료

앱이 포커스를 잃거나 비활성화되었는데 A Key Up을 받지 못하는 상황을 처리해야 한다.

권장 정책:

```text
OnApplicationFocus(false)
또는 OnApplicationPause(true)
    ↓
활성 Push-to-Talk 세션 강제 종료
    ↓
유효 발화가 있으면 마감
    ↓
없으면 폐기
```

## 9.7 `ISpeechToTextProvider`

음성인식 엔진을 추상화한다.

필요 역할:

```text
Initialize
IsReady
TranscribePartial
TranscribeFinal
Cancel
Dispose
```

정확한 메서드 형태는 프로젝트의 비동기 처리 규칙에 맞춘다.

중요:

- 상위 시스템이 whisper.unity 구체 클래스에 의존하지 않는다.
- 동시에 여러 최종 추론을 실행할지 여부를 명확하게 제한한다.
- 모델 초기화는 앱 시작 또는 첫 사용 전에 한 번만 수행한다.
- 추론 취소와 앱 종료를 안전하게 처리한다.
- Unity 메인 스레드에서만 가능한 API 호출과 백그라운드 추론을 구분한다.

## 9.8 `WhisperSpeechToTextProvider`

책임:

- whisper 모델 로드
- 한국어 인식 설정
- 오디오 입력 형식 검증
- 중간 결과 생성
- 최종 결과 생성
- provider 결과를 공통 `SpeechRecognitionResult`로 변환
- 네이티브 오류를 프로젝트 오류 형태로 변환

한국어 설정:

- 언어를 `ko`로 명시하거나 자동 감지와 비교 가능하게 한다.
- Phase 2 기본값은 한국어 고정이다.
- 번역 모드가 아니라 transcription 모드로 사용한다.
- 불필요한 타임스탬프 생성은 초기에는 비활성화할 수 있다.

## 9.9 `SpeechRecognitionCoordinator`

전체 흐름을 조정하지만 각 세부 작업을 직접 구현하지 않는다.

책임:

- 초기화 순서 관리
- 상태 전환
- Push-to-Talk 세션 관리
- VAD 결과에 따른 발화 시작·종료
- Partial 요청 스케줄링
- Final 요청 생성
- 오래된 Partial 결과 무시
- 결과 이벤트 전달
- 음소거와 Suspend 처리
- 오류 복구

---

# 10. 발화 구간 처리

## 10.1 Push-to-Talk와 VAD의 관계

A 키는 녹음 가능 시간을 정하고, VAD는 실제 발화 구간을 정한다.

```text
A Down
    ↓
Listening 시작
    ↓
Pre-roll 순환 버퍼 유지
    ↓
VAD Speech Start
    ↓
Pre-roll + 현재 음성 샘플을 Utterance Buffer에 저장
    ↓
VAD Speech End 또는 A Up
    ↓
Post-roll 추가
    ↓
최종 발화 완성
    ↓
Transcribing
```

## 10.2 음성 시작

음성 시작 판정 시:

- 시작 직전 Pre-roll 샘플을 함께 포함한다.
- 첫 음절이 잘리지 않아야 한다.
- 짧은 키보드 소리 하나만으로 발화가 시작되지 않도록 최소 지속 시간을 둔다.

## 10.3 음성 종료

다음 중 하나면 현재 발화를 마감한다.

- 음성 이후 설정된 침묵 시간이 경과함
- 사용자가 A 키를 놓음
- 최대 발화 길이에 도달함
- 앱이 포커스를 잃음
- 사용자가 음소거함
- 시스템이 입력을 Suspend함

정책:

- A 키를 놓으면 종료 침묵 시간을 기다리지 않고 현재 유효 발화를 마감한다.
- 발화가 너무 짧으면 폐기한다.
- 최대 길이에 도달하면 현재 발화를 마감하고 경고를 남긴다.

## 10.4 A 키를 계속 누른 상태에서 문장이 끝난 경우

초기 권장 동작:

```text
A Held
    ↓
VAD가 한 문장 종료 감지
    ↓
해당 문장을 Final Transcription
    ↓
A가 계속 눌려 있으면 다시 Listening
    ↓
다음 발화를 새 UtteranceId로 수집
```

따라서 한 번 A 키를 누른 상태에서 여러 문장을 말하면 문장별로 복수의 최종 결과가 나올 수 있다.

구현 복잡도가 과도해지면 1차 버전에서는 A Up 시 한 번만 Final 처리하고, VAD는 앞뒤 침묵 제거에만 사용해도 된다.  
단, 어떤 정책을 채택했는지 작업 결과에 명확히 기록한다.

---

# 11. 중간 결과와 최종 결과

## 11.1 중간 결과(Partial)

목적:

- 사용자가 말한 내용이 인식되고 있는지 확인
- STT 디버깅
- 미래의 실시간 자막 UI 확장

조건:

- `IsFinal = false`
- 같은 `UtteranceId`를 사용
- 새 결과가 오면 이전 Partial 표시를 교체
- LLM으로 전달하지 않음
- 저장하지 않음
- 늦게 도착한 오래된 Partial은 무시

초기 권장 방식:

- 발화 중 설정된 간격마다 지금까지의 발화 복사본을 추론
- Partial 추론이 진행 중이면 새 요청을 무한히 큐에 쌓지 않음
- 최신 요청 하나만 유지하거나 이전 요청 결과를 폐기
- Partial 때문에 Final 추론이 지나치게 지연되지 않게 함

## 11.2 최종 결과(Final)

조건:

- `IsFinal = true`
- 발화 종료 후 한 번만 발행
- 앞뒤 공백 제거
- 빈 문자열이면 발행하지 않음
- 동일 `UtteranceId`의 중복 Final을 방지
- 다음 단계의 LLM이 받을 공식 사용자 입력

Final 발생 순서:

```text
발화 마감
    ↓
Transcribing
    ↓
최종 STT 결과 수신
    ↓
텍스트 정리
    ↓
OnFinalResult 발행
    ↓
Ready 또는 Listening 복귀
```

---

# 12. 음소거 기능

## 12.1 사용자 음소거

다음 공개 기능을 제공한다.

```text
SetMuted(true)
SetMuted(false)
ToggleMuted()
IsMuted
```

음소거 시:

- 상태를 `Muted`로 전환
- 활성 Push-to-Talk 세션 종료
- 현재 미완성 발화는 기본적으로 폐기
- A 키 입력 무시
- Partial 요청 취소 또는 결과 무시
- 마이크 캡처를 완전히 종료할지 계속 유지할지는 설정 가능

초기 권장안:

- 음소거 시 `Microphone.End`로 실제 장치 캡처를 중지한다.
- 음소거 해제 시 선택 장치로 다시 초기화한다.
- 재초기화 시간이 사용자 경험에 문제가 되면 이후 방식 변경을 검토한다.

## 12.2 시스템 Suspend

사용자 음소거와 별도로 다음 확장 기능을 둔다.

```text
SetCaptureSuspended(bool suspended)
```

목적:

- Phase 3 이후 Mate가 TTS로 말할 때 자신의 목소리를 다시 듣지 않게 함
- 설정 변경 또는 장치 전환 중 일시 정지
- 앱 비활성화 시 안전한 캡처 차단

Suspend는 사용자 음소거 설정을 변경하지 않는다.

---

# 13. 마이크 장치 선택 UI

Phase 2에서는 개발용 UI로 다음 정보를 제공한다.

```text
Microphone Device Dropdown
Refresh Devices Button
Selected Device
Input Sample Rate
Input Channel Count
Microphone Running
Mute Toggle
Current State
Current RMS / Energy
VAD Speech 여부
Partial Text
Final Text
Last Error
Model Path
Model Loaded
Inference Time
```

필수 기능:

- 장치 목록 새로고침
- 장치 선택
- 선택 장치 저장
- 선택 장치 재연결
- 음소거 On/Off
- A 키 사용 안내
- 현재 상태 확인
- Partial과 Final을 서로 다른 영역에 표시

UI는 개발 및 검증 목적이다.  
최종 제품 UI 디자인은 Phase 2 범위가 아니다.

---

# 14. 사용자 음성 말풍선

## 15.1 목적

사용자가 말한 내용이 STT에 의해 올바르게 인식되는지 즉시 확인할 수 있도록, Mate 주변에 말풍선을 표시한다.

```text
A 키를 누르고 말함
    ↓
Partial Result
    ↓
Mate 위 말풍선에 임시 텍스트 표시
    ↓
A 키를 놓거나 발화 종료
    ↓
Final Result
    ↓
같은 말풍선을 확정 텍스트로 교체
    ↓
일정 시간 후 숨김
```

말풍선은 Phase 2에서 **사용자 발화 확인용**이다.  
Mate가 LLM을 통해 답변하는 말풍선은 Phase 3 이후 별도의 출력 종류로 설계한다.

## 15.2 표시 규칙

### Listening 시작

- 새 Push-to-Talk 세션이 시작되면 이전 Partial 텍스트를 지운다.
- 필요하면 `듣는 중...` 또는 마이크 상태 표시를 보여준다.
- 아직 실제 음성이 감지되지 않았다면 빈 말풍선을 계속 띄우지 않아도 된다.

### Partial 결과

- `IsFinal == false`인 최신 텍스트를 말풍선에 표시한다.
- 같은 `UtteranceId`의 새 Partial이 오면 기존 텍스트를 교체한다.
- Partial 결과를 말풍선 목록에 누적하지 않는다.
- 늦게 도착한 이전 발화의 Partial은 표시하지 않는다.
- Partial이라는 사실을 사용자가 구분할 수 있도록 임시 표시 상태를 둔다.

예시:

```text
Partial:
"오늘은 무엇을"
```

### Final 결과

- `IsFinal == true`인 결과가 오면 같은 말풍선을 최종 문장으로 교체한다.
- 앞뒤 공백을 제거한 텍스트를 표시한다.
- 빈 Final은 말풍선에 표시하지 않는다.
- Final 결과는 설정된 시간 동안 유지한 뒤 사라진다.
- 새 발화가 시작되면 이전 Final을 즉시 숨길지 유지할지 정책을 명확히 한다.

Phase 2 기본안:

```text
Final 표시 유지 시간: 4초
새 발화 시작: 이전 Final 즉시 숨김
숨김 방식: 짧은 Fade Out 또는 즉시 숨김
```

표시 시간은 Inspector 또는 설정 데이터에서 조정 가능하게 한다.

## 15.3 위치

말풍선은 Mate의 머리 위 또는 상단 주변에 표시한다.

권장 구조:

```text
Mate Head Anchor (3D Transform)
    ↓ WorldToScreenPoint
MateScreenAnchor
    ↓
UserSpeechBubbleView (Screen Space Canvas)
```

권장 이유:

- Mate가 걷거나 드래그되어도 따라갈 수 있음
- 화면 해상도와 Mate 크기에 관계없이 글자 크기를 읽기 쉽게 유지 가능
- World Space Canvas보다 UI 크기 조정이 단순함

필수 처리:

- Mate의 Head Bone 또는 전용 Bubble Anchor Transform을 기준점으로 사용한다.
- 머리 위에 일정 픽셀 오프셋을 둔다.
- 화면 가장자리에서는 말풍선이 잘리지 않도록 Clamp한다.
- Mate가 화면 밖이거나 비활성화되면 말풍선을 숨긴다.
- 카메라 뒤쪽에 있는 기준점은 표시하지 않는다.
- Mate를 드래그할 때도 위치가 자연스럽게 따라가야 한다.

## 15.4 크기와 텍스트

- TextMeshPro 사용을 우선한다.
- 긴 한국어 문장은 자동 줄바꿈한다.
- 최소 너비와 최대 너비를 둔다.
- 내용에 따라 높이가 자동으로 늘어나게 한다.
- 너무 긴 문장은 화면 전체를 가리지 않도록 최대 높이 또는 최대 표시 글자 수를 둔다.
- Phase 2 테스트에서는 최소 2~4줄을 읽을 수 있어야 한다.
- 텍스트가 길어 잘렸다면 디버그 UI에서는 전체 문장을 확인할 수 있어야 한다.

초기 권장값:

```text
최대 너비: 화면 너비의 30~40%
최소 표시 시간: 1.5초
기본 Final 표시 시간: 4초
문자 수에 따른 추가 표시 시간: 선택
최대 표시 시간: 8초
```

## 15.5 데스크톱 클릭 통과와의 관계

Phase 1의 투명 창 및 클릭 통과 기능을 방해하지 않아야 한다.

원칙:

- 말풍선은 기본적으로 클릭 가능한 UI가 아니다.
- Text와 배경 Graphic의 `Raycast Target`을 끈다.
- `CanvasGroup.blocksRaycasts`를 비활성화한다.
- 말풍선이 보인다는 이유만으로 Mate 외부 영역 전체가 클릭을 가로채면 안 된다.
- `DesktopInputHitTest`가 Mate Collider만 입력 대상으로 판단하는 구조라면 말풍선은 Hit Test 대상에서 제외한다.
- 알파 기반 Windows Hit Test를 사용한다면, 말풍선 픽셀 때문에 바탕화면 클릭이 막히는지 반드시 Windows Player에서 확인한다.
- 문제가 생기면 말풍선 표시 영역은 시각적으로 보이되 입력 통과되도록 Windows 창 입력 정책을 분리한다.

## 15.6 책임 분리

### `UserSpeechBubbleView`

책임:

- Partial 텍스트 표시
- Final 텍스트 표시
- 표시 상태 변경
- 자동 숨김
- Fade In/Out
- 텍스트 레이아웃 갱신

하지 않는 일:

- 마이크 녹음
- VAD
- Whisper 호출
- 결과 텍스트 수정
- LLM 호출
- Mate Animator 직접 제어

### `MateScreenAnchor`

책임:

- Mate Head Anchor의 화면 좌표 계산
- 말풍선 위치 갱신
- 화면 경계 Clamp
- Mate가 보이지 않을 때 숨김 요청

### 연결 방향

```text
SpeechRecognitionCoordinator
    ↓ ISpeechRecognitionResultSource
UserSpeechBubblePresenter 또는 UserSpeechBubbleView
    ↓
Partial / Final 표시

Mate Head Anchor
    ↓
MateScreenAnchor
    ↓
말풍선 위치
```

`UserSpeechBubbleView`는 whisper.unity의 구체 타입을 참조하지 않는다.

## 15.7 표시 상태

권장 말풍선 상태:

```text
Hidden
Listening
Partial
Final
Error
```

### `Hidden`

- 표시하지 않음

### `Listening`

- 선택적으로 `듣는 중...` 표시
- 실제 SpeechDetected 전에는 숨겨도 됨

### `Partial`

- 현재 중간 인식 텍스트 표시
- 다음 Partial로 교체

### `Final`

- 확정 문장 표시
- 일정 시간 후 숨김

### `Error`

- 개발 빌드에서는 간단한 실패 문구를 표시할 수 있음
- 상세 오류는 디버그 UI와 로그에서 확인
- 제품 빌드에서는 오류 종류에 따라 말풍선 표시 여부를 설정

---

# 15. Mate 상태 연결

기존 Mate 상태 시스템이 있다면 다음 상태를 외부 요청으로 연결한다.

```text
Ready
    → Mate 기본 Idle

Listening / SpeechDetected
    → Mate가 사용자 쪽을 바라봄
    → Listening 표시 또는 간단한 반응

Transcribing
    → 생각하는 자세 또는 대기 표현

Final Result
    → Phase 2에서는 Idle 복귀
```

중요:

- 음성인식 시스템이 Animator를 직접 조작하지 않는다.
- `SpeechRecognitionStateChanged` 같은 이벤트를 Mate 표현 계층이 구독한다.
- 음성인식 실패가 Mate의 전체 상태 시스템을 멈추게 하지 않는다.
- 애니메이션 에셋이 없다면 이벤트 연결 지점만 구현하고 기존 Idle을 유지한다.

---

# 16. 다른 시스템에 전달하는 인터페이스

Phase 3 LLM 연결을 위해 결과 소스를 제공한다.

개념적 인터페이스:

```text
ISpeechRecognitionResultSource
    ├─ CurrentState
    ├─ IsMuted
    ├─ OnPartialResult
    ├─ OnFinalResult
    ├─ OnStateChanged
    └─ OnError
```

의존성 방향:

```text
LLM Conversation System
        ↓ 구독
ISpeechRecognitionResultSource
        ↑ 구현
SpeechRecognitionCoordinator
```

금지:

```text
SpeechRecognitionCoordinator
    → 특정 LLM 클래스 직접 호출
```

Phase 2에서는 Final 결과를 다음 두 곳에만 보낸다.

1. 디버그 UI
2. 공통 결과 이벤트

---

# 17. 오류 처리

다음 오류를 구분하여 표시한다.

- 마이크 장치가 없음
- 저장된 마이크 장치가 사라짐
- 마이크 시작 실패
- 마이크 권한 거부
- Microphone position이 진행되지 않음
- 오디오 버퍼 Overflow
- 모델 파일 없음
- 모델 로드 실패
- 네이티브 DLL 로드 실패
- 지원하지 않는 플랫폼 또는 아키텍처
- STT 추론 실패
- STT 추론 취소
- 잘못된 샘플레이트
- 빈 음성
- 최대 발화 시간 초과
- 앱 종료 중 작업 정리 실패

오류 메시지에는 가능한 경우 다음을 포함한다.

```text
무엇이 실패했는가
어떤 장치 또는 파일이 대상이었는가
사용자가 무엇을 확인해야 하는가
내부 Exception 또는 Error Code
```

오류가 발생해도 Phase 1의 Mate 움직임과 데스크톱 기능은 가능한 범위에서 계속 동작해야 한다.

---

# 18. 스레드와 비동기 처리

- STT 추론으로 Unity 메인 스레드를 장시간 막지 않는다.
- 프로젝트가 UniTask를 사용하고 있다면 기존 비동기 규칙을 따른다.
- 그렇지 않으면 표준 Task 또는 패키지 제공 비동기 API를 검토한다.
- Unity Object와 UI 갱신은 메인 스레드에서 수행한다.
- 앱 종료 시 진행 중인 추론을 취소하고 네이티브 리소스를 정리한다.
- Partial과 Final 요청의 경쟁 상태를 방지한다.
- 이전 세션 결과가 새 세션 UI를 덮어쓰지 않도록 `SessionId`, `UtteranceId`를 검증한다.
- 인식 요청이 밀리지 않도록 동시 추론 수를 제한한다.

초기 권장 정책:

```text
Final 추론 우선
Partial 추론은 최대 1개
새 Partial이 필요할 때 이전 요청이 실행 중이면 큐를 무한 증가시키지 않음
```

---

# 19. 개인정보와 파일 저장

Phase 2 기본 정책:

- 음성을 외부 서버로 보내지 않는다.
- 원본 음성을 디스크에 저장하지 않는다.
- 인식 텍스트를 영구 저장하지 않는다.
- 로그에는 사용자가 말한 전체 텍스트를 남길지 개발 설정으로 제어한다.
- 오류 재현용 WAV 저장 기능이 필요하면 명시적인 개발 옵션으로만 제공한다.
- 개발용 WAV 파일은 Git에 커밋하지 않는다.

---

# 20. 설정 데이터

`SpeechRecognitionProfile` 또는 기존 설정 체계에 다음 값을 둔다.

```text
Push-to-Talk Key
Preferred Microphone Device
Target Sample Rate
Requested Microphone Sample Rate
Microphone Loop Length
Pre-roll Duration
Post-roll Duration
Minimum Speech Duration
End Silence Duration
Maximum Utterance Duration
VAD Start Threshold
VAD End Threshold
Partial Enabled
Partial Update Interval
Language
Model Path
Mute On Start
Debug Logging
Save Debug Audio
```

런타임 조정이 필요한 값은 Inspector 또는 개발 UI에서 변경 가능하게 한다.

---

# 21. 구현 작업 순서

## Phase 2A — 프로젝트 조사와 입력 확인

### Step 1. 현재 프로젝트 조사

확인하고 기록한다.

- Unity 버전
- Windows 빌드 백엔드
- 기존 Input System 사용 여부
- 기존 비동기 라이브러리
- 기존 이벤트 또는 메시지 시스템
- Mate 상태 관리 방식
- Assembly Definition
- 기존 설정 저장 방식
- 기존 UI 프레임워크
- `whisper.unity` 또는 STT 관련 패키지 존재 여부
- 마이크 관련 기존 코드 존재 여부

### Step 2. 마이크 장치 목록과 선택

완료 조건:

- Windows 마이크 장치 목록이 표시된다.
- 장치 새로고침이 가능하다.
- 장치를 선택할 수 있다.
- 선택 장치가 저장된다.
- 저장된 장치가 없을 때 안전한 기본값을 사용한다.
- 장치가 없으면 명확한 오류가 보인다.

### Step 3. PCM 입력과 순환 버퍼

완료 조건:

- 선택 장치에서 녹음을 시작하고 중지할 수 있다.
- 새 PCM 샘플만 읽는다.
- Microphone 버퍼 Wrap-around가 정상 처리된다.
- 입력 에너지를 디버그 UI에서 확인할 수 있다.
- 프레임마다 큰 GC Allocation이 반복되지 않는다.
- 장시간 실행해도 버퍼 크기가 무한 증가하지 않는다.

---

## Phase 2B — A 키 Push-to-Talk와 VAD

### Step 4. A 키 세션

완료 조건:

- A Down 시 Listening이 시작된다.
- A Held 동안 입력이 유지된다.
- A Up 시 세션이 종료된다.
- 앱 포커스 손실 시 세션이 안전하게 종료된다.
- 음소거 중에는 세션이 시작되지 않는다.
- 매우 짧은 입력은 빈 결과를 만들지 않는다.

### Step 5. 음성 시작·종료 감지

완료 조건:

- 현재 RMS 또는 에너지를 확인할 수 있다.
- 음성이 시작될 때 `SpeechDetected`가 된다.
- 침묵이 유지되면 발화가 종료된다.
- Pre-roll로 첫 음절이 심하게 잘리지 않는다.
- 키보드 클릭 하나만으로 발화가 쉽게 시작되지 않는다.
- A Up은 현재 발화를 즉시 마감한다.
- 최대 발화 길이 제한이 작동한다.

---

## Phase 2C — 로컬 한국어 STT

### Step 6. STT 패키지와 모델 초기화

완료 조건:

- 로컬 Whisper provider가 초기화된다.
- 한국어 multilingual 모델이 로드된다.
- 모델 경로 오류가 명확하게 표시된다.
- 네이티브 라이브러리 오류가 명확하게 표시된다.
- Editor에서 초기화된다.
- Windows x64 Player 빌드에서 초기화된다.

### Step 7. 최종 인식

완료 조건:

- A 키를 누르고 한국어로 말한 뒤 놓으면 Final 결과가 생성된다.
- Final 결과는 `IsFinal = true`다.
- 빈 텍스트를 발행하지 않는다.
- 한 발화당 Final이 중복 발행되지 않는다.
- 결과가 공통 `SpeechRecognitionResult`로 변환된다.
- 디버그 UI에서 최종 텍스트와 추론 시간을 확인할 수 있다.

### Step 8. 중간 인식

완료 조건:

- 발화 중 Partial 결과가 생성된다.
- Partial은 `IsFinal = false`다.
- 같은 발화의 Partial이 UI에서 갱신된다.
- 오래된 Partial이 새 발화를 덮어쓰지 않는다.
- Partial 요청이 무한히 쌓이지 않는다.
- Final 결과가 Partial보다 우선한다.

---

## Phase 2D — 음소거, 상태, 인터페이스

### Step 9. 음소거

완료 조건:

- 음소거를 켜고 끌 수 있다.
- 음소거 상태가 UI에 표시된다.
- 음소거 중 A 키가 무시된다.
- 활성 발화 처리 정책이 일관된다.
- 음소거 해제 후 마이크를 다시 사용할 수 있다.

### Step 10. 상태 연결

완료 조건:

- `Ready`
- `Listening`
- `SpeechDetected`
- `Transcribing`
- `Muted`
- `Error`

상태 전환을 UI에서 확인할 수 있다.

기존 Mate 시스템에는 이벤트로만 전달하며 음성 코드가 Animator를 직접 제어하지 않는다.

### Step 11. 결과 인터페이스

완료 조건:

- Partial 이벤트가 제공된다.
- Final 이벤트가 제공된다.
- 상태 변경 이벤트가 제공된다.
- 오류 이벤트가 제공된다.
- 구독자가 없어도 오류가 발생하지 않는다.
- LLM 구현 없이 테스트 구독자가 Final 결과를 받을 수 있다.

---

## Phase 2E — 말풍선과 통합 검증

### Step 12. 사용자 음성 말풍선

완료 조건:

- Mate 머리 위에 말풍선이 표시된다.
- Mate가 이동하거나 드래그될 때 말풍선이 따라간다.
- Partial 결과가 같은 말풍선에서 실시간으로 갱신된다.
- Final 결과가 확정 텍스트로 교체된다.
- 새 발화가 시작되면 이전 표시가 정책대로 초기화된다.
- Final 텍스트가 일정 시간 후 자동으로 숨겨진다.
- 긴 한국어 문장이 자동 줄바꿈된다.
- 화면 가장자리에서 말풍선이 잘리지 않는다.
- 말풍선 UI가 Mate 드래그와 바탕화면 클릭 통과를 방해하지 않는다.
- Speech View가 whisper 구체 클래스에 직접 의존하지 않는다.

### Step 13. Windows 빌드 테스트

반드시 Unity Editor와 Windows Player 양쪽에서 확인한다.

- 마이크 목록
- 마이크 선택
- A 키 입력
- 음성 시작·종료
- 한국어 Partial
- 한국어 Final
- Partial 말풍선 표시
- Final 말풍선 표시
- Mate 이동·드래그 시 말풍선 추적
- 말풍선 자동 숨김
- 말풍선 영역 클릭 통과
- 음소거
- 앱 포커스 손실
- 모델 로드
- 앱 종료
- Mate Phase 1 기능 유지

---

# 22. 테스트 시나리오

## 22.1 정상 한국어 문장

말하기:

```text
안녕, 오늘은 무엇을 하면 좋을까?
```

확인:

- Partial이 한 번 이상 갱신됨
- Final이 한 번 발생함
- Final 문장이 의미상 알아볼 수 있음
- 언어가 `ko`
- 빈 결과가 없음

## 22.2 A 키만 짧게 누르기

확인:

- 빈 Final 결과 없음
- 오류 상태로 가지 않음
- Ready로 복귀

## 22.3 A 키를 누르고 침묵

확인:

- Listening은 되지만 SpeechDetected가 되지 않음
- A Up 후 STT를 불필요하게 실행하지 않음

## 22.4 말하다가 A 키 놓기

확인:

- 마지막 단어가 과도하게 잘리지 않음
- 현재 발화가 Final 처리됨

## 22.5 긴 발화

확인:

- 최대 발화 길이 제한
- 메모리 무한 증가 없음
- UI 멈춤 없음

## 22.6 키보드와 마우스 소음

확인:

- 작은 클릭 소리만으로 긴 오인식이 생성되지 않음
- VAD 임계값 조정 가능

## 22.7 마이크 변경

확인:

- 기존 장치 정지
- 새 장치 시작
- 버퍼 초기화
- 이후 정상 인식

## 22.8 마이크 연결 해제

확인:

- 크래시 없음
- 오류 메시지 표시
- 장치 새로고침 후 복구 가능

## 22.9 음소거

확인:

- 음소거 중 A 키 무시
- 음소거 해제 후 정상 복구
- 미완성 발화가 잘못 Final 처리되지 않음

## 22.10 앱 포커스 손실

확인:

- A Up 이벤트 누락으로 무한 Listening에 남지 않음
- 현재 세션이 정책대로 마감 또는 폐기됨

## 22.11 연속 발화

확인:

- 여러 UtteranceId가 구분됨
- 이전 Partial이 다음 Final을 덮어쓰지 않음
- STT 요청이 무한 큐잉되지 않음

---

## 22.12 말풍선 Partial 확인

말하기:

```text
오늘은 학교에 가서 프로젝트 작업을 할 거야
```

확인:

- 말하는 동안 말풍선 텍스트가 한 번 이상 갱신됨
- Partial 문장이 새 말풍선으로 계속 쌓이지 않음
- 이전 Partial이 최신 Partial로 교체됨
- 말풍선이 Mate 머리 위를 따라감

## 22.13 말풍선 Final 확인

확인:

- A 키를 놓으면 확정 문장이 말풍선에 표시됨
- Final 결과가 Partial 표시를 교체함
- 설정된 시간이 지나면 말풍선이 숨겨짐
- 다음 A 키 세션 시작 시 이전 문장이 남아 있지 않음

## 22.14 긴 문장과 화면 가장자리

확인:

- 한국어 자동 줄바꿈
- Mate가 화면 좌우 가장자리에 있어도 말풍선이 잘리지 않음
- 말풍선이 화면 전체를 과도하게 가리지 않음
- 전체 문장은 디버그 UI에서 확인 가능

## 22.15 클릭 통과

Windows Player에서 확인:

- 말풍선 위 또는 주변 빈 영역이 의도치 않게 바탕화면 클릭을 막지 않음
- Mate Collider 위에서는 기존 클릭·드래그가 정상 동작함
- 말풍선 Graphic이 Unity UI Raycast를 가로채지 않음

---

# 23. 성능 목표

정확한 수치는 사용자 PC 성능과 모델에 따라 달라지므로, 먼저 측정하고 기록한다.

최소 목표:

- 마이크 입력 중 Mate 애니메이션이 눈에 띄게 끊기지 않음
- 녹음 중 매 프레임 대규모 GC Allocation 없음
- Final 추론 중 Unity 창이 장시간 응답 없음 상태가 되지 않음
- 10분 이상 반복 테스트에서 메모리 사용량이 계속 증가하지 않음
- Partial 요청이 쌓여 Final 지연이 계속 증가하지 않음

기록 항목:

```text
CPU
Memory
Model Load Time
Partial Inference Time
Final Inference Time
Audio Duration
Real-time Factor
Dropped Audio Sample 여부
GC Allocation
```

---

# 24. Phase 2 완료 체크리스트

## 마이크

- [ ] Windows 마이크 목록을 조회할 수 있다.
- [ ] 장치를 선택할 수 있다.
- [ ] 선택 장치를 저장하고 복원한다.
- [ ] 녹음을 시작하고 중지할 수 있다.
- [ ] 장치가 없을 때 명확한 오류가 나온다.
- [ ] 순환 버퍼 Wrap-around가 정상 처리된다.

## Push-to-Talk

- [ ] A Down 시 Listening이 시작된다.
- [ ] A Held 동안 입력을 수집한다.
- [ ] A Up 시 현재 발화를 마감한다.
- [ ] 포커스 손실 시 무한 Listening에 남지 않는다.
- [ ] 짧은 무음 입력은 STT를 실행하지 않는다.

## VAD

- [ ] 음성 시작을 감지한다.
- [ ] 음성 종료를 감지한다.
- [ ] Pre-roll이 적용된다.
- [ ] 종료 침묵 시간을 조절할 수 있다.
- [ ] 최대 발화 길이가 제한된다.
- [ ] VAD 수치를 디버그 UI에서 확인할 수 있다.

## 한국어 STT

- [ ] 로컬 모델이 로드된다.
- [ ] 한국어를 인식한다.
- [ ] 영어 전용 모델을 사용하지 않는다.
- [ ] Windows Player에서도 네이티브 라이브러리가 로드된다.
- [ ] 빈 Final을 발행하지 않는다.
- [ ] Final이 중복 발행되지 않는다.

## 결과

- [ ] Partial과 Final이 구분된다.
- [ ] Partial은 `IsFinal = false`다.
- [ ] Final은 `IsFinal = true`다.
- [ ] 결과에 `UtteranceId`가 있다.
- [ ] 오래된 Partial이 새 결과를 덮어쓰지 않는다.
- [ ] Final 결과를 공통 이벤트로 전달한다.
- [ ] LLM 클래스에 직접 의존하지 않는다.

## 사용자 음성 말풍선

- [ ] Partial 결과가 Mate 위 말풍선에 표시된다.
- [ ] 새 Partial이 기존 Partial을 교체한다.
- [ ] Final 결과가 확정 문장으로 표시된다.
- [ ] Final 표시 후 자동으로 숨겨진다.
- [ ] 새 발화 시작 시 이전 표시가 초기화된다.
- [ ] 긴 한국어 문장이 줄바꿈된다.
- [ ] Mate 이동과 드래그를 따라간다.
- [ ] 화면 가장자리에서 말풍선이 잘리지 않는다.
- [ ] UI Raycast를 가로채지 않는다.
- [ ] Windows 클릭 통과를 방해하지 않는다.
- [ ] whisper 구체 타입에 의존하지 않는다.

## 음소거와 상태

- [ ] 음소거를 켜고 끌 수 있다.
- [ ] 음소거 중 A 키 입력이 무시된다.
- [ ] 음소거 해제 후 복구된다.
- [ ] Listening 상태가 있다.
- [ ] Transcribing 상태가 있다.
- [ ] Error 상태와 오류 메시지가 있다.
- [ ] Mate 표현 계층과 이벤트로 연결된다.

## 안정성

- [ ] 마이크 입력이 Mate 움직임을 멈추게 하지 않는다.
- [ ] 추론이 Unity 메인 스레드를 장시간 막지 않는다.
- [ ] 앱 종료 시 마이크와 네이티브 리소스를 정리한다.
- [ ] 10분 이상 반복 테스트에서 메모리 누수가 눈에 띄지 않는다.
- [ ] 모델과 음성 파일을 Git에 불필요하게 포함하지 않는다.
- [ ] 원본 음성을 기본적으로 디스크에 저장하지 않는다.

---

# 25. Codex 작업 결과 보고 형식

작업 완료 후 프로젝트 안에 다음 내용을 보고한다.

```markdown
# Phase 2 작업 결과

## 1. 조사 결과
- Unity 버전:
- 입력 시스템:
- 비동기 처리 방식:
- 이벤트 시스템:
- 사용한 STT 패키지 및 버전:
- 사용한 whisper.cpp 버전:
- 사용한 모델:
- 모델 경로:
- Windows 빌드 백엔드:

## 2. 생성한 파일
- 경로:
- 역할:

## 3. 수정한 파일
- 경로:
- 수정 내용:

## 4. 최종 구조
- 마이크 입력 흐름:
- 버퍼 구조:
- VAD 정책:
- Partial 정책:
- Final 정책:
- 음소거 정책:
- 결과 전달 방식:

## 5. 상태 전환
- Ready:
- Listening:
- SpeechDetected:
- Transcribing:
- Muted:
- Error:

## 6. 테스트 결과
- [ ] 마이크 목록
- [ ] 마이크 선택
- [ ] PCM 입력
- [ ] A 키 Push-to-Talk
- [ ] 음성 시작 감지
- [ ] 음성 종료 감지
- [ ] 한국어 Partial
- [ ] 한국어 Final
- [ ] Partial 말풍선
- [ ] Final 말풍선
- [ ] Mate 추적
- [ ] 자동 숨김
- [ ] 말풍선 클릭 통과
- [ ] 음소거
- [ ] 포커스 손실
- [ ] Editor 실행
- [ ] Windows Player 실행
- [ ] Phase 1 기능 유지

## 7. 성능 측정
- 모델 로드 시간:
- 평균 Partial 추론 시간:
- 평균 Final 추론 시간:
- 테스트 발화 길이:
- 메모리 사용:
- 알려진 GC Allocation:

## 8. 남은 문제
- 문제:
- 재현 방법:
- 임시 해결:
- 다음 권장 작업:

## 9. 말풍선 구현 결과
- 말풍선 Canvas 방식:
- Mate Anchor:
- Partial 표시 정책:
- Final 표시 시간:
- 화면 경계 처리:
- 클릭 통과 처리:
- 알려진 UI 문제:

## 10. Phase 3 연결점
- Final 결과 이벤트:
- 결과 데이터 타입:
- LLM 시스템이 구독할 위치:
- TTS 중 입력 Suspend 방법:
```

---

# 26. Codex 최종 실행 지시

아래 순서대로 진행한다.

1. 기존 Phase 1 프로젝트 구조를 조사한다.
2. Phase 1 기능을 손상시키지 않는 별도 Voice/STT 계층을 설계한다.
3. 마이크 장치 목록과 장치 선택부터 구현한다.
4. PCM 순환 버퍼와 입력 에너지 표시를 완성한다.
5. A 키 Push-to-Talk를 구현한다.
6. RMS 기반 VAD로 음성 시작과 종료를 구분한다.
7. Pre-roll, 종료 침묵, 최소 발화, 최대 발화를 적용한다.
8. 현재 Unity 버전에 호환되는 로컬 whisper.cpp Unity 연동을 검증하고 설치한다.
9. 한국어 multilingual 모델로 Final 인식을 먼저 완성한다.
10. Final이 안정화된 뒤 Partial 인식을 추가한다.
11. 음소거와 Suspend 확장 지점을 구현한다.
12. Partial, Final, State, Error 이벤트를 공통 인터페이스로 제공한다.
13. Mate Head Anchor를 따라가는 사용자 음성 말풍선을 구현한다.
14. Partial은 실시간으로 교체 표시하고 Final은 확정 문장으로 일정 시간 표시한다.
15. 말풍선이 Unity UI 입력과 Windows 클릭 통과를 방해하지 않는지 확인한다.
16. Unity Editor에서 테스트한다.
17. Windows x64 Player를 빌드하여 네이티브 라이브러리, 마이크, 말풍선, 클릭 통과를 검증한다.
18. 최소 10분간 반복 테스트한다.
19. LLM, TTS, 립싱크는 구현하지 않는다.
20. 완료 후 `Phase 2 작업 결과` 형식으로 보고한다.

---

# 최종 한 줄 목표

> 사용자가 Unity 애플리케이션에 포커스를 둔 상태에서 A 키를 누르고 한국어로 말하면, Mate가 Listening과 Transcribing 상태를 거쳐 중간 텍스트와 확정 텍스트를 구분하고, 인식된 내용을 Mate 위 말풍선으로 보여준 뒤 최종 텍스트를 다음 단계의 LLM 시스템이 받을 수 있는 공통 인터페이스로 전달하게 만든다.
