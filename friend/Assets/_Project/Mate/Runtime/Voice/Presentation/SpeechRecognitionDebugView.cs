using Mate.Runtime.Voice.Core;
using UnityEngine;

namespace Mate.Runtime.Voice.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SpeechRecognitionDebugView : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionCoordinator coordinator;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private Rect windowRect = new Rect(14f, 14f, 430f, 520f);

        private Vector2 _scroll;

        private void Awake()
        {
            coordinator = coordinator != null ? coordinator : GetComponent<SpeechRecognitionCoordinator>();
        }

        public void Configure(SpeechRecognitionCoordinator source)
        {
            coordinator = source;
        }

        private void OnGUI()
        {
            if (!showOverlay || coordinator == null)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Mate Speech Debug");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"State: {coordinator.CurrentState}");
            GUILayout.Label($"PTT: {coordinator.Profile?.pushToTalkKey ?? KeyCode.A}");
            GUILayout.Label($"Mic Running: {coordinator.IsMicrophoneRunning}");
            GUILayout.Label($"Input: {coordinator.InputSampleRate} Hz / {coordinator.InputChannelCount} ch");
            var gain = coordinator.Profile != null ? coordinator.Profile.vadInputGain : 1f;
            GUILayout.Label($"VAD Gain: {gain:0.0}x");
            GUILayout.Label($"Raw RMS: {coordinator.CurrentRawRms:0.0000}  VAD RMS: {coordinator.CurrentRms:0.0000}");
            GUILayout.Label($"Threshold: {coordinator.CurrentVadThreshold:0.0000}  Speech: {coordinator.IsVoiceDetected}");
            GUILayout.Label($"Model Loaded: {coordinator.IsModelLoaded}  Init: {coordinator.IsModelInitializing}");
            GUILayout.Label($"Model Path: {coordinator.ModelPath}");
            GUILayout.Label($"Inference: {coordinator.LastInferenceMilliseconds} ms");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Mics"))
            {
                coordinator.RefreshMicrophones();
            }

            if (GUILayout.Button(coordinator.IsMuted ? "Unmute" : "Mute"))
            {
                coordinator.ToggleMuted();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Gain -"))
            {
                AdjustVadGain(-0.5f);
            }

            if (GUILayout.Button("Gain +"))
            {
                AdjustVadGain(0.5f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Microphones");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(110f));
            if (GUILayout.Button(string.IsNullOrWhiteSpace(coordinator.SelectedMicrophoneDevice) ? "> Default" : "Default"))
            {
                coordinator.SelectMicrophone(string.Empty);
            }

            var devices = coordinator.MicrophoneDevices;
            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var selected = device == coordinator.SelectedMicrophoneDevice;
                if (GUILayout.Button(selected ? $"> {device}" : device))
                {
                    coordinator.SelectMicrophone(device);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.Label("Partial");
            GUILayout.TextArea(coordinator.LastPartialText, GUILayout.Height(52f));
            GUILayout.Label("Final");
            GUILayout.TextArea(coordinator.LastFinalText, GUILayout.Height(70f));

            if (!string.IsNullOrWhiteSpace(coordinator.LastErrorText))
            {
                GUILayout.Label("Last Error");
                GUILayout.TextArea(coordinator.LastErrorText, GUILayout.Height(62f));
            }

            GUI.DragWindow();
        }

        private void AdjustVadGain(float delta)
        {
            if (coordinator == null || coordinator.Profile == null)
            {
                return;
            }

            coordinator.Profile.vadInputGain = Mathf.Clamp(coordinator.Profile.vadInputGain + delta, 1f, 12f);
        }
    }
}
