using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;
using UnityEngine;
using Whisper;

namespace Mate.Runtime.Voice.Recognition
{
    [DisallowMultipleComponent]
    public sealed class WhisperSpeechToTextProvider : MonoBehaviour, ISpeechToTextProvider
    {
        private readonly SemaphoreSlim _transcriptionGate = new SemaphoreSlim(1, 1);

        private SpeechRecognitionProfile _profile;
        private WhisperWrapper _wrapper;
        private WhisperParams _params;
        private string _modelPath;
        private bool _disposed;

        public string ProviderName => "whisper.unity";
        public bool IsReady => _wrapper != null;
        public bool IsInitializing { get; private set; }
        public string ModelPath => _modelPath;
        public long LastInferenceMilliseconds { get; private set; }

        public async Task InitializeAsync(SpeechRecognitionProfile profile, CancellationToken cancellationToken)
        {
            if (IsReady || IsInitializing)
            {
                return;
            }

            _profile = profile;
            _modelPath = ResolveModelPath(profile);
            if (!File.Exists(_modelPath))
            {
                throw new FileNotFoundException("Whisper model file was not found.", _modelPath);
            }

            IsInitializing = true;
            try
            {
                var context = WhisperContextParams.GetDefaultParams();
                context.UseGpu = profile.useGpu;
                context.FlashAttn = profile.flashAttention;

                cancellationToken.ThrowIfCancellationRequested();
                _wrapper = await WhisperWrapper.InitFromFileAsync(_modelPath, context);
                cancellationToken.ThrowIfCancellationRequested();

                if (_wrapper == null)
                {
                    throw new InvalidOperationException("Whisper failed to load the model.");
                }

                _params = WhisperParams.GetDefaultParams();
                _params.Language = string.IsNullOrWhiteSpace(profile.language) ? "ko" : profile.language;
                _params.Translate = false;
                _params.NoContext = true;
                _params.SingleSegment = true;
                _params.PrintProgress = false;
                _params.PrintRealtime = false;
                _params.PrintTimestamps = false;
            }
            finally
            {
                IsInitializing = false;
            }
        }

        public async Task<SpeechRecognitionResult> TranscribeAsync(
            float[] samples,
            int sampleRate,
            int sessionId,
            int utteranceId,
            bool isFinal,
            float startTime,
            float endTime,
            CancellationToken cancellationToken)
        {
            if (_wrapper == null || _params == null)
            {
                return SpeechRecognitionResult.FromError(
                    sessionId,
                    utteranceId,
                    SpeechRecognitionErrorCode.ModelLoadFailed,
                    "Whisper provider is not initialized.",
                    ProviderName);
            }

            if (samples == null || samples.Length == 0)
            {
                return SpeechRecognitionResult.FromError(
                    sessionId,
                    utteranceId,
                    SpeechRecognitionErrorCode.EmptySpeech,
                    "Audio buffer is empty.",
                    ProviderName);
            }

            await _transcriptionGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                var preparedSamples = PrepareSamplesForWhisper(samples, _profile);
                var whisperResult = await _wrapper.GetTextAsync(preparedSamples, sampleRate, 1, _params);
                sw.Stop();
                LastInferenceMilliseconds = sw.ElapsedMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();
                if (whisperResult == null)
                {
                    return SpeechRecognitionResult.FromError(
                        sessionId,
                        utteranceId,
                        SpeechRecognitionErrorCode.TranscriptionFailed,
                        "Whisper returned no transcription result.",
                        ProviderName);
                }

                var text = NormalizeText(whisperResult.Result);
                return SpeechRecognitionResult.FromText(
                    sessionId,
                    utteranceId,
                    text,
                    string.IsNullOrWhiteSpace(_profile.language) ? "ko" : _profile.language,
                    isFinal,
                    startTime,
                    endTime,
                    samples.Length / (float)Mathf.Max(1, sampleRate),
                    ProviderName);
            }
            catch (OperationCanceledException)
            {
                return SpeechRecognitionResult.FromError(
                    sessionId,
                    utteranceId,
                    SpeechRecognitionErrorCode.TranscriptionCancelled,
                    "Whisper transcription was cancelled.",
                    ProviderName);
            }
            catch (DllNotFoundException ex)
            {
                return SpeechRecognitionResult.FromError(
                    sessionId,
                    utteranceId,
                    SpeechRecognitionErrorCode.NativeLibraryLoadFailed,
                    "Whisper native DLL could not be loaded.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return SpeechRecognitionResult.FromError(
                    sessionId,
                    utteranceId,
                    SpeechRecognitionErrorCode.TranscriptionFailed,
                    "Whisper transcription failed.",
                    ex.Message);
            }
            finally
            {
                _transcriptionGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wrapper = null;
            _params = null;
            _transcriptionGate.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private static string ResolveModelPath(SpeechRecognitionProfile profile)
        {
            var configuredPath = string.IsNullOrWhiteSpace(profile.modelPath)
                ? "Whisper/ggml-base.bin"
                : profile.modelPath;

            return profile.modelPathInStreamingAssets
                ? Path.Combine(Application.streamingAssetsPath, configuredPath)
                : configuredPath;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static float[] PrepareSamplesForWhisper(float[] samples, SpeechRecognitionProfile profile)
        {
            if (samples == null || samples.Length == 0)
            {
                return samples;
            }

            var prepared = new float[samples.Length];
            Array.Copy(samples, prepared, samples.Length);

            if (profile == null || !profile.normalizeAudioForStt)
            {
                return prepared;
            }

            var peak = 0f;
            var sum = 0.0;
            for (var i = 0; i < prepared.Length; i++)
            {
                var sample = prepared[i];
                peak = Mathf.Max(peak, Mathf.Abs(sample));
                sum += sample * sample;
            }

            if (peak <= 0.000001f)
            {
                return prepared;
            }

            var rms = Mathf.Sqrt((float)(sum / prepared.Length));
            var rmsGain = rms > 0.000001f ? profile.targetSttRms / rms : profile.maxSttGain;
            var peakGain = profile.peakLimit / peak;
            var gain = Mathf.Min(profile.maxSttGain, rmsGain, peakGain);
            if (gain <= 0f || Mathf.Approximately(gain, 1f))
            {
                return prepared;
            }

            for (var i = 0; i < prepared.Length; i++)
            {
                prepared[i] = Mathf.Clamp(prepared[i] * gain, -profile.peakLimit, profile.peakLimit);
            }

            return prepared;
        }
    }
}
