using System;
using Mate.Platform.Windows;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mate.Runtime.Desktop
{
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class DesktopInputHitTest : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider hitCollider;
        [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private bool holdInputWhilePressed = true;
        [SerializeField] private bool receiveInputWhenUnconfigured = true;

        private bool _pressedStartedOnTarget;
        private bool _wasPressed;
        private bool _isPointerOverTarget;
        private bool _wantsInput;

        public bool IsPointerOverTarget => _isPointerOverTarget;
        public bool WantsInput => _wantsInput;

        private void Awake()
        {
            Cache();
        }

        private void Reset()
        {
            Cache();
        }

        public void Configure(Camera camera, Collider collider)
        {
            targetCamera = camera;
            hitCollider = collider;
        }

        public bool Evaluate(IntPtr hwnd)
        {
            if (targetCamera == null || (hitCollider == null && hitLayers.value == 0))
            {
                _isPointerOverTarget = false;
                _wantsInput = receiveInputWhenUnconfigured;
                return _wantsInput;
            }

            _isPointerOverTarget = TryReadPointerPosition(hwnd, out var screenPosition) && RaycastTarget(screenPosition);
            var pressed = ReadPointerPressed();

            if (pressed && !_wasPressed)
            {
                _pressedStartedOnTarget = _isPointerOverTarget;
            }
            else if (!pressed)
            {
                _pressedStartedOnTarget = false;
            }

            _wasPressed = pressed;
            _wantsInput = _isPointerOverTarget || (holdInputWhilePressed && _pressedStartedOnTarget && pressed);
            return _wantsInput;
        }

        private void Cache()
        {
            targetCamera = targetCamera != null ? targetCamera : Camera.main;
        }

        private bool TryReadPointerPosition(IntPtr hwnd, out Vector2 screenPosition)
        {
            if (WindowsDesktopWindowUtility.TryGetClientCursorPosition(hwnd, out screenPosition))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            return true;
#else
            screenPosition = default;
            return false;
#endif
        }

        private bool ReadPointerPressed()
        {
            if (WindowsDesktopWindowUtility.IsLeftMouseButtonDown())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        private bool RaycastTarget(Vector2 screenPosition)
        {
            var ray = targetCamera.ScreenPointToRay(screenPosition);
            if (hitCollider != null)
            {
                return hitCollider.Raycast(ray, out _, targetCamera.farClipPlane);
            }

            return Physics.Raycast(ray, out _, targetCamera.farClipPlane, hitLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
