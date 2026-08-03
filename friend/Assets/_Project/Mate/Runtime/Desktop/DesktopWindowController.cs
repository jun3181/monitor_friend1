using System;
using System.Collections;
using Mate.Platform.Windows;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mate.Runtime.Desktop
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class DesktopWindowController : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField] private bool enableWindowsDesktopWindow = true;
        [SerializeField] private bool transparentBackground = true;
        [SerializeField] private bool borderless = true;
        [SerializeField] private bool fitPrimaryDisplay = true;
        [SerializeField] private bool startTopMost = true;
        [SerializeField] private bool enableClickThrough = true;
        [SerializeField] private bool hideFromAltTab;
        [SerializeField] private bool hidePreviewFloorInPlayer = true;
        [SerializeField] private Color transparentColorKey = new Color(1f, 0f, 1f, 1f);

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private DesktopInputHitTest inputHitTest;

        private IntPtr _windowHandle;
        private bool _configured;
        private bool _topMost;
        private bool _clickThrough;
        private bool _forceReceiveInput;
        private float _nextConfigureAttemptAt;

        public bool IsConfigured => _configured;
        public bool IsClickThrough => _clickThrough;
        public bool ForceReceiveInput => _forceReceiveInput;

        private IEnumerator Start()
        {
            Cache();
            Application.runInBackground = true;
            QualitySettings.antiAliasing = 0;
            ConfigureCameraForTransparency();
            HidePreviewObjectsForPlayer();

            if (!ShouldConfigureNativeWindow())
            {
                yield break;
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            TryConfigureNativeWindow();
        }

        private void Reset()
        {
            Cache();
        }

        private void Update()
        {
            HandleDebugToggles();

            if (!ShouldConfigureNativeWindow())
            {
                return;
            }

            if (!_configured)
            {
                if (Time.unscaledTime >= _nextConfigureAttemptAt)
                {
                    TryConfigureNativeWindow();
                }

                return;
            }

            UpdateClickThrough();
        }

        public void Configure(Camera camera, DesktopInputHitTest hitTest)
        {
            targetCamera = camera;
            inputHitTest = hitTest;
        }

        private void Cache()
        {
            targetCamera = targetCamera != null ? targetCamera : Camera.main;
            inputHitTest = inputHitTest != null ? inputHitTest : GetComponent<DesktopInputHitTest>();
        }

        private bool ShouldConfigureNativeWindow()
        {
            return enableWindowsDesktopWindow && WindowsDesktopWindowUtility.IsSupported;
        }

        private void ConfigureCameraForTransparency()
        {
            if (!transparentBackground || targetCamera == null)
            {
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.allowHDR = false;
            targetCamera.allowMSAA = false;
            DisableCameraPostProcessing(targetCamera);
            targetCamera.backgroundColor = WindowsDesktopWindowUtility.IsSupported
                ? transparentColorKey
                : new Color(0f, 0f, 0f, 0f);
        }

        private void HidePreviewObjectsForPlayer()
        {
            if (!hidePreviewFloorInPlayer || !WindowsDesktopWindowUtility.IsSupported)
            {
                return;
            }

            var previewFloor = GameObject.Find("Preview Floor");
            if (previewFloor != null)
            {
                previewFloor.SetActive(false);
            }

            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (var i = 0; i < volumes.Length; i++)
            {
                volumes[i].enabled = false;
            }
        }

        private static void DisableCameraPostProcessing(Camera camera)
        {
            var components = camera.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component.GetType().Name != "UniversalAdditionalCameraData")
                {
                    continue;
                }

                var property = component.GetType().GetProperty("renderPostProcessing");
                if (property != null && property.CanWrite)
                {
                    property.SetValue(component, false);
                }
            }
        }

        private void TryConfigureNativeWindow()
        {
            _nextConfigureAttemptAt = Time.unscaledTime + 0.5f;

            if (!WindowsDesktopWindowUtility.TryFindUnityWindow(out _windowHandle))
            {
                return;
            }

            _topMost = startTopMost;

            if (borderless)
            {
                WindowsDesktopWindowUtility.ApplyBorderless(_windowHandle, hideFromAltTab);
            }

            if (transparentBackground)
            {
                WindowsDesktopWindowUtility.ApplyTransparentFrame(_windowHandle, transparentColorKey);
            }

            if (fitPrimaryDisplay && WindowsDesktopWindowUtility.TryGetPrimaryDisplayBounds(out var bounds))
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.SetResolution(bounds.width, bounds.height, false);
                WindowsDesktopWindowUtility.SetWindowBounds(_windowHandle, bounds, _topMost);
            }
            else
            {
                WindowsDesktopWindowUtility.SetTopMost(_windowHandle, _topMost);
            }

            _configured = true;
            SetClickThrough(false);
            Debug.Log("DesktopWindowController configured the Windows desktop window.");
        }

        private void UpdateClickThrough()
        {
            if (!enableClickThrough || _forceReceiveInput || inputHitTest == null)
            {
                SetClickThrough(false);
                return;
            }

            var wantsInput = inputHitTest.Evaluate(_windowHandle);
            SetClickThrough(!wantsInput);
        }

        private void SetClickThrough(bool clickThrough)
        {
            if (_clickThrough == clickThrough)
            {
                return;
            }

            _clickThrough = clickThrough;
            WindowsDesktopWindowUtility.SetClickThrough(_windowHandle, clickThrough);
        }

        private void HandleDebugToggles()
        {
            if (WasF8Pressed())
            {
                _topMost = !_topMost;
                if (_configured)
                {
                    WindowsDesktopWindowUtility.SetTopMost(_windowHandle, _topMost);
                }

                Debug.Log($"DesktopWindowController top-most: {_topMost}");
            }

            if (WasF9Pressed())
            {
                _forceReceiveInput = !_forceReceiveInput;
                if (_forceReceiveInput)
                {
                    SetClickThrough(false);
                }

                Debug.Log($"DesktopWindowController force receive input: {_forceReceiveInput}");
            }

            if (WasF10Pressed())
            {
                enableClickThrough = !enableClickThrough;
                if (!enableClickThrough)
                {
                    SetClickThrough(false);
                }

                Debug.Log($"DesktopWindowController click-through enabled: {enableClickThrough}");
            }
        }

        private static bool WasF8Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F8);
#else
            return false;
#endif
        }

        private static bool WasF9Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F9);
#else
            return false;
#endif
        }

        private static bool WasF10Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F10);
#else
            return false;
#endif
        }
    }
}
