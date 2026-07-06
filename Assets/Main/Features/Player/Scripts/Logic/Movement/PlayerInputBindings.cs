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
        Interact
    }

    public static class PlayerInputBindings
    {
        private const string PreferencePrefix = "Neighbor.Input.";

        private static readonly PlayerInputBindingAction[] RebindableActions =
        {
            PlayerInputBindingAction.Forward,
            PlayerInputBindingAction.Backward,
            PlayerInputBindingAction.Left,
            PlayerInputBindingAction.Right,
            PlayerInputBindingAction.Jump,
            PlayerInputBindingAction.Run,
            PlayerInputBindingAction.Crouch,
            PlayerInputBindingAction.LeanLeft,
            PlayerInputBindingAction.LeanRight,
            PlayerInputBindingAction.Interact
        };

        public static PlayerInputBindingAction[] GetRebindableActions()
        {
            return (PlayerInputBindingAction[])RebindableActions.Clone();
        }

        public static bool IsPressed(PlayerInputBindingAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && IsPressed(keyboard, GetBoundKey(action));
        }

        public static bool WasPressedThisFrame(PlayerInputBindingAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && WasPressedThisFrame(keyboard, GetBoundKey(action));
        }

        public static Key GetBoundKey(PlayerInputBindingAction action)
        {
            Key defaultKey = GetDefaultKey(action);
            Key savedKey = (Key)PlayerPrefs.GetInt(GetPreferenceKey(action), (int)defaultKey);
            return IsBindableKey(savedKey) ? savedKey : defaultKey;
        }

        public static bool TrySetBoundKey(PlayerInputBindingAction action, Key key)
        {
            if (!IsBindableKey(key))
            {
                return false;
            }

            Key previousKey = GetBoundKey(action);
            if (previousKey == key)
            {
                SetRawBoundKey(action, key);
                PlayerPrefs.Save();
                return true;
            }

            for (int i = 0; i < RebindableActions.Length; i++)
            {
                PlayerInputBindingAction otherAction = RebindableActions[i];
                if (otherAction == action || GetBoundKey(otherAction) != key)
                {
                    continue;
                }

                SetRawBoundKey(otherAction, previousKey);
                break;
            }

            SetRawBoundKey(action, key);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < RebindableActions.Length; i++)
            {
                PlayerPrefs.DeleteKey(GetPreferenceKey(RebindableActions[i]));
            }

            PlayerPrefs.Save();
        }

        public static bool TryGetPressedKeyThisFrame(out Key pressedKey)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                pressedKey = Key.None;
                return false;
            }

            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (keyControl == null || !keyControl.wasPressedThisFrame)
                {
                    continue;
                }

                Key key = keyControl.keyCode;
                if (!IsBindableKey(key))
                {
                    continue;
                }

                pressedKey = key;
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
                _ => action.ToString()
            };
        }

        public static string GetKeyLabel(PlayerInputBindingAction action)
        {
            return GetKeyLabel(GetBoundKey(action));
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

        private static Key GetDefaultKey(PlayerInputBindingAction action)
        {
            return action switch
            {
                PlayerInputBindingAction.Forward => Key.W,
                PlayerInputBindingAction.Backward => Key.S,
                PlayerInputBindingAction.Left => Key.A,
                PlayerInputBindingAction.Right => Key.D,
                PlayerInputBindingAction.Jump => Key.Space,
                PlayerInputBindingAction.Run => Key.LeftShift,
                PlayerInputBindingAction.Crouch => Key.LeftCtrl,
                PlayerInputBindingAction.LeanLeft => Key.Q,
                PlayerInputBindingAction.LeanRight => Key.R,
                PlayerInputBindingAction.Interact => Key.E,
                _ => Key.None
            };
        }

        private static bool IsBindableKey(Key key)
        {
            return key != Key.None && key != Key.Escape;
        }

        private static bool IsPressed(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.isPressed;
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static void SetRawBoundKey(PlayerInputBindingAction action, Key key)
        {
            PlayerPrefs.SetInt(GetPreferenceKey(action), (int)key);
        }

        private static string GetPreferenceKey(PlayerInputBindingAction action)
        {
            return PreferencePrefix + action;
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
