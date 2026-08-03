using Mate.Runtime.Core;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mate.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MateInteractionController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider hitCollider;
        [SerializeField] private MateController controller;
        [SerializeField] private MateDragController dragController;
        [SerializeField] private float dragThresholdPixels = 12f;
        [SerializeField] private float clickMaxDuration = 0.35f;
        [SerializeField] private float strokeMinDuration = 0.45f;
        [SerializeField] private float strokeMinDistancePixels = 42f;
        [SerializeField] private float strokeCooldown = 0.8f;

        private bool _pressedOnMate;
        private bool _dragging;
        private bool _strokeFired;
        private Vector2 _pressPosition;
        private Vector2 _lastPosition;
        private float _pressStartedAt;
        private float _travelDistance;
        private float _lastStrokeAt = -999f;

        private void Awake()
        {
            Cache();
        }

        private void Reset()
        {
            Cache();
        }

        private void Update()
        {
            if (!TryReadPointer(out var pointer))
            {
                return;
            }

            if (pointer.Down)
            {
                BeginPress(pointer.Position);
            }

            if (_pressedOnMate && pointer.Pressed)
            {
                ContinuePress(pointer.Position);
            }

            if (_pressedOnMate && pointer.Up)
            {
                EndPress(pointer.Position);
            }
        }

        private void BeginPress(Vector2 screenPosition)
        {
            if (!IsPointerOverMate(screenPosition))
            {
                return;
            }

            _pressedOnMate = true;
            _dragging = false;
            _strokeFired = false;
            _pressPosition = screenPosition;
            _lastPosition = screenPosition;
            _pressStartedAt = Time.time;
            _travelDistance = 0f;
        }

        private void ContinuePress(Vector2 screenPosition)
        {
            var frameDistance = Vector2.Distance(screenPosition, _lastPosition);
            _travelDistance += frameDistance;
            _lastPosition = screenPosition;

            var fromPress = Vector2.Distance(screenPosition, _pressPosition);
            if (!_dragging && fromPress >= dragThresholdPixels)
            {
                _dragging = true;
                dragController?.BeginDrag(screenPosition);
            }

            if (_dragging)
            {
                dragController?.UpdateDrag(screenPosition);
                return;
            }

            var heldFor = Time.time - _pressStartedAt;
            if (!_strokeFired
                && heldFor >= strokeMinDuration
                && _travelDistance >= strokeMinDistancePixels
                && Time.time - _lastStrokeAt >= strokeCooldown
                && IsPointerOverMate(screenPosition))
            {
                _strokeFired = true;
                _lastStrokeAt = Time.time;
                controller?.React(MateReactionType.Stroke);
            }
        }

        private void EndPress(Vector2 screenPosition)
        {
            if (_dragging)
            {
                dragController?.EndDrag();
            }
            else
            {
                var heldFor = Time.time - _pressStartedAt;
                var fromPress = Vector2.Distance(screenPosition, _pressPosition);
                if (!_strokeFired && heldFor <= clickMaxDuration && fromPress < dragThresholdPixels)
                {
                    controller?.React(MateReactionType.Click);
                }
            }

            _pressedOnMate = false;
            _dragging = false;
        }

        private bool IsPointerOverMate(Vector2 screenPosition)
        {
            if (targetCamera == null || hitCollider == null)
            {
                return false;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            return hitCollider.Raycast(ray, out _, targetCamera.farClipPlane);
        }

        private void Cache()
        {
            targetCamera = targetCamera != null ? targetCamera : Camera.main;
            controller = controller != null ? controller : GetComponent<MateController>();
            dragController = dragController != null ? dragController : GetComponent<MateDragController>();
            hitCollider = hitCollider != null ? hitCollider : GetComponent<Collider>();
        }

        private bool TryReadPointer(out PointerSnapshot pointer)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointer = new PointerSnapshot(
                    mouse.position.ReadValue(),
                    mouse.leftButton.isPressed,
                    mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.wasReleasedThisFrame);
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pointer = new PointerSnapshot(
                Input.mousePosition,
                Input.GetMouseButton(0),
                Input.GetMouseButtonDown(0),
                Input.GetMouseButtonUp(0));
            return true;
#else
            pointer = default;
            return false;
#endif
        }

        private readonly struct PointerSnapshot
        {
            public readonly Vector2 Position;
            public readonly bool Pressed;
            public readonly bool Down;
            public readonly bool Up;

            public PointerSnapshot(Vector2 position, bool pressed, bool down, bool up)
            {
                Position = position;
                Pressed = pressed;
                Down = down;
                Up = up;
            }
        }
    }
}
