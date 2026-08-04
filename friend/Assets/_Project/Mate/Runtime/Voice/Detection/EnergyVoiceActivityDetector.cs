using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Input;
using UnityEngine;

namespace Mate.Runtime.Voice.Detection
{
    public sealed class EnergyVoiceActivityDetector : IVoiceActivityDetector
    {
        private readonly SpeechRecognitionProfile _profile;

        public EnergyVoiceActivityDetector(SpeechRecognitionProfile profile)
        {
            _profile = profile;
        }

        public VoiceActivityFrame Evaluate(float[] monoSamples, int sampleCount, bool speechAlreadyActive)
        {
            var energy = AudioResampler.CalculateRms(monoSamples, sampleCount);
            var threshold = speechAlreadyActive ? _profile.vadEndThreshold : _profile.vadStartThreshold;
            threshold = Mathf.Max(threshold, _profile.noiseFloor);
            return new VoiceActivityFrame(energy >= threshold, energy, threshold);
        }
    }
}
