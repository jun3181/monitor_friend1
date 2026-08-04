using System;
using System.Threading;
using System.Threading.Tasks;
using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;

namespace Mate.Runtime.Voice.Recognition
{
    public interface ISpeechToTextProvider : IDisposable
    {
        string ProviderName { get; }
        bool IsReady { get; }
        bool IsInitializing { get; }
        string ModelPath { get; }

        Task InitializeAsync(SpeechRecognitionProfile profile, CancellationToken cancellationToken);

        Task<SpeechRecognitionResult> TranscribeAsync(
            float[] samples,
            int sampleRate,
            int sessionId,
            int utteranceId,
            bool isFinal,
            float startTime,
            float endTime,
            CancellationToken cancellationToken);
    }
}
