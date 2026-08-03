using Mate.Runtime.Core;
using UnityEngine;

namespace Mate.Runtime.Movement
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class MateNaturalMotion : MonoBehaviour
    {
        [Header("Idle Motion")]
        [SerializeField] private bool enableIdleMotion = true;
        [SerializeField] private float breathSpeed = 1.2f;
        [SerializeField] private float breathAngle = 1.5f;
        [SerializeField] private float swaySpeed = 0.7f;
        [SerializeField] private float swayAngle = 2.2f;

        [Header("Rest Pose")]
        [SerializeField] private bool relaxArms = true;
        [Range(0f, 1f)]
        [SerializeField] private float armRelaxAmount = 0.9f;
        [SerializeField] private float armSwayAngle = 1.1f;

        [Header("Reactions")]
        [SerializeField] private float reactionDecay = 6f;
        [SerializeField] private float clickPulseAngle = 5f;
        [SerializeField] private float strokePulseAngle = 3f;

        private Animator _animator;
        private MateState _state = MateState.Idle;

        private Transform _spine;
        private Transform _chest;
        private Transform _upperChest;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;

        private Quaternion _spineBase;
        private Quaternion _chestBase;
        private Quaternion _upperChestBase;
        private Quaternion _leftUpperArmBase;
        private Quaternion _rightUpperArmBase;

        private float _seed;
        private float _reactionPulse;

        private void Awake()
        {
            CacheBones();
            _seed = Random.Range(0f, 100f);
        }

        private void Reset()
        {
            CacheBones();
        }

        private void LateUpdate()
        {
            if (!enableIdleMotion || _animator == null)
            {
                return;
            }

            var blocked = _state == MateState.Dragged || _state == MateState.Picked;
            var stateWeight = blocked ? 0.35f : 1f;
            var time = Time.time + _seed;
            var breath = Mathf.Sin(time * breathSpeed) * breathAngle * stateWeight;
            var sway = Mathf.Sin(time * swaySpeed) * swayAngle * stateWeight;
            var smallNoise = Mathf.Sin(time * 0.43f + 1.7f) * 0.8f * stateWeight;

            _reactionPulse = Mathf.MoveTowards(_reactionPulse, 0f, reactionDecay * Time.deltaTime);
            var reaction = _reactionPulse;

            if (_spine != null)
            {
                _spine.localRotation = _spineBase * Quaternion.Euler(breath * 0.35f, sway * 0.18f, -sway * 0.16f);
            }

            if (_chest != null)
            {
                _chest.localRotation = _chestBase * Quaternion.Euler(breath + reaction, sway * 0.28f, sway * 0.18f);
            }

            if (_upperChest != null)
            {
                _upperChest.localRotation = _upperChestBase * Quaternion.Euler(breath * 0.45f + reaction * 0.35f, sway * 0.18f, smallNoise);
            }

            if (relaxArms)
            {
                ApplyArmRelaxation(_leftUpperArm, _leftLowerArm, _leftUpperArmBase, -1f, time, stateWeight);
                ApplyArmRelaxation(_rightUpperArm, _rightLowerArm, _rightUpperArmBase, 1f, time + 0.35f, stateWeight);
            }
        }

        public void SetState(MateState state)
        {
            _state = state;
        }

        public void Pulse(MateReactionType reactionType, float strength)
        {
            var angle = reactionType == MateReactionType.Stroke ? strokePulseAngle : clickPulseAngle;
            _reactionPulse = Mathf.Max(_reactionPulse, angle * Mathf.Clamp01(strength));
        }

        private void CacheBones()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            _spineBase = _spine != null ? _spine.localRotation : Quaternion.identity;
            _chestBase = _chest != null ? _chest.localRotation : Quaternion.identity;
            _upperChestBase = _upperChest != null ? _upperChest.localRotation : Quaternion.identity;
            _leftUpperArmBase = _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity;
            _rightUpperArmBase = _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity;
        }

        private void ApplyArmRelaxation(Transform upperArm, Transform lowerArm, Quaternion baseRotation, float side, float time, float stateWeight)
        {
            if (upperArm == null || lowerArm == null || upperArm.parent == null)
            {
                return;
            }

            upperArm.localRotation = baseRotation;

            var currentDirection = lowerArm.position - upperArm.position;
            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var sideDirection = transform.right * side * 0.22f;
            var desiredDirection = (-transform.up * 0.92f + sideDirection + transform.forward * 0.08f).normalized;
            var delta = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection);
            var targetWorldRotation = delta * upperArm.rotation;
            var targetLocalRotation = Quaternion.Inverse(upperArm.parent.rotation) * targetWorldRotation;
            var armSway = Mathf.Sin(time * (swaySpeed * 0.8f)) * armSwayAngle * stateWeight;

            upperArm.localRotation = Quaternion.Slerp(
                baseRotation,
                targetLocalRotation * Quaternion.Euler(0f, 0f, armSway * side),
                armRelaxAmount);
        }
    }
}
