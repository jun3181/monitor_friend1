using System;

namespace Mate.Runtime.Voice.Core
{
    [Serializable]
    public sealed class SpeechRecognitionError
    {
        public SpeechRecognitionErrorCode Code;
        public string Message;
        public string Detail;

        public SpeechRecognitionError(SpeechRecognitionErrorCode code, string message, string detail = "")
        {
            Code = code;
            Message = message;
            Detail = detail;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? $"{Code}: {Message}" : $"{Code}: {Message} ({Detail})";
        }
    }
}
