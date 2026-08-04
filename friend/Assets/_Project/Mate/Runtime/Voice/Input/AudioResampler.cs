using UnityEngine;

namespace Mate.Runtime.Voice.Input
{
    public static class AudioResampler
    {
        public static void Copy(float[] source, float[] destination, int count)
        {
            if (source == null || destination == null || count <= 0)
            {
                return;
            }

            System.Array.Copy(source, destination, Mathf.Min(count, Mathf.Min(source.Length, destination.Length)));
        }

        public static int DownmixToMono(float[] interleaved, int frameCount, int channels, float[] mono)
        {
            if (interleaved == null || mono == null || frameCount <= 0)
            {
                return 0;
            }

            if (channels <= 1)
            {
                for (var i = 0; i < frameCount; i++)
                {
                    mono[i] = interleaved[i];
                }

                return frameCount;
            }

            for (var frame = 0; frame < frameCount; frame++)
            {
                var sum = 0f;
                var offset = frame * channels;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += interleaved[offset + channel];
                }

                mono[frame] = Mathf.Clamp(sum / channels, -1f, 1f);
            }

            return frameCount;
        }

        public static float CalculateRms(float[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return 0f;
            }

            var sum = 0.0;
            for (var i = 0; i < count; i++)
            {
                var sample = samples[i];
                sum += sample * sample;
            }

            return Mathf.Sqrt((float)(sum / count));
        }

        public static void ApplyGain(float[] samples, int count, float gain)
        {
            if (samples == null || count <= 0)
            {
                return;
            }

            gain = Mathf.Max(0f, gain);
            if (Mathf.Approximately(gain, 1f))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }
        }
    }
}
