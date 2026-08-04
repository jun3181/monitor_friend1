using UnityEngine;

namespace Mate.Runtime.Voice.Configuration
{
    [CreateAssetMenu(menuName = "Mate/Voice/Speech Recognition Profile", fileName = "SpeechRecognitionProfile")]
    public sealed class SpeechRecognitionProfile : ScriptableObject
    {
        [Header("Input")]
        public KeyCode pushToTalkKey = KeyCode.A;
        public int requestedMicrophoneSampleRate = 16000;
        public int microphoneLoopLengthSeconds = 10;
        public int readChunkFrames = 1024;
        public string preferredMicrophoneDevice = "";
        [Range(1f, 12f)]
        public float vadInputGain = 4f;

        [Header("Utterance")]
        public int targetSampleRate = 16000;
        public float preRollSeconds = 0.35f;
        public float postRollSeconds = 0.2f;
        public float minimumSpeechSeconds = 0.3f;
        public float endSilenceSeconds = 0.8f;
        public float maximumUtteranceSeconds = 25f;

        [Header("VAD")]
        public float vadStartThreshold = 0.008f;
        public float vadEndThreshold = 0.004f;
        public float noiseFloor = 0.001f;

        [Header("Recognition")]
        public bool partialEnabled = true;
        public float partialUpdateInterval = 1.2f;
        public string language = "ko";
        public string modelPath = "Whisper/ggml-small.bin";
        public bool modelPathInStreamingAssets = true;
        public bool useGpu;
        public bool flashAttention;
        public bool normalizeAudioForStt = true;
        [Range(0.02f, 0.25f)]
        public float targetSttRms = 0.08f;
        [Range(1f, 16f)]
        public float maxSttGain = 8f;
        [Range(0.5f, 0.98f)]
        public float peakLimit = 0.95f;

        [Header("Runtime")]
        public bool muteOnStart;
        public bool debugLogging = true;
        public float finalBubbleSeconds = 4f;
        public float maxBubbleSeconds = 8f;

        public static SpeechRecognitionProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<SpeechRecognitionProfile>();
            profile.name = "Runtime Speech Recognition Profile";
            return profile;
        }
    }
}
