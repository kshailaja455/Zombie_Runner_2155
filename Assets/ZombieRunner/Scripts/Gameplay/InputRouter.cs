using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZombieRunner
{
    public enum InputCommand { Left, Right, Jump, Slide, Pause }

    /// <summary>
    /// Keyboard + swipe input. Uses the Input System package when it is the active input handler
    /// (default in Unity 6 URP projects) and falls back to the legacy Input Manager otherwise.
    /// Keyboard: A/Left, D/Right, W/Up/Space jump, S/Down slide, Escape pause.
    /// Touch: swipe left/right/up/down. Mouse drag also swipes (handy for testing mobile controls in the Editor).
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public event Action<InputCommand> OnCommand;

        public float swipeThresholdInches = 0.25f;
        public float maxSwipeTime = 0.6f;

        Vector2 start;
        float startTime;
        bool tracking;

        float SwipePixels => Mathf.Max(40f, (Screen.dpi > 0 ? Screen.dpi : 160f) * swipeThresholdInches);

        void Fire(InputCommand c) => OnCommand?.Invoke(c);

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Fire(InputCommand.Left);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Fire(InputCommand.Right);
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Fire(InputCommand.Jump);
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Fire(InputCommand.Slide);
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) Fire(InputCommand.Pause);
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.left.wasPressedThisFrame) Fire(InputCommand.Left);
                if (gp.dpad.right.wasPressedThisFrame) Fire(InputCommand.Right);
                if (gp.buttonSouth.wasPressedThisFrame || gp.dpad.up.wasPressedThisFrame) Fire(InputCommand.Jump);
                if (gp.buttonEast.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame) Fire(InputCommand.Slide);
                if (gp.startButton.wasPressedThisFrame) Fire(InputCommand.Pause);
            }
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var t = ts.primaryTouch;
                if (t.press.wasPressedThisFrame) Begin(t.position.ReadValue());
                else if (t.press.isPressed) Track(t.position.ReadValue());
                else if (t.press.wasReleasedThisFrame) tracking = false;
            }
            else
            {
                var m = Mouse.current;
                if (m != null)
                {
                    if (m.leftButton.wasPressedThisFrame) Begin(m.position.ReadValue());
                    else if (m.leftButton.isPressed) Track(m.position.ReadValue());
                    else if (m.leftButton.wasReleasedThisFrame) tracking = false;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Fire(InputCommand.Left);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Fire(InputCommand.Right);
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space)) Fire(InputCommand.Jump);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Fire(InputCommand.Slide);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) Fire(InputCommand.Pause);
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) Begin(t.position);
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) Track(t.position);
                else tracking = false;
            }
            else
            {
                if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition);
                else if (Input.GetMouseButton(0)) Track(Input.mousePosition);
                else if (Input.GetMouseButtonUp(0)) tracking = false;
            }
#endif
        }

        void Begin(Vector2 pos)
        {
            // Ignore touches that start on the top HUD strip (pause button area).
            if (pos.y > Screen.height * 0.86f) { tracking = false; return; }
            start = pos;
            startTime = Time.unscaledTime;
            tracking = true;
        }

        void Track(Vector2 pos)
        {
            if (!tracking) return;
            if (Time.unscaledTime - startTime > maxSwipeTime) { tracking = false; return; }
            var d = pos - start;
            if (d.magnitude < SwipePixels) return;
            tracking = false; // one command per swipe
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) Fire(d.x < 0 ? InputCommand.Left : InputCommand.Right);
            else Fire(d.y > 0 ? InputCommand.Jump : InputCommand.Slide);
        }
    }
}
