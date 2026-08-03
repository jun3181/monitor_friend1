using UnityEngine;

namespace Mate.Runtime.Look
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class MateLookController : MonoBehaviour
    {
        [SerializeField] private bool enableLook = true;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float headWeight = 0.65f;
        [SerializeField] private float neckWeight = 0.25f;
        [SerializeField] private float maxAngle = 28f;
        [SerializeField] private float smooth = 7f;
        [SerializeField] private Vector2 randomLookInterval = new Vector2(2f, 5f);
        [SerializeField] private Vector2 randomLookOffset = new Vector2(0.35f, 0.22f);

        private Animator _animator;
        private Transform _head;
        private Transform _neck;
        private Vector3 _currentTarget;
        private float _nextTargetAt;

        private void Awake()
        {
            Cache();
            PickNextTarget(true);
        }

        private void Reset()
        {
            Cache();
        }

        private void LateUpdate()
        {
            if (!enableLook || targetCamera == null)
            {
                return;
            }

            if (Time.time >= _nextTargetAt)
            {
                PickNextTarget(false);
            }

            var damp = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            ApplyLook(_neck, neckWeight, damp);
            ApplyLook(_head, headWeight, damp);
        }

        public void Notice()
        {
            PickNextTarget(true);
        }

        private void Cache()
        {
            targetCamera = targetCamera != null ? targetCamera : Camera.main;
            _animator = GetComponent<Animator>();

            if (_animator != null && _animator.isHuman)
            {
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
                _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            }
        }

        private void PickNextTarget(bool immediate)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            var offsetX = Random.Range(-randomLookOffset.x, randomLookOffset.x);
            var offsetY = Random.Range(-randomLookOffset.y, randomLookOffset.y);
            _currentTarget = targetCamera.transform.position
                + targetCamera.transform.right * offsetX
                + targetCamera.transform.up * offsetY;
            _nextTargetAt = Time.time + (immediate ? 0.35f : Random.Range(randomLookInterval.x, randomLookInterval.y));
        }

        private void ApplyLook(Transform bone, float weight, float damp)
        {
            if (bone == null || weight <= 0f)
            {
                return;
            }

            var desired = _currentTarget - bone.position;
            if (desired.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var delta = Quaternion.FromToRotation(bone.forward, desired.normalized);
            var angle = Quaternion.Angle(Quaternion.identity, delta);
            if (angle > maxAngle)
            {
                delta = Quaternion.Slerp(Quaternion.identity, delta, maxAngle / angle);
            }

            var targetRotation = delta * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, damp * weight);
        }
    }
}
