using System;

namespace Mate.Runtime.Voice.Core
{
    [Serializable]
    public sealed class SpeechRecognitionResult
    {
        public int SessionId;
        public int UtteranceId;
        public string Text;
        public string Language;
        public bool IsFinal;
        public float StartTime;
        public float EndTime;
        public float AudioDuration;
        public string ProviderName;
        public float Confidence;
        public SpeechRecognitionErrorCode ErrorCode;
        public string ErrorMessage;

        public bool HasError => ErrorCode != SpeechRecognitionErrorCode.None;

        public static SpeechRecognitionResult FromText(
            int sessionId,
            int utteranceId,
            string text,
            string language,
            bool isFinal,
            float startTime,
            float endTime,
            float audioDuration,
            string providerName)
        {
            return new SpeechRecognitionResult
            {
                SessionId = sessionId,
                UtteranceId = utteranceId,
                Text = text,
                Language = language,
                IsFinal = isFinal,
                StartTime = startTime,
                EndTime = endTime,
                AudioDuration = audioDuration,
                ProviderName = providerName,
                Confidence = -1f,
                ErrorCode = SpeechRecognitionErrorCode.None
            };
        }

        public static SpeechRecognitionResult FromError(
            int sessionId,
            int utteranceId,
            SpeechRecognitionErrorCode code,
            string message,
            string providerName)
        {
            return new SpeechRecognitionResult
            {
                SessionId = sessionId,
                UtteranceId = utteranceId,
                Text = string.Empty,
                Language = string.Empty,
                IsFinal = true,
                ProviderName = providerName,
                Confidence = -1f,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
