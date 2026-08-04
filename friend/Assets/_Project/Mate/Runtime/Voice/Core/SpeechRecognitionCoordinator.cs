using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Detection;
using Mate.Runtime.Voice.Input;
using Mate.Runtime.Voice.Recognition;
using UnityEngine;

namespace Mate.Runtime.Voice.Core
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class SpeechRecognitionCoordinator : MonoBehaviour, ISpeechRecognitionResultSource
    {
        [Header("Configuration")]
        [SerializeField] private SpeechRecognitionProfile profile;
        [SerializeField] private bool initializeOnStart = true;

        [Header("Provider")]
        [SerializeField] private WhisperSpeechToTextProvider whisperProvider;

        [Header("Debug")]
        [SerializeField] private SpeechRecognitionState currentState = SpeechRecognitionState.Uninitialized;
        [SerializeField] private bool isMuted;
        [SerializeField] private bool isSuspended;

        private MicrophoneDeviceService _deviceService;
        private MicrophoneCapture _capture;
        private AudioRingBuffer _preRollBuffer;
        private IVoiceActivityDetector _voiceActivityDetector;
        private CancellationTokenSource _lifetimeCts;
        private readonly List<float> _utteranceSamples = new List<float>(16000 * 8);

        private float[] _readScratch;
        private float[] _drainScratch;
        private float[] _monoScratch;
        private float[] _vadScratch;
        private bool _pushToTalkActive;
        private bool _speechActive;
        private bool _endingSession;
        private bool _partialInFlight;
        private bool _finalInFlight;
        private float _speechCandidateSeconds;
        private float _silenceSeconds;
        private float _utteranceStartTime;
        private float _nextPartialAt;
        private int _sessionId;
        private int _utteranceId;
        private SpeechRecognitionError _lastError;

        public event Action<SpeechRecognitionResult> OnPartialResult;
        public event Action<SpeechRecognitionResult> OnFinalResult;
        public event Action<SpeechRecognitionState> OnStateChanged;
        public event Action<SpeechRecognitionError> OnError;

        public SpeechRecognitionState CurrentState => currentState;
        public bool IsMuted => isMuted;
        public bool IsSuspended => isSuspended;
        public bool IsMicrophoneRunning => _capture != null && _capture.IsRunning;
        public bool IsModelLoaded => whisperProvider != null && whisperProvider.IsReady;
        public bool IsModelInitializing => whisperProvider != null && whisperProvider.IsInitializing;
        public bool IsTranscribing => _finalInFlight || _partialInFlight;
        public IReadOnlyList<string> MicrophoneDevices => _deviceService != null ? _deviceService.Devices : Array.Empty<string>();
        public string SelectedMicrophoneDevice => _deviceService != null ? _deviceService.SelectedDeviceName : string.Empty;
        public int InputSampleRate => _capture != null ? _capture.Frequency : 0;
        public int InputChannelCount => _capture != null ? _capture.Channels : 0;
        public float CurrentRms { get; private set; }
        public float CurrentRawRms { get; private set; }
        public float CurrentVadThreshold { get; private set; }
        public bool IsVoiceDetected { get; private set; }
        public string LastPartialText { get; private set; } = string.Empty;
        public string LastFinalText { get; private set; } = string.Empty;
        public string LastErrorText => _lastError != null ? _lastError.ToString() : string.Empty;
        public long LastInferenceMilliseconds => whisperProvider != null ? whisperProvider.LastInferenceMilliseconds : 0;
        public string ModelPath => whisperProvider != null ? whisperProvider.ModelPath : string.Empty;
        public SpeechRecognitionProfile Profile => profile;

        private void Awake()
        {
            Cache();
            _lifetimeCts = new CancellationTokenSource();
        }

        private async void Start()
        {
            if (initializeOnStart)
            {
                await InitializeAsync();
            }
        }

        private void Reset()
        {
            Cache();
        }

        private void Update()
        {
            if (!_pushToTalkActive || _capture == null || !_capture.IsRunning || _endingSession)
            {
                return;
            }

            PumpMicrophone(false);
        }

        private void OnDestroy()
        {
            try
            {
                _capture?.Stop();
                _lifetimeCts?.Cancel();
                _lifetimeCts?.Dispose();
                whisperProvider?.Dispose();
            }
            catch (Exception ex)
            {
                RaiseError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.ShutdownFailed,
                    "Speech recognition shutdown failed.",
                    ex.Message));
            }
        }

        public void Configure(SpeechRecognitionProfile targetProfile, WhisperSpeechToTextProvider provider)
        {
            profile = targetProfile;
            whisperProvider = provider;
        }

        public async Task InitializeAsync()
        {
            if (currentState == SpeechRecognitionState.Initializing || currentState == SpeechRecognitionState.Ready)
            {
                return;
            }

            Cache();
            profile = profile != null ? profile : SpeechRecognitionProfile.CreateRuntimeDefault();
            isMuted = profile.muteOnStart;

            SetState(SpeechRecognitionState.Initializing);
            _deviceService = new MicrophoneDeviceService(profile);
            _capture = new MicrophoneCapture();
            _preRollBuffer = new AudioRingBuffer(Mathf.CeilToInt(profile.preRollSeconds * profile.targetSampleRate));
            _voiceActivityDetector = new EnergyVoiceActivityDetector(profile);

            var deviceError = _deviceService.RefreshDevices();
            if (deviceError != null)
            {
                RaiseError(deviceError);
            }

            try
            {
                if (whisperProvider == null)
                {
                    whisperProvider = gameObject.AddComponent<WhisperSpeechToTextProvider>();
                }

                await whisperProvider.InitializeAsync(profile, _lifetimeCts.Token);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                HandleFatalError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.ModelFileMissing,
                    "Whisper model file is missing.",
                    ex.FileName));
                return;
            }
            catch (DllNotFoundException ex)
            {
                HandleFatalError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.NativeLibraryLoadFailed,
                    "Whisper native DLL could not be loaded.",
                    ex.Message));
                return;
            }
            catch (Exception ex)
            {
                HandleFatalError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.ModelLoadFailed,
                    "Whisper provider initialization failed.",
                    ex.Message));
                return;
            }

            if (deviceError != null)
            {
                SetState(SpeechRecognitionState.Error);
                return;
            }

            SetState(isMuted ? SpeechRecognitionState.Muted : SpeechRecognitionState.Ready);
        }

        public SpeechRecognitionError RefreshMicrophones()
        {
            if (_deviceService == null)
            {
                _deviceService = new MicrophoneDeviceService(profile != null ? profile : SpeechRecognitionProfile.CreateRuntimeDefault());
            }

            var error = _deviceService.RefreshDevices();
            if (error != null)
            {
                RaiseError(error);
            }

            return error;
        }

        public SpeechRecognitionError SelectMicrophone(string deviceName)
        {
            if (_deviceService == null)
            {
                RefreshMicrophones();
            }

            var error = _deviceService.SelectDevice(deviceName);
            if (error != null)
            {
                RaiseError(error);
            }

            return error;
        }

        public void BeginPushToTalk()
        {
            if (_pushToTalkActive || _finalInFlight)
            {
                return;
            }

            if (currentState == SpeechRecognitionState.Uninitialized)
            {
                _ = InitializeAsync();
                return;
            }

            if (isMuted)
            {
                SetState(SpeechRecognitionState.Muted);
                return;
            }

            if (isSuspended)
            {
                SetState(SpeechRecognitionState.Suspended);
                return;
            }

            if (whisperProvider == null || !whisperProvider.IsReady)
            {
                RaiseError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.ModelLoadFailed,
                    "Whisper model is not ready.",
                    ModelPath));
                SetState(SpeechRecognitionState.Error);
                return;
            }

            var deviceError = RefreshMicrophones();
            if (deviceError != null)
            {
                SetState(SpeechRecognitionState.Error);
                return;
            }

            var startError = _capture.Start(
                _deviceService.SelectedDeviceName,
                profile.requestedMicrophoneSampleRate,
                profile.microphoneLoopLengthSeconds);
            if (startError != null)
            {
                HandleFatalError(startError);
                return;
            }

            _preRollBuffer = new AudioRingBuffer(Mathf.CeilToInt(profile.preRollSeconds * Mathf.Max(1, _capture.Frequency)));
            EnsureScratchBuffers();
            _sessionId++;
            _utteranceId++;
            _pushToTalkActive = true;
            _speechActive = false;
            _endingSession = false;
            _speechCandidateSeconds = 0f;
            _silenceSeconds = 0f;
            _utteranceStartTime = Time.unscaledTime;
            _nextPartialAt = Time.unscaledTime + profile.partialUpdateInterval;
            _utteranceSamples.Clear();
            _preRollBuffer.Clear();
            LastPartialText = string.Empty;

            SetState(SpeechRecognitionState.Listening);
        }

        public void EndPushToTalk()
        {
            if (!_pushToTalkActive)
            {
                return;
            }

            FinishSession(true, SpeechRecognitionErrorCode.None);
        }

        public void SetMuted(bool muted)
        {
            if (isMuted == muted)
            {
                return;
            }

            isMuted = muted;
            if (isMuted)
            {
                if (_pushToTalkActive)
                {
                    DiscardActiveSession();
                }

                _capture?.Stop();
                SetState(SpeechRecognitionState.Muted);
            }
            else
            {
                SetState(isSuspended ? SpeechRecognitionState.Suspended : SpeechRecognitionState.Ready);
            }
        }

        public void ToggleMuted()
        {
            SetMuted(!isMuted);
        }

        public void SetCaptureSuspended(bool suspended)
        {
            if (isSuspended == suspended)
            {
                return;
            }

            isSuspended = suspended;
            if (isSuspended)
            {
                if (_pushToTalkActive)
                {
                    DiscardActiveSession();
                }

                _capture?.Stop();
                SetState(SpeechRecognitionState.Suspended);
            }
            else if (!isMuted)
            {
                SetState(SpeechRecognitionState.Ready);
            }
        }

        private void PumpMicrophone(bool drain)
        {
            if (_capture == null || !_capture.IsRunning)
            {
                return;
            }

            while (_capture.TryReadChunk(_readScratch, profile.readChunkFrames, out var framesRead))
            {
                ProcessAudioFrames(_readScratch, framesRead);
                if (_endingSession)
                {
                    return;
                }
            }

            if (!drain)
            {
                return;
            }

            while (_capture.GetAvailableFrames() > 0)
            {
                var frames = _capture.DrainAvailable(_drainScratch);
                if (frames <= 0)
                {
                    break;
                }

                ProcessAudioFrames(_drainScratch, frames);
                if (_endingSession)
                {
                    return;
                }
            }
        }

        private void ProcessAudioFrames(float[] interleavedSamples, int framesRead)
        {
            var monoFrames = AudioResampler.DownmixToMono(interleavedSamples, framesRead, Mathf.Max(1, _capture.Channels), _monoScratch);
            AudioResampler.Copy(_monoScratch, _vadScratch, monoFrames);
            AudioResampler.ApplyGain(_vadScratch, monoFrames, profile.vadInputGain);
            var vadFrame = _voiceActivityDetector.Evaluate(_vadScratch, monoFrames, _speechActive);
            CurrentRawRms = AudioResampler.CalculateRms(_monoScratch, monoFrames);
            CurrentRms = vadFrame.Energy;
            CurrentVadThreshold = vadFrame.Threshold;
            IsVoiceDetected = vadFrame.IsSpeech;

            var seconds = monoFrames / (float)Mathf.Max(1, _capture.Frequency);

            if (!_speechActive)
            {
                _preRollBuffer.Write(_monoScratch, monoFrames);
                var overflow = _preRollBuffer.ConsumeOverflowWarning();
                if (overflow != null)
                {
                    RaiseError(overflow);
                }

                _speechCandidateSeconds = vadFrame.IsSpeech ? _speechCandidateSeconds + seconds : 0f;
                if (_speechCandidateSeconds >= profile.minimumSpeechSeconds)
                {
                    BeginSpeech();
                }

                return;
            }

            AddMonoSamplesToUtterance(_monoScratch, monoFrames);
            _silenceSeconds = vadFrame.IsSpeech ? 0f : _silenceSeconds + seconds;

            var utteranceSeconds = _utteranceSamples.Count / (float)Mathf.Max(1, _capture.Frequency);
            if (utteranceSeconds >= profile.maximumUtteranceSeconds)
            {
                RaiseError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.UtteranceTooLong,
                    "Maximum utterance duration was reached.",
                    $"{profile.maximumUtteranceSeconds:0.0}s"));
                FinishSession(true, SpeechRecognitionErrorCode.UtteranceTooLong);
                return;
            }

            if (_silenceSeconds >= profile.endSilenceSeconds)
            {
                FinishSession(true, SpeechRecognitionErrorCode.None);
                return;
            }

            TryRequestPartial();
        }

        private void BeginSpeech()
        {
            _speechActive = true;
            _silenceSeconds = 0f;
            _utteranceStartTime = Time.unscaledTime - (_preRollBuffer.Count / (float)Mathf.Max(1, _capture.Frequency));
            _utteranceSamples.Clear();
            _preRollBuffer.CopyTo(_utteranceSamples);
            SetState(SpeechRecognitionState.SpeechDetected);
        }

        private void FinishSession(bool transcribeIfValid, SpeechRecognitionErrorCode reason)
        {
            if (_endingSession)
            {
                return;
            }

            _endingSession = true;
            PumpMicrophone(true);
            var inputSampleRate = Mathf.Max(1, _capture != null ? _capture.Frequency : profile.requestedMicrophoneSampleRate);
            _capture?.Stop();
            _pushToTalkActive = false;
            IsVoiceDetected = false;

            if (!transcribeIfValid || !_speechActive)
            {
                DiscardActiveSession();
                SetReadyState();
                return;
            }

            var sampleRate = inputSampleRate;
            var duration = _utteranceSamples.Count / (float)sampleRate;
            if (duration < profile.minimumSpeechSeconds)
            {
                RaiseError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.EmptySpeech,
                    "Speech was too short to transcribe.",
                    $"{duration:0.00}s"));
                DiscardActiveSession();
                SetReadyState();
                return;
            }

            var samples = _utteranceSamples.ToArray();
            var sessionId = _sessionId;
            var utteranceId = _utteranceId;
            var start = _utteranceStartTime;
            var end = Time.unscaledTime;
            _speechActive = false;
            _utteranceSamples.Clear();
            _ = RunFinalTranscriptionAsync(samples, sampleRate, sessionId, utteranceId, start, end, reason);
        }

        private void DiscardActiveSession()
        {
            _capture?.Stop();
            _pushToTalkActive = false;
            _speechActive = false;
            _endingSession = false;
            _speechCandidateSeconds = 0f;
            _silenceSeconds = 0f;
            _utteranceSamples.Clear();
            _preRollBuffer?.Clear();
            IsVoiceDetected = false;
        }

        private async Task RunFinalTranscriptionAsync(
            float[] samples,
            int sampleRate,
            int sessionId,
            int utteranceId,
            float startTime,
            float endTime,
            SpeechRecognitionErrorCode reason)
        {
            _finalInFlight = true;
            _partialInFlight = false;
            SetState(SpeechRecognitionState.Transcribing);

            var result = await whisperProvider.TranscribeAsync(
                samples,
                sampleRate,
                sessionId,
                utteranceId,
                true,
                startTime,
                endTime,
                _lifetimeCts.Token);

            _finalInFlight = false;
            _endingSession = false;

            if (result.HasError)
            {
                RaiseError(new SpeechRecognitionError(result.ErrorCode, result.ErrorMessage, result.ProviderName));
            }
            else if (!string.IsNullOrWhiteSpace(result.Text) && IsCurrentUtterance(result))
            {
                LastFinalText = result.Text;
                OnFinalResult?.Invoke(result);
                if (profile.debugLogging)
                {
                    Debug.Log($"Speech final[{sessionId}:{utteranceId}] {result.Text}");
                }
            }
            else if (reason == SpeechRecognitionErrorCode.None)
            {
                RaiseError(new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.EmptySpeech,
                    "Whisper returned an empty final result."));
            }

            SetReadyState();
        }

        private void TryRequestPartial()
        {
            if (!profile.partialEnabled || _partialInFlight || _finalInFlight || Time.unscaledTime < _nextPartialAt)
            {
                return;
            }

            var duration = _utteranceSamples.Count / (float)Mathf.Max(1, _capture.Frequency);
            if (duration < profile.minimumSpeechSeconds)
            {
                return;
            }

            _nextPartialAt = Time.unscaledTime + profile.partialUpdateInterval;
            var samples = _utteranceSamples.ToArray();
            var sessionId = _sessionId;
            var utteranceId = _utteranceId;
            var start = _utteranceStartTime;
            var end = Time.unscaledTime;
            _ = RunPartialTranscriptionAsync(samples, _capture.Frequency, sessionId, utteranceId, start, end);
        }

        private async Task RunPartialTranscriptionAsync(
            float[] samples,
            int sampleRate,
            int sessionId,
            int utteranceId,
            float startTime,
            float endTime)
        {
            _partialInFlight = true;
            var result = await whisperProvider.TranscribeAsync(
                samples,
                sampleRate,
                sessionId,
                utteranceId,
                false,
                startTime,
                endTime,
                _lifetimeCts.Token);
            _partialInFlight = false;

            if (result.HasError)
            {
                if (result.ErrorCode != SpeechRecognitionErrorCode.TranscriptionCancelled)
                {
                    RaiseError(new SpeechRecognitionError(result.ErrorCode, result.ErrorMessage, result.ProviderName));
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(result.Text) || result.SessionId != _sessionId || result.UtteranceId != _utteranceId || _finalInFlight)
            {
                return;
            }

            LastPartialText = result.Text;
            OnPartialResult?.Invoke(result);
        }

        private bool IsCurrentUtterance(SpeechRecognitionResult result)
        {
            return result.SessionId == _sessionId && result.UtteranceId == _utteranceId;
        }

        private void AddMonoSamplesToUtterance(float[] samples, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _utteranceSamples.Add(samples[i]);
            }
        }

        private void EnsureScratchBuffers()
        {
            var channels = Mathf.Max(1, _capture.Channels);
            var chunkFrames = Mathf.Max(256, profile.readChunkFrames);
            var interleavedLength = chunkFrames * channels;

            if (_readScratch == null || _readScratch.Length != interleavedLength)
            {
                _readScratch = new float[interleavedLength];
            }

            if (_drainScratch == null || _drainScratch.Length != interleavedLength)
            {
                _drainScratch = new float[interleavedLength];
            }

            if (_monoScratch == null || _monoScratch.Length != chunkFrames)
            {
                _monoScratch = new float[chunkFrames];
            }

            if (_vadScratch == null || _vadScratch.Length != chunkFrames)
            {
                _vadScratch = new float[chunkFrames];
            }
        }

        private void HandleFatalError(SpeechRecognitionError error)
        {
            RaiseError(error);
            DiscardActiveSession();
            SetState(SpeechRecognitionState.Error);
        }

        private void RaiseError(SpeechRecognitionError error)
        {
            _lastError = error;
            OnError?.Invoke(error);
            if (profile == null || profile.debugLogging)
            {
                Debug.LogWarning(error.ToString());
            }
        }

        private void SetReadyState()
        {
            if (isMuted)
            {
                SetState(SpeechRecognitionState.Muted);
            }
            else if (isSuspended)
            {
                SetState(SpeechRecognitionState.Suspended);
            }
            else
            {
                SetState(SpeechRecognitionState.Ready);
            }
        }

        private void SetState(SpeechRecognitionState state)
        {
            if (currentState == state)
            {
                return;
            }

            currentState = state;
            OnStateChanged?.Invoke(state);
        }

        private void Cache()
        {
            whisperProvider = whisperProvider != null ? whisperProvider : GetComponent<WhisperSpeechToTextProvider>();
        }
    }
}
