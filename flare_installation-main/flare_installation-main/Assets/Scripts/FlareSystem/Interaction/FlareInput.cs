using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlareSystem
{
    public static class FlareInput
    {
        public static bool TryGetPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#else
            position = Vector2.zero;
            return false;
#endif
        }

        public static bool PrimaryPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        public static bool PrimaryPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        public static bool SecondaryPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        public static bool MiddlePointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.middleButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(2);
#else
            return false;
#endif
        }

        public static Vector2 PointerDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        public static float ScrollY()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y / 120f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        public static bool AltPressed()
        {
            return GetKey(KeyCode.LeftAlt) || GetKey(KeyCode.RightAlt);
        }

        public static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Key? inputKey = ToInputSystemKey(key);
            if (inputKey.HasValue && Keyboard.current != null)
            {
                var control = Keyboard.current[inputKey.Value];
                return control != null && control.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Key? inputKey = ToInputSystemKey(key);
            if (inputKey.HasValue && Keyboard.current != null)
            {
                var control = Keyboard.current[inputKey.Value];
                return control != null && control.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Key? ToInputSystemKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.W: return Key.W;
                case KeyCode.A: return Key.A;
                case KeyCode.S: return Key.S;
                case KeyCode.D: return Key.D;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                default: return null;
            }
        }
#endif
    }
}
