using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Neighbor.Main.Features.Player
{
    public enum PlayerInputBindingAction
    {
        Forward,
        Backward,
        Left,
        Right,
        Jump,
        Run,
        Crouch,
        LeanLeft,
        LeanRight,
        Interact,
        PrimaryUse,
        SecondaryUse,
        Zoom,
        Inventory1,
        Inventory2,
        Inventory3,
        Inventory4,
        Inventory5,
        Inventory6,
        InspectHeld
    }

    public enum PlayerInputBindingDevice
    {
        Keyboard,
        Mouse
    }

    public enum PlayerMouseButton
    {
        None,
        Left,
        Right,
        Middle,
        Back,
        Forward
    }

    public readonly struct PlayerInputControlBinding : IEquatable<PlayerInputControlBinding>
    {
        private PlayerInputControlBinding(PlayerInputBindingDevice device, Key keyboardKey, PlayerMouseButton mouseButton)
        {
            Device = device;
            KeyboardKey = keyboardKey;
            MouseButton = mouseButton;
        }

        public PlayerInputBindingDevice Device { get; }
        public Key KeyboardKey { get; }
        public PlayerMouseButton MouseButton { get; }

        public static PlayerInputControlBinding ForKeyboard(Key key)
        {
            return new PlayerInputControlBinding(PlayerInputBindingDevice.Keyboard, key, PlayerMouseButton.None);
        }

        public static PlayerInputControlBinding ForMouse(PlayerMouseButton button)
        {
            return new PlayerInputControlBinding(PlayerInputBindingDevice.Mouse, Key.None, button);
        }

        public bool Equals(PlayerInputControlBinding other)
        {
            return Device == other.Device
                && KeyboardKey == other.KeyboardKey
                && MouseButton == other.MouseButton;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerInputControlBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            int control = Device == PlayerInputBindingDevice.Keyboard
                ? (int)KeyboardKey
                : (int)MouseButton;
            return ((int)Device * 397) ^ control;
        }

        public override string ToString()
        {
            return PlayerInputBindings.GetControlLabel(this);
        }
    }

    public static class PlayerInputBindings
    {
        private const string LegacyKeyboardPreferencePrefix = "Neighbor.Input.";
        private const string DevicePreferencePrefix = "Neighbor.Input.Device.";
        private const string KeyboardPreferencePrefix = "Neighbor.Input.Keyboard.";
        private const string MousePreferencePrefix = "Neighbor.Input.Mouse.";

        private static readonly PlayerInputBindingAction[] RebindableActions =
        {
            PlayerInputBindingAction.Forward,
            PlayerInputBindingAction.Backward,
            PlayerInputBindingAction.Left,
            PlayerInputBindingAction.Right,
            PlayerInputBindingAction.Jump,
            PlayerInputBindingAction.Run,
            PlayerInputBindingAction.Crouch,
            PlayerInputBindingAction.Interact,
            PlayerInputBindingAction.PrimaryUse,
            PlayerInputBindingAction.SecondaryUse,
            PlayerInputBindingAction.Zoom,
            PlayerInputBindingAction.InspectHeld,
            PlayerInputBindingAction.LeanLeft,
            PlayerInputBindingAction.LeanRight,
            PlayerInputBindingAction.Inventory1,
            PlayerInputBindingAction.Inventory2,
            PlayerInputBindingAction.Inventory3,
            PlayerInputBindingAction.Inventory4,
            PlayerInputBindingAction.Inventory5,
            PlayerInputBindingAction.Inventory6
        };

        public static PlayerInputBindingAction[] GetRebindableActions()
        {
            return (PlayerInputBindingAction[])RebindableActions.Clone();
        }

        public static bool IsPressed(PlayerInputBindingAction action)
        {
            return IsPressed(GetBinding(action));
        }

        public static bool WasPressedThisFrame(PlayerInputBindingAction action)
        {
            return WasPressedThisFrame(GetBinding(action));
        }

        public static bool WasReleasedThisFrame(PlayerInputBindingAction action)
        {
            return WasReleasedThisFrame(GetBinding(action));
        }

        public static PlayerInputControlBinding GetBinding(PlayerInputBindingAction action)
        {
            PlayerInputControlBinding defaultBinding = GetDefaultBinding(action);
            string devicePreferenceKey = GetDevicePreferenceKey(action);
            if (PlayerPrefs.HasKey(devicePreferenceKey))
            {
                PlayerInputBindingDevice device = (PlayerInputBindingDevice)PlayerPrefs.GetInt(
                    devicePreferenceKey,
                    (int)defaultBinding.Device);
                PlayerInputControlBinding savedBinding = device == PlayerInputBindingDevice.Mouse
                    ? PlayerInputControlBinding.ForMouse((PlayerMouseButton)PlayerPrefs.GetInt(
                        GetMousePreferenceKey(action),
                        (int)defaultBinding.MouseButton))
                    : PlayerInputControlBinding.ForKeyboard((Key)PlayerPrefs.GetInt(
                        GetKeyboardPreferenceKey(action),
                        (int)defaultBinding.KeyboardKey));

                if (IsBindableControl(savedBinding))
                {
                    return savedBinding;
                }
            }

            string legacyPreferenceKey = GetLegacyKeyboardPreferenceKey(action);
            if (PlayerPrefs.HasKey(legacyPreferenceKey))
            {
                PlayerInputControlBinding legacyBinding = PlayerInputControlBinding.ForKeyboard(
                    (Key)PlayerPrefs.GetInt(legacyPreferenceKey, (int)defaultBinding.KeyboardKey));
                if (IsBindableControl(legacyBinding))
                {
                    return legacyBinding;
                }
            }

            return defaultBinding;
        }

        public static Key GetBoundKey(PlayerInputBindingAction action)
        {
            PlayerInputControlBinding binding = GetBinding(action);
            return binding.Device == PlayerInputBindingDevice.Keyboard ? binding.KeyboardKey : Key.None;
        }

        public static bool TrySetBoundKey(PlayerInputBindingAction action, Key key)
        {
            return TrySetBinding(action, PlayerInputControlBinding.ForKeyboard(key));
        }

        public static bool TrySetBinding(PlayerInputBindingAction action, PlayerInputControlBinding binding)
        {
            if (!IsBindableControl(binding))
            {
                return false;
            }

            PlayerInputControlBinding previousBinding = GetBinding(action);
            if (previousBinding.Equals(binding))
            {
                SetRawBinding(action, binding);
                PlayerPrefs.Save();
                return true;
            }

            for (int i = 0; i < RebindableActions.Length; i++)
            {
                PlayerInputBindingAction otherAction = RebindableActions[i];
                if (otherAction == action || !GetBinding(otherAction).Equals(binding))
                {
                    continue;
                }

                SetRawBinding(otherAction, previousBinding);
                break;
            }

            SetRawBinding(action, binding);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < RebindableActions.Length; i++)
            {
                PlayerInputBindingAction action = RebindableActions[i];
                PlayerPrefs.DeleteKey(GetLegacyKeyboardPreferenceKey(action));
                PlayerPrefs.DeleteKey(GetDevicePreferenceKey(action));
                PlayerPrefs.DeleteKey(GetKeyboardPreferenceKey(action));
                PlayerPrefs.DeleteKey(GetMousePreferenceKey(action));
            }

            PlayerPrefs.Save();
        }

        public static bool TryGetPressedControlThisFrame(out PlayerInputControlBinding pressedBinding)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (KeyControl keyControl in keyboard.allKeys)
                {
                    if (keyControl == null || !keyControl.wasPressedThisFrame)
                    {
                        continue;
                    }

                    PlayerInputControlBinding binding = PlayerInputControlBinding.ForKeyboard(keyControl.keyCode);
                    if (!IsBindableControl(binding))
                    {
                        continue;
                    }

                    pressedBinding = binding;
                    return true;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                PlayerMouseButton[] buttons =
                {
                    PlayerMouseButton.Left,
                    PlayerMouseButton.Right,
                    PlayerMouseButton.Middle,
                    PlayerMouseButton.Back,
                    PlayerMouseButton.Forward
                };

                for (int i = 0; i < buttons.Length; i++)
                {
                    PlayerMouseButton button = buttons[i];
                    ButtonControl control = GetMouseButtonControl(mouse, button);
                    if (control == null || !control.wasPressedThisFrame)
                    {
                        continue;
                    }

                    pressedBinding = PlayerInputControlBinding.ForMouse(button);
                    return true;
                }
            }

            pressedBinding = default;
            return false;
        }

        public static bool TryGetPressedKeyThisFrame(out Key pressedKey)
        {
            if (TryGetPressedControlThisFrame(out PlayerInputControlBinding binding)
                && binding.Device == PlayerInputBindingDevice.Keyboard)
            {
                pressedKey = binding.KeyboardKey;
                return true;
            }

            pressedKey = Key.None;
            return false;
        }

        public static string GetActionLabel(PlayerInputBindingAction action)
        {
            return action switch
            {
                PlayerInputBindingAction.Forward => "Forward",
                PlayerInputBindingAction.Backward => "Backward",
                PlayerInputBindingAction.Left => "Left",
                PlayerInputBindingAction.Right => "Right",
                PlayerInputBindingAction.Jump => "Jump",
                PlayerInputBindingAction.Run => "Run",
                PlayerInputBindingAction.Crouch => "Crouch",
                PlayerInputBindingAction.LeanLeft => "Lean Left",
                PlayerInputBindingAction.LeanRight => "Lean Right",
                PlayerInputBindingAction.Interact => "Interact",
                PlayerInputBindingAction.PrimaryUse => "Primary Use",
                PlayerInputBindingAction.SecondaryUse => "Place/Throw",
                PlayerInputBindingAction.Zoom => "Zoom",
                PlayerInputBindingAction.InspectHeld => "Inspect Held",
                PlayerInputBindingAction.Inventory1 => "Slot 1",
                PlayerInputBindingAction.Inventory2 => "Slot 2",
                PlayerInputBindingAction.Inventory3 => "Slot 3",
                PlayerInputBindingAction.Inventory4 => "Slot 4",
                PlayerInputBindingAction.Inventory5 => "Slot 5",
                PlayerInputBindingAction.Inventory6 => "Slot 6",
                _ => action.ToString()
            };
        }

        public static string GetKeyLabel(PlayerInputBindingAction action)
        {
            return GetControlLabel(action);
        }

        public static string GetControlLabel(PlayerInputBindingAction action)
        {
            return GetControlLabel(GetBinding(action));
        }

        public static string GetControlLabel(PlayerInputControlBinding binding)
        {
            return binding.Device == PlayerInputBindingDevice.Mouse
                ? GetMouseButtonLabel(binding.MouseButton)
                : GetKeyLabel(binding.KeyboardKey);
        }

        public static string GetKeyLabel(Key key)
        {
            return key switch
            {
                Key.LeftShift => "L SHIFT",
                Key.RightShift => "R SHIFT",
                Key.LeftCtrl => "L CTRL",
                Key.RightCtrl => "R CTRL",
                Key.LeftAlt => "L ALT",
                Key.RightAlt => "R ALT",
                Key.LeftArrow => "LEFT",
                Key.RightArrow => "RIGHT",
                Key.UpArrow => "UP",
                Key.DownArrow => "DOWN",
                Key.Space => "SPACE",
                Key.Backquote => "`",
                Key.Semicolon => ";",
                Key.Quote => "'",
                Key.Comma => ",",
                Key.Period => ".",
                Key.Slash => "/",
                Key.Backslash => "\\",
                Key.LeftBracket => "[",
                Key.RightBracket => "]",
                Key.Minus => "-",
                Key.Equals => "=",
                _ => FormatKeyName(key.ToString())
            };
        }

        private static PlayerInputControlBinding GetDefaultBinding(PlayerInputBindingAction action)
        {
            return action switch
            {
                PlayerInputBindingAction.Forward => PlayerInputControlBinding.ForKeyboard(Key.W),
                PlayerInputBindingAction.Backward => PlayerInputControlBinding.ForKeyboard(Key.S),
                PlayerInputBindingAction.Left => PlayerInputControlBinding.ForKeyboard(Key.A),
                PlayerInputBindingAction.Right => PlayerInputControlBinding.ForKeyboard(Key.D),
                PlayerInputBindingAction.Jump => PlayerInputControlBinding.ForKeyboard(Key.Space),
                PlayerInputBindingAction.Run => PlayerInputControlBinding.ForKeyboard(Key.LeftShift),
                PlayerInputBindingAction.Crouch => PlayerInputControlBinding.ForKeyboard(Key.LeftCtrl),
                PlayerInputBindingAction.LeanLeft => PlayerInputControlBinding.ForKeyboard(Key.Q),
                PlayerInputBindingAction.LeanRight => PlayerInputControlBinding.ForKeyboard(Key.R),
                PlayerInputBindingAction.Interact => PlayerInputControlBinding.ForKeyboard(Key.E),
                PlayerInputBindingAction.PrimaryUse => PlayerInputControlBinding.ForMouse(PlayerMouseButton.Left),
                PlayerInputBindingAction.SecondaryUse => PlayerInputControlBinding.ForMouse(PlayerMouseButton.Right),
                PlayerInputBindingAction.Zoom => PlayerInputControlBinding.ForMouse(PlayerMouseButton.Middle),
                PlayerInputBindingAction.InspectHeld => PlayerInputControlBinding.ForKeyboard(Key.F),
                PlayerInputBindingAction.Inventory1 => PlayerInputControlBinding.ForKeyboard(Key.Digit1),
                PlayerInputBindingAction.Inventory2 => PlayerInputControlBinding.ForKeyboard(Key.Digit2),
                PlayerInputBindingAction.Inventory3 => PlayerInputControlBinding.ForKeyboard(Key.Digit3),
                PlayerInputBindingAction.Inventory4 => PlayerInputControlBinding.ForKeyboard(Key.Digit4),
                PlayerInputBindingAction.Inventory5 => PlayerInputControlBinding.ForKeyboard(Key.Digit5),
                PlayerInputBindingAction.Inventory6 => PlayerInputControlBinding.ForKeyboard(Key.Digit6),
                _ => PlayerInputControlBinding.ForKeyboard(Key.None)
            };
        }

        private static bool IsBindableControl(PlayerInputControlBinding binding)
        {
            return binding.Device == PlayerInputBindingDevice.Mouse
                ? binding.MouseButton != PlayerMouseButton.None
                : IsBindableKey(binding.KeyboardKey);
        }

        private static bool IsBindableKey(Key key)
        {
            return key != Key.None && key != Key.Escape;
        }

        private static bool IsPressed(PlayerInputControlBinding binding)
        {
            ButtonControl control = GetButtonControl(binding);
            return control != null && control.isPressed;
        }

        private static bool WasPressedThisFrame(PlayerInputControlBinding binding)
        {
            ButtonControl control = GetButtonControl(binding);
            return control != null && control.wasPressedThisFrame;
        }

        private static bool WasReleasedThisFrame(PlayerInputControlBinding binding)
        {
            ButtonControl control = GetButtonControl(binding);
            return control != null && control.wasReleasedThisFrame;
        }

        private static ButtonControl GetButtonControl(PlayerInputControlBinding binding)
        {
            if (binding.Device == PlayerInputBindingDevice.Mouse)
            {
                Mouse mouse = Mouse.current;
                return mouse != null ? GetMouseButtonControl(mouse, binding.MouseButton) : null;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null ? keyboard[binding.KeyboardKey] : null;
        }

        private static ButtonControl GetMouseButtonControl(Mouse mouse, PlayerMouseButton button)
        {
            return button switch
            {
                PlayerMouseButton.Left => mouse.leftButton,
                PlayerMouseButton.Right => mouse.rightButton,
                PlayerMouseButton.Middle => mouse.middleButton,
                PlayerMouseButton.Back => mouse.backButton,
                PlayerMouseButton.Forward => mouse.forwardButton,
                _ => null
            };
        }

        private static string GetMouseButtonLabel(PlayerMouseButton button)
        {
            return button switch
            {
                PlayerMouseButton.Left => "L MOUSE",
                PlayerMouseButton.Right => "R MOUSE",
                PlayerMouseButton.Middle => "M MOUSE",
                PlayerMouseButton.Back => "MOUSE BACK",
                PlayerMouseButton.Forward => "MOUSE FWD",
                _ => "UNBOUND"
            };
        }

        private static void SetRawBinding(PlayerInputBindingAction action, PlayerInputControlBinding binding)
        {
            PlayerPrefs.SetInt(GetDevicePreferenceKey(action), (int)binding.Device);
            if (binding.Device == PlayerInputBindingDevice.Mouse)
            {
                PlayerPrefs.SetInt(GetMousePreferenceKey(action), (int)binding.MouseButton);
                PlayerPrefs.DeleteKey(GetKeyboardPreferenceKey(action));
                PlayerPrefs.DeleteKey(GetLegacyKeyboardPreferenceKey(action));
                return;
            }

            PlayerPrefs.SetInt(GetKeyboardPreferenceKey(action), (int)binding.KeyboardKey);
            PlayerPrefs.SetInt(GetLegacyKeyboardPreferenceKey(action), (int)binding.KeyboardKey);
            PlayerPrefs.DeleteKey(GetMousePreferenceKey(action));
        }

        private static string GetLegacyKeyboardPreferenceKey(PlayerInputBindingAction action)
        {
            return LegacyKeyboardPreferencePrefix + action;
        }

        private static string GetDevicePreferenceKey(PlayerInputBindingAction action)
        {
            return DevicePreferencePrefix + action;
        }

        private static string GetKeyboardPreferenceKey(PlayerInputBindingAction action)
        {
            return KeyboardPreferencePrefix + action;
        }

        private static string GetMousePreferenceKey(PlayerInputBindingAction action)
        {
            return MousePreferencePrefix + action;
        }

        private static string FormatKeyName(string keyName)
        {
            if (keyName.StartsWith("Digit", StringComparison.Ordinal))
            {
                return keyName.Substring("Digit".Length);
            }

            if (keyName.StartsWith("Numpad", StringComparison.Ordinal))
            {
                return "NUM " + keyName.Substring("Numpad".Length).ToUpperInvariant();
            }

            return keyName.ToUpperInvariant();
        }
    }
}
