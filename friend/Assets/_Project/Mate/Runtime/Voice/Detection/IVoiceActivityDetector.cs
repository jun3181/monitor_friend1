namespace Mate.Runtime.Voice.Detection
{
    public readonly struct VoiceActivityFrame
    {
        public readonly bool IsSpeech;
        public readonly float Energy;
        public readonly float Threshold;

        public VoiceActivityFrame(bool isSpeech, float energy, float threshold)
        {
            IsSpeech = isSpeech;
            Energy = energy;
            Threshold = threshold;
        }
    }

    public interface IVoiceActivityDetector
    {
        VoiceActivityFrame Evaluate(float[] monoSamples, int sampleCount, bool speechAlreadyActive);
    }
}
