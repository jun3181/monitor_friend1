using System;
using System.Collections.Generic;
using Mate.Runtime.Voice.Core;

namespace Mate.Runtime.Voice.Input
{
    public sealed class AudioRingBuffer
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;
        public bool Overflowed { get; private set; }

        public AudioRingBuffer(int capacitySamples)
        {
            _buffer = new float[Math.Max(1, capacitySamples)];
        }

        public void Clear()
        {
            _writeIndex = 0;
            _count = 0;
            Overflowed = false;
        }

        public void Write(float[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return;
            }

            if (count > _buffer.Length)
            {
                var start = count - _buffer.Length;
                Array.Copy(samples, start, _buffer, 0, _buffer.Length);
                _writeIndex = 0;
                _count = _buffer.Length;
                Overflowed = true;
                return;
            }

            for (var i = 0; i < count; i++)
            {
                _buffer[_writeIndex] = samples[i];
                _writeIndex = (_writeIndex + 1) % _buffer.Length;
            }

            _count = Math.Min(_count + count, _buffer.Length);
        }

        public void CopyTo(List<float> destination)
        {
            if (destination == null || _count <= 0)
            {
                return;
            }

            var start = (_writeIndex - _count + _buffer.Length) % _buffer.Length;
            for (var i = 0; i < _count; i++)
            {
                destination.Add(_buffer[(start + i) % _buffer.Length]);
            }
        }

        public SpeechRecognitionError ConsumeOverflowWarning()
        {
            if (!Overflowed)
            {
                return null;
            }

            Overflowed = false;
            return new SpeechRecognitionError(
                SpeechRecognitionErrorCode.AudioBufferOverflow,
                "Audio pre-roll buffer overflowed.",
                $"Capacity samples: {_buffer.Length}");
        }
    }
}
