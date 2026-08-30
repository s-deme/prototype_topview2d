using System;
using System.Collections.Generic;
using UnityEngine;

namespace VerdantBlade
{
    public enum GameAction
    {
        None,
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Attack,
        Dash,
        Pause
    }

    /// <summary>
    /// Central input layer for keyboard, mouse, and a conventional Unity gamepad.
    /// Primary keyboard bindings are local, persistent, and can be changed in Settings.
    /// </summary>
    public static class GameInput
    {
        private static readonly GameAction[] RebindableActions =
        {
            GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft, GameAction.MoveRight,
            GameAction.Attack, GameAction.Dash, GameAction.Pause
        };
        private static readonly Dictionary<GameAction, KeyCode> bindings = new Dictionary<GameAction, KeyCode>();
        private static bool loaded;

        public static Vector2 Move
        {
            get
            {
                var keys = new Vector2(
                    (IsHeld(GameAction.MoveRight) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (IsHeld(GameAction.MoveLeft) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                    (IsHeld(GameAction.MoveUp) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (IsHeld(GameAction.MoveDown) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
                var gamepad = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return Vector2.ClampMagnitude(gamepad.sqrMagnitude > keys.sqrMagnitude ? gamepad : keys, 1f);
            }
        }

        public static bool PrimaryPressed => IsPressed(GameAction.Attack) || Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool DashPressed => IsPressed(GameAction.Dash) || Input.GetKeyDown(KeyCode.X) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool PausePressed => IsPressed(GameAction.Pause) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool MenuConfirmPressed => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool MenuBackPressed => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool MenuUpPressed => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton13);
        public static bool MenuDownPressed => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton14);

        public static IEnumerable<GameAction> Actions => RebindableActions;

        public static KeyCode GetBinding(GameAction action)
        {
            EnsureLoaded();
            return bindings[action];
        }

        public static string BindingLabel(GameAction action)
        {
            return KeyLabel(GetBinding(action));
        }

        public static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return "スペース";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.LeftShift: return "左Shift";
                case KeyCode.RightShift: return "右Shift";
                case KeyCode.LeftControl: return "左Ctrl";
                case KeyCode.RightControl: return "右Ctrl";
                case KeyCode.LeftAlt: return "左Alt";
                case KeyCode.RightAlt: return "右Alt";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.Mouse0: return "左クリック";
                case KeyCode.Mouse1: return "右クリック";
                default: return key.ToString();
            }
        }

        public static bool IsBoundElsewhere(GameAction action, KeyCode key)
        {
            EnsureLoaded();
            foreach (var pair in bindings)
            {
                if (pair.Key != action && pair.Value == key)
                {
                    return true;
                }
            }
            return false;
        }

        public static void SetBinding(GameAction action, KeyCode key)
        {
            if (action == GameAction.None || IsBoundElsewhere(action, key))
            {
                return;
            }
            bindings[action] = key;
            PlayerProfile.SaveBinding(action, key);
        }

        public static void ResetBindings()
        {
            bindings.Clear();
            loaded = false;
            PlayerProfile.ResetBindings();
            EnsureLoaded();
        }

        public static bool TryCaptureKeyboardKey(out KeyCode key)
        {
            foreach (KeyCode candidate in Enum.GetValues(typeof(KeyCode)))
            {
                if ((int)candidate >= (int)KeyCode.Backspace && (int)candidate <= (int)KeyCode.Menu && Input.GetKeyDown(candidate))
                {
                    key = candidate;
                    return true;
                }
            }
            key = KeyCode.None;
            return false;
        }

        public static bool TryGetPointerDirection(Vector2 origin, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!Input.mousePresent || Camera.main == null)
            {
                return false;
            }

            var pointer = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            direction = (Vector2)pointer - origin;
            return direction.sqrMagnitude > 0.04f;
        }

        private static bool IsPressed(GameAction action)
        {
            return Input.GetKeyDown(GetBinding(action));
        }

        private static bool IsHeld(GameAction action)
        {
            return Input.GetKey(GetBinding(action));
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            foreach (var action in RebindableActions)
            {
                bindings[action] = PlayerProfile.LoadBinding(action, DefaultBinding(action));
            }
            loaded = true;
        }

        private static KeyCode DefaultBinding(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveUp: return KeyCode.W;
                case GameAction.MoveDown: return KeyCode.S;
                case GameAction.MoveLeft: return KeyCode.A;
                case GameAction.MoveRight: return KeyCode.D;
                case GameAction.Attack: return KeyCode.Space;
                case GameAction.Dash: return KeyCode.LeftShift;
                case GameAction.Pause: return KeyCode.Escape;
                default: return KeyCode.None;
            }
        }
    }
}
