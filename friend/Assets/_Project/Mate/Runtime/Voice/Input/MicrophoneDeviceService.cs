using System;
using System.Collections.Generic;
using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;
using UnityEngine;

namespace Mate.Runtime.Voice.Input
{
    public sealed class MicrophoneDeviceService
    {
        private const string PreferredDeviceKey = "Mate.Phase2.PreferredMicrophoneDevice";

        private readonly SpeechRecognitionProfile _profile;
        private readonly List<string> _devices = new List<string>();
        private string _selectedDeviceName;

        public event Action DevicesChanged;

        public IReadOnlyList<string> Devices => _devices;
        public string SelectedDeviceName => _selectedDeviceName;
        public bool HasDevices => _devices.Count > 0;

        public MicrophoneDeviceService(SpeechRecognitionProfile profile)
        {
            _profile = profile;
            RefreshDevices();
        }

        public SpeechRecognitionError RefreshDevices()
        {
            _devices.Clear();
            var devices = Microphone.devices;
            for (var i = 0; i < devices.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(devices[i]))
                {
                    _devices.Add(devices[i]);
                }
            }

            RestoreSelection();
            DevicesChanged?.Invoke();

            if (_devices.Count == 0)
            {
                return new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.NoMicrophoneDevice,
                    "Windows microphone device list is empty.",
                    "Check Windows microphone permission and input devices.");
            }

            return null;
        }

        public SpeechRecognitionError SelectDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                _selectedDeviceName = string.Empty;
                PlayerPrefs.SetString(PreferredDeviceKey, string.Empty);
                PlayerPrefs.Save();
                DevicesChanged?.Invoke();
                return null;
            }

            if (!_devices.Contains(deviceName))
            {
                return new SpeechRecognitionError(
                    SpeechRecognitionErrorCode.SavedMicrophoneMissing,
                    "Selected microphone device was not found.",
                    deviceName);
            }

            _selectedDeviceName = deviceName;
            PlayerPrefs.SetString(PreferredDeviceKey, _selectedDeviceName);
            PlayerPrefs.Save();
            DevicesChanged?.Invoke();
            return null;
        }

        private void RestoreSelection()
        {
            var saved = PlayerPrefs.GetString(PreferredDeviceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(saved))
            {
                saved = _profile != null ? _profile.preferredMicrophoneDevice : string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(saved) && _devices.Contains(saved))
            {
                _selectedDeviceName = saved;
                return;
            }

            _selectedDeviceName = string.Empty;
        }
    }
}
