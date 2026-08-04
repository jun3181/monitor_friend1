using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Mate.Runtime.Voice.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PushToTalkController : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionCoordinator coordinator;
        [SerializeField] private SpeechRecognitionProfile profile;
        [SerializeField] private KeyCode fallbackKey = KeyCode.A;

        private bool _wasPressed;

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
            if (coordinator == null)
            {
                return;
            }

            var key = profile != null ? profile.pushToTalkKey : fallbackKey;
            var pressed = IsPressed(key);

            if (pressed && !_wasPressed)
            {
                coordinator.BeginPushToTalk();
            }
            else if (!pressed && _wasPressed)
            {
                coordinator.EndPushToTalk();
            }

            _wasPressed = pressed;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ForceEndSession();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ForceEndSession();
            }
        }

        public void Configure(SpeechRecognitionCoordinator targetCoordinator, SpeechRecognitionProfile targetProfile)
        {
            coordinator = targetCoordinator;
            profile = targetProfile;
        }

        private void ForceEndSession()
        {
            if (!_wasPressed || coordinator == null)
            {
                return;
            }

            _wasPressed = false;
            coordinator.EndPushToTalk();
        }

        private void Cache()
        {
            coordinator = coordinator != null ? coordinator : GetComponent<SpeechRecognitionCoordinator>();
        }

        private static bool IsPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && TryGetKeyControl(keyboard, key, out var control))
            {
                return control.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetKeyControl(Keyboard keyboard, KeyCode key, out KeyControl control)
        {
            control = key switch
            {
                KeyCode.A => keyboard.aKey,
                KeyCode.B => keyboard.bKey,
                KeyCode.C => keyboard.cKey,
                KeyCode.D => keyboard.dKey,
                KeyCode.E => keyboard.eKey,
                KeyCode.F => keyboard.fKey,
                KeyCode.G => keyboard.gKey,
                KeyCode.H => keyboard.hKey,
                KeyCode.I => keyboard.iKey,
                KeyCode.J => keyboard.jKey,
                KeyCode.K => keyboard.kKey,
                KeyCode.L => keyboard.lKey,
                KeyCode.M => keyboard.mKey,
                KeyCode.N => keyboard.nKey,
                KeyCode.O => keyboard.oKey,
                KeyCode.P => keyboard.pKey,
                KeyCode.Q => keyboard.qKey,
                KeyCode.R => keyboard.rKey,
                KeyCode.S => keyboard.sKey,
                KeyCode.T => keyboard.tKey,
                KeyCode.U => keyboard.uKey,
                KeyCode.V => keyboard.vKey,
                KeyCode.W => keyboard.wKey,
                KeyCode.X => keyboard.xKey,
                KeyCode.Y => keyboard.yKey,
                KeyCode.Z => keyboard.zKey,
                KeyCode.Space => keyboard.spaceKey,
                KeyCode.LeftShift => keyboard.leftShiftKey,
                KeyCode.RightShift => keyboard.rightShiftKey,
                KeyCode.LeftControl => keyboard.leftCtrlKey,
                KeyCode.RightControl => keyboard.rightCtrlKey,
                _ => null
            };

            return control != null;
        }
#endif
    }
}
