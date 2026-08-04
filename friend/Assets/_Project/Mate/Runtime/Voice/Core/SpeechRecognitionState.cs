namespace Mate.Runtime.Voice.Core
{
    public enum SpeechRecognitionState
    {
        Uninitialized,
        Initializing,
        Ready,
        Listening,
        SpeechDetected,
        Transcribing,
        Muted,
        Suspended,
        Error
    }

    public enum SpeechRecognitionErrorCode
    {
        None,
        NoMicrophoneDevice,
        SavedMicrophoneMissing,
        MicrophoneStartFailed,
        MicrophonePermissionDenied,
        MicrophonePositionStalled,
        AudioBufferOverflow,
        ModelFileMissing,
        ModelLoadFailed,
        NativeLibraryLoadFailed,
        UnsupportedPlatform,
        TranscriptionFailed,
        TranscriptionCancelled,
        InvalidSampleRate,
        EmptySpeech,
        UtteranceTooLong,
        ShutdownFailed
    }
}
