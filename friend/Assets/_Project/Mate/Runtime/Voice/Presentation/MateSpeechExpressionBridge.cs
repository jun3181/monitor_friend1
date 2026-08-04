using Mate.Runtime.Face;
using Mate.Runtime.Look;
using Mate.Runtime.Voice.Core;
using UnityEngine;

namespace Mate.Runtime.Voice.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MateSpeechExpressionBridge : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionCoordinator coordinator;
        [SerializeField] private MateLookController lookController;
        [SerializeField] private MateBlinkController blinkController;

        private void Awake()
        {
            Cache();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            SpeechRecognitionCoordinator source,
            MateLookController look,
            MateBlinkController blink)
        {
            Unsubscribe();
            coordinator = source;
            lookController = look;
            blinkController = blink;
            Subscribe();
        }

        private void Subscribe()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.OnStateChanged += HandleStateChanged;
            coordinator.OnFinalResult += HandleFinalResult;
        }

        private void Unsubscribe()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.OnStateChanged -= HandleStateChanged;
            coordinator.OnFinalResult -= HandleFinalResult;
        }

        private void HandleStateChanged(SpeechRecognitionState state)
        {
            if (state == SpeechRecognitionState.Listening || state == SpeechRecognitionState.SpeechDetected)
            {
                lookController?.Notice();
            }
        }

        private void HandleFinalResult(SpeechRecognitionResult result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.Text))
            {
                blinkController?.BlinkNow();
                lookController?.Notice();
            }
        }

        private void Cache()
        {
            coordinator = coordinator != null ? coordinator : FindFirstObjectByType<SpeechRecognitionCoordinator>();
            lookController = lookController != null ? lookController : GetComponent<MateLookController>();
            blinkController = blinkController != null ? blinkController : GetComponent<MateBlinkController>();
        }
    }
}
