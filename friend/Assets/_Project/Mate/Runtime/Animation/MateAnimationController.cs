using Mate.Runtime.Core;
using UnityEngine;

namespace Mate.Runtime.Animation
{
    [DisallowMultipleComponent]
    public sealed class MateAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool useAnimatorControllerStates;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private float crossFadeDuration = 0.2f;

        private int _idleStateHash;
        private int _walkStateHash;

        private void Awake()
        {
            Cache();
        }

        private void Reset()
        {
            Cache();
        }

        public void SetState(MateState state)
        {
            if (!useAnimatorControllerStates || animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            var stateHash = state == MateState.Walking ? _walkStateHash : _idleStateHash;
            if (stateHash != 0 && animator.HasState(0, stateHash))
            {
                animator.CrossFadeInFixedTime(stateHash, crossFadeDuration);
            }
        }

        public void PlayReaction(MateReactionType reactionType)
        {
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
        }

        private void Cache()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            _idleStateHash = string.IsNullOrEmpty(idleStateName) ? 0 : Animator.StringToHash(idleStateName);
            _walkStateHash = string.IsNullOrEmpty(walkStateName) ? 0 : Animator.StringToHash(walkStateName);

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }
    }
}
