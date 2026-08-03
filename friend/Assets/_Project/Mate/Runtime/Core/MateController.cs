using Mate.Runtime.Animation;
using Mate.Runtime.Face;
using Mate.Runtime.Look;
using Mate.Runtime.Movement;
using UnityEngine;

namespace Mate.Runtime.Core
{
    [DisallowMultipleComponent]
    public sealed class MateController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private MateState currentState = MateState.Initialize;
        [SerializeField] private float reactionDuration = 0.55f;

        [Header("Components")]
        [SerializeField] private MateAnimationController animationController;
        [SerializeField] private MateNaturalMotion naturalMotion;
        [SerializeField] private MateLookController lookController;
        [SerializeField] private MateBlinkController blinkController;

        private float _reactionEndsAt;

        public MateState CurrentState => currentState;
        public bool IsInteractiveLocked => currentState.BlocksAutonomousMotion();

        private void Awake()
        {
            CacheComponents();
            SetState(MateState.Idle);
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (currentState == MateState.Reacting && Time.time >= _reactionEndsAt)
            {
                SetState(MateState.Idle);
            }
        }

        public void SetWalking(bool walking)
        {
            if (currentState == MateState.Dragged || currentState == MateState.Picked)
            {
                return;
            }

            SetState(walking ? MateState.Walking : MateState.Idle);
        }

        public void BeginDrag()
        {
            SetState(MateState.Dragged);
            React(MateReactionType.DragStart, 0.15f);
        }

        public void EndDrag()
        {
            React(MateReactionType.DragEnd, 0.2f);
            SetState(MateState.Idle);
        }

        public void React(MateReactionType reactionType)
        {
            React(reactionType, 1f);
            _reactionEndsAt = Time.time + reactionDuration;
            SetState(MateState.Reacting);
        }

        private void React(MateReactionType reactionType, float strength)
        {
            animationController?.PlayReaction(reactionType);
            naturalMotion?.Pulse(reactionType, strength);
            lookController?.Notice();
            blinkController?.BlinkNow();
        }

        private void SetState(MateState state)
        {
            if (currentState == state)
            {
                return;
            }

            currentState = state;
            animationController?.SetState(state);
            naturalMotion?.SetState(state);
        }

        private void CacheComponents()
        {
            animationController = animationController != null ? animationController : GetComponent<MateAnimationController>();
            naturalMotion = naturalMotion != null ? naturalMotion : GetComponent<MateNaturalMotion>();
            lookController = lookController != null ? lookController : GetComponent<MateLookController>();
            blinkController = blinkController != null ? blinkController : GetComponent<MateBlinkController>();
        }
    }
}
