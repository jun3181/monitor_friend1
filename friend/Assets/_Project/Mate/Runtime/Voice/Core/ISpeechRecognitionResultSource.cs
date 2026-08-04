using System;

namespace Mate.Runtime.Voice.Core
{
    public interface ISpeechRecognitionResultSource
    {
        SpeechRecognitionState CurrentState { get; }
        bool IsMuted { get; }

        event Action<SpeechRecognitionResult> OnPartialResult;
        event Action<SpeechRecognitionResult> OnFinalResult;
        event Action<SpeechRecognitionState> OnStateChanged;
        event Action<SpeechRecognitionError> OnError;
    }
}
