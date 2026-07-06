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
                move.x += PlayerInputBindings.IsPressed(PlayerInputBindingAction.Right) || IsPressed(keyboard.rightArrowKey) ? 1f : 0f;
                move.x -= PlayerInputBindings.IsPressed(PlayerInputBindingAction.Left) || IsPressed(keyboard.leftArrowKey) ? 1f : 0f;
                move.y += PlayerInputBindings.IsPressed(PlayerInputBindingAction.Forward) || IsPressed(keyboard.upArrowKey) ? 1f : 0f;
                move.y -= PlayerInputBindings.IsPressed(PlayerInputBindingAction.Backward) || IsPressed(keyboard.downArrowKey) ? 1f : 0f;
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
                JumpPressed = PlayerInputBindings.WasPressedThisFrame(PlayerInputBindingAction.Jump),
                RunHeld = PlayerInputBindings.IsPressed(PlayerInputBindingAction.Run),
                CrouchHeld = PlayerInputBindings.IsPressed(PlayerInputBindingAction.Crouch),
                CrouchPressed = PlayerInputBindings.WasPressedThisFrame(PlayerInputBindingAction.Crouch),
                LeanLeftHeld = PlayerInputBindings.IsPressed(PlayerInputBindingAction.LeanLeft),
                LeanRightHeld = PlayerInputBindings.IsPressed(PlayerInputBindingAction.LeanRight),
                ZoomHeld = PlayerInputBindings.IsPressed(PlayerInputBindingAction.Zoom),
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
        public bool CrouchPressed;
        public bool LeanLeftHeld;
        public bool LeanRightHeld;
        public bool ZoomHeld;
        public float ZoomDrag;
        public float ZoomScroll;
        public bool CursorUnlockPressed;
    }
}
