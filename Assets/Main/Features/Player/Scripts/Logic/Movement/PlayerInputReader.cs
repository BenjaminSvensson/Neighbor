using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Neighbor.Main.Features.Player
{
    public static class PlayerInputReader
    {
        public static PlayerFrameInput ReadFrameInput(float mouseSensitivity = 0.08f, bool invertLookY = false)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                move.x += IsPressed(keyboard.dKey) || IsPressed(keyboard.rightArrowKey) ? 1f : 0f;
                move.x -= IsPressed(keyboard.aKey) || IsPressed(keyboard.leftArrowKey) ? 1f : 0f;
                move.y += IsPressed(keyboard.wKey) || IsPressed(keyboard.upArrowKey) ? 1f : 0f;
                move.y -= IsPressed(keyboard.sKey) || IsPressed(keyboard.downArrowKey) ? 1f : 0f;
            }

            move = Vector2.ClampMagnitude(move, 1f);

            Vector2 look = mouse != null ? mouse.delta.ReadValue() * mouseSensitivity : Vector2.zero;
            float zoomDrag = mouse != null ? mouse.delta.ReadValue().y : 0f;
            if (!invertLookY)
            {
                look.y = -look.y;
            }

            return new PlayerFrameInput
            {
                Move = move,
                Look = look,
                JumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame,
                RunHeld = keyboard != null && IsPressed(keyboard.leftShiftKey),
                CrouchHeld = keyboard != null && IsPressed(keyboard.leftCtrlKey),
                LeanLeftHeld = keyboard != null && IsPressed(keyboard.zKey),
                LeanRightHeld = keyboard != null && IsPressed(keyboard.cKey),
                ZoomHeld = mouse != null && IsPressed(mouse.middleButton),
                ZoomDrag = zoomDrag,
                ZoomScroll = mouse != null ? mouse.scroll.ReadValue().y : 0f,
                CursorUnlockPressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame
            };
        }

        private static bool IsPressed(ButtonControl control)
        {
            return control != null && control.isPressed;
        }
    }

    public struct PlayerFrameInput
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool JumpPressed;
        public bool RunHeld;
        public bool CrouchHeld;
        public bool LeanLeftHeld;
        public bool LeanRightHeld;
        public bool ZoomHeld;
        public float ZoomDrag;
        public float ZoomScroll;
        public bool CursorUnlockPressed;
    }
}
