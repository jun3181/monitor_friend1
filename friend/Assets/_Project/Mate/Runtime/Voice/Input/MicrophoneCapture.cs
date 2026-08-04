using System;
using Mate.Runtime.Voice.Core;
using UnityEngine;

namespace Mate.Runtime.Voice.Input
{
    public sealed class MicrophoneCapture
    {
        private AudioClip _clip;
        private string _deviceName;
        private int _lastReadPosition;
        private float[] _wrapScratch;

        public bool IsRunning { get; private set; }
        public int Frequency => _clip != null ? _clip.frequency : 0;
        public int Channels => _clip != null ? _clip.channels : 0;
        public int ClipFrameCount => _clip != null ? _clip.samples : 0;
        public string DeviceName => _deviceName;

        public SpeechRecognitionError Start(string deviceName, int requestedFrequency, int loopLengthSeconds)
        {
            Stop();

            try
            {
                _deviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName;
                _clip = Microphone.Start(_deviceName, true, Mathf.Max(1, loopLengthSeconds), Mathf.Max(8000, requestedFrequency));
                if (_clip == null)
                {
                    return new SpeechRecognitionError(
                        SpeechRecognitionErrorCode.MicrophoneStartFailed,
                        "Unity Microphone.Start returned null.",
                        _deviceName ?? "Default microphone");
                }

                _lastReadPosition = Microphone.GetPosition(_deviceName);
                IsRunning = true;
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                return new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.MicrophonePermissionDenied,
                    "Microphone permission was denied.",
                    ex.Message);
            }
            catch (Exception ex)
            {
                return new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.MicrophoneStartFailed,
                    "Microphone could not be started.",
                    ex.Message);
            }
        }

        public void Stop()
        {
            if (!IsRunning && _clip == null)
            {
                return;
            }

            try
            {
                Microphone.End(_deviceName);
            }
            finally
            {
                if (_clip != null)
                {
                    UnityEngine.Object.Destroy(_clip);
                }

                _clip = null;
                _deviceName = null;
                _lastReadPosition = 0;
                IsRunning = false;
            }
        }

        public int GetAvailableFrames()
        {
            if (!IsRunning || _clip == null)
            {
                return 0;
            }

            var position = Microphone.GetPosition(_deviceName);
            if (position < 0)
            {
                return 0;
            }

            return Distance(_lastReadPosition, position, _clip.samples);
        }

        public bool TryReadChunk(float[] destination, int frameCount, out int framesRead)
        {
            framesRead = 0;
            if (!IsRunning || _clip == null || destination == null || frameCount <= 0)
            {
                return false;
            }

            if (GetAvailableFrames() < frameCount || destination.Length < frameCount * _clip.channels)
            {
                return false;
            }

            ReadFrames(destination, frameCount);
            framesRead = frameCount;
            return true;
        }

        public int DrainAvailable(float[] destination)
        {
            if (!IsRunning || _clip == null || destination == null)
            {
                return 0;
            }

            var channels = _clip.channels;
            var frames = Mathf.Min(GetAvailableFrames(), destination.Length / channels);
            if (frames <= 0)
            {
                return 0;
            }

            ReadFrames(destination, frames);
            return frames;
        }

        private void ReadFrames(float[] destination, int frameCount)
        {
            var clipFrames = _clip.samples;
            var channels = _clip.channels;
            var requiredFloats = frameCount * channels;
            var framesToEnd = clipFrames - _lastReadPosition;

            if (frameCount <= framesToEnd)
            {
                if (destination.Length == requiredFloats)
                {
                    _clip.GetData(destination, _lastReadPosition);
                }
                else
                {
                    EnsureWrapScratch(requiredFloats);
                    _clip.GetData(_wrapScratch, _lastReadPosition);
                    Array.Copy(_wrapScratch, 0, destination, 0, requiredFloats);
                }

                _lastReadPosition = (_lastReadPosition + frameCount) % clipFrames;
                return;
            }

            var firstFloats = framesToEnd * channels;
            EnsureWrapScratch(firstFloats);
            _clip.GetData(_wrapScratch, _lastReadPosition);
            Array.Copy(_wrapScratch, 0, destination, 0, firstFloats);

            var secondFrames = frameCount - framesToEnd;
            var secondFloats = secondFrames * channels;
            EnsureWrapScratch(secondFloats);
            _clip.GetData(_wrapScratch, 0);
            Array.Copy(_wrapScratch, 0, destination, firstFloats, secondFloats);

            _lastReadPosition = secondFrames;
        }

        private void EnsureWrapScratch(int length)
        {
            if (_wrapScratch == null || _wrapScratch.Length != length)
            {
                _wrapScratch = new float[length];
            }
        }

        private static int Distance(int from, int to, int length)
        {
            return to >= from ? to - from : length - from + to;
        }
    }
}
