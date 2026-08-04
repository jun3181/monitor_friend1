using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Mate.Runtime.Voice.Presentation
{
    [DisallowMultipleComponent]
    public sealed class UserSpeechBubbleView : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionCoordinator coordinator;
        [SerializeField] private SpeechRecognitionProfile profile;
        [SerializeField] private MateScreenAnchor screenAnchor;
        [SerializeField] private RectTransform bubbleRoot;
        [SerializeField] private Text bubbleText;
        [SerializeField] private Image bubbleBackground;
        [SerializeField] private CanvasGroup canvasGroup;

        private BubbleState _state = BubbleState.Hidden;
        private float _hideAt = -1f;
        private int _sessionId = -1;
        private int _utteranceId = -1;

        private void Awake()
        {
            Cache();
            EnsureView();
            HideImmediate();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_state == BubbleState.Hidden)
            {
                return;
            }

            if (screenAnchor != null && !screenAnchor.UpdatePosition())
            {
                canvasGroup.alpha = 0f;
                return;
            }

            canvasGroup.alpha = 1f;

            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                HideImmediate();
            }
        }

        public void Configure(
            SpeechRecognitionCoordinator source,
            SpeechRecognitionProfile sourceProfile,
            MateScreenAnchor anchor)
        {
            Unsubscribe();
            coordinator = source;
            profile = sourceProfile;
            screenAnchor = anchor;
            Cache();
            EnsureView();
            Subscribe();
        }

        private void Subscribe()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.OnStateChanged += HandleStateChanged;
            coordinator.OnPartialResult += HandlePartial;
            coordinator.OnFinalResult += HandleFinal;
            coordinator.OnError += HandleError;
        }

        private void Unsubscribe()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.OnStateChanged -= HandleStateChanged;
            coordinator.OnPartialResult -= HandlePartial;
            coordinator.OnFinalResult -= HandleFinal;
            coordinator.OnError -= HandleError;
        }

        private void HandleStateChanged(SpeechRecognitionState state)
        {
            if (state == SpeechRecognitionState.Listening)
            {
                _sessionId = -1;
                _utteranceId = -1;
                ShowListening();
            }
            else if (state == SpeechRecognitionState.Ready && _state == BubbleState.Listening)
            {
                HideImmediate();
            }
        }

        private void HandlePartial(SpeechRecognitionResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                return;
            }

            _sessionId = result.SessionId;
            _utteranceId = result.UtteranceId;
            ShowText(result.Text, BubbleState.Partial);
        }

        private void HandleFinal(SpeechRecognitionResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                return;
            }

            _sessionId = result.SessionId;
            _utteranceId = result.UtteranceId;
            ShowText(result.Text, BubbleState.Final);
            var seconds = profile != null ? profile.finalBubbleSeconds : 4f;
            _hideAt = Time.unscaledTime + Mathf.Clamp(seconds, 1.5f, profile != null ? profile.maxBubbleSeconds : 8f);
        }

        private void HandleError(SpeechRecognitionError error)
        {
            if (error == null
                || error.Code == SpeechRecognitionErrorCode.EmptySpeech
                || error.Code == SpeechRecognitionErrorCode.AudioBufferOverflow)
            {
                return;
            }

            ShowText("음성 인식 확인 필요", BubbleState.Error);
            _hideAt = Time.unscaledTime + 2.5f;
        }

        private void ShowListening()
        {
            ShowText("듣는 중...", BubbleState.Listening);
            _hideAt = -1f;
        }

        private void ShowText(string text, BubbleState state)
        {
            EnsureView();
            _state = state;
            bubbleText.text = text;
            bubbleText.color = state == BubbleState.Partial ? new Color(0.86f, 0.91f, 1f, 1f) : Color.white;

            if (bubbleBackground != null)
            {
                bubbleBackground.color = state switch
                {
                    BubbleState.Partial => new Color(0.1f, 0.16f, 0.24f, 0.86f),
                    BubbleState.Final => new Color(0.08f, 0.18f, 0.14f, 0.9f),
                    BubbleState.Error => new Color(0.32f, 0.08f, 0.08f, 0.9f),
                    _ => new Color(0.08f, 0.1f, 0.14f, 0.82f)
                };
            }

            canvasGroup.alpha = 1f;
        }

        private void HideImmediate()
        {
            _state = BubbleState.Hidden;
            _hideAt = -1f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Cache()
        {
            coordinator = coordinator != null ? coordinator : FindFirstObjectByType<SpeechRecognitionCoordinator>();
            screenAnchor = screenAnchor != null ? screenAnchor : GetComponent<MateScreenAnchor>();
            profile = profile != null ? profile : (coordinator != null ? coordinator.Profile : null);
        }

        private void EnsureView()
        {
            if (bubbleRoot != null && bubbleText != null && canvasGroup != null)
            {
                DisableRaycasts();
                return;
            }

            var canvasObject = new GameObject("User Speech Bubble Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>();

            var rootObject = new GameObject("Bubble");
            rootObject.transform.SetParent(canvasObject.transform, false);
            bubbleRoot = rootObject.AddComponent<RectTransform>();
            bubbleRoot.sizeDelta = new Vector2(380f, 96f);
            bubbleRoot.pivot = new Vector2(0.5f, 0.5f);

            bubbleBackground = rootObject.AddComponent<Image>();
            bubbleBackground.raycastTarget = false;
            bubbleBackground.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);

            canvasGroup = rootObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.ignoreParentGroups = true;

            var layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = rootObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(rootObject.transform, false);
            bubbleText = textObject.AddComponent<Text>();
            bubbleText.raycastTarget = false;
            bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bubbleText.fontSize = 20;
            bubbleText.alignment = TextAnchor.MiddleCenter;
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            bubbleText.color = Color.white;

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(348f, 70f);

            var layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 220f;
            layoutElement.preferredWidth = 348f;
            layoutElement.flexibleWidth = 0f;

            if (screenAnchor != null)
            {
                screenAnchor.SetTargetRect(bubbleRoot);
            }

            DisableRaycasts();
        }

        private void DisableRaycasts()
        {
            if (bubbleBackground != null)
            {
                bubbleBackground.raycastTarget = false;
            }

            if (bubbleText != null)
            {
                bubbleText.raycastTarget = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private enum BubbleState
        {
            Hidden,
            Listening,
            Partial,
            Final,
            Error
        }
    }
}
