using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KingdomChaos
{
    public enum Act { Left, Right, Up, Down, Jump, Dodge, Light, Heavy, Special, Interact, Pet, Pause, Mute, Confirm }

    // Reads the keyboard and mouse through whichever input backend the project has switched on
    // (Project Settings > Player > Active Input Handling), so the game runs unchanged in an old
    // project on the Input Manager or a new Unity 6 project on the Input System package.
    public static class KCInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        static readonly KeyCode[][] Keys =
        {
            new[] { KeyCode.A, KeyCode.LeftArrow },
            new[] { KeyCode.D, KeyCode.RightArrow },
            new[] { KeyCode.W, KeyCode.UpArrow },
            new[] { KeyCode.S, KeyCode.DownArrow },
            new[] { KeyCode.Space },
            new[] { KeyCode.LeftShift, KeyCode.RightShift },
            new[] { KeyCode.J },
            new[] { KeyCode.K },
            new[] { KeyCode.Q },
            new[] { KeyCode.E },
            new[] { KeyCode.F },
            new[] { KeyCode.Escape, KeyCode.P },
            new[] { KeyCode.M },
            new[] { KeyCode.Return, KeyCode.KeypadEnter },
        };

        static bool Check(Act a, bool down)
        {
            foreach (var k in Keys[(int)a])
                if (down ? Input.GetKeyDown(k) : Input.GetKey(k)) return true;
            if (a == Act.Light) return down ? Input.GetMouseButtonDown(0) : Input.GetMouseButton(0);
            if (a == Act.Heavy) return down ? Input.GetMouseButtonDown(1) : Input.GetMouseButton(1);
            return false;
        }
#elif ENABLE_INPUT_SYSTEM
        static readonly Key[][] Keys =
        {
            new[] { Key.A, Key.LeftArrow },
            new[] { Key.D, Key.RightArrow },
            new[] { Key.W, Key.UpArrow },
            new[] { Key.S, Key.DownArrow },
            new[] { Key.Space },
            new[] { Key.LeftShift, Key.RightShift },
            new[] { Key.J },
            new[] { Key.K },
            new[] { Key.Q },
            new[] { Key.E },
            new[] { Key.F },
            new[] { Key.Escape, Key.P },
            new[] { Key.M },
            new[] { Key.Enter, Key.NumpadEnter },
        };

        static bool Check(Act a, bool down)
        {
            var kb = Keyboard.current;
            if (kb != null)
                foreach (var k in Keys[(int)a])
                {
                    var c = kb[k];
                    if (down ? c.wasPressedThisFrame : c.isPressed) return true;
                }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (a == Act.Light) return down ? mouse.leftButton.wasPressedThisFrame : mouse.leftButton.isPressed;
                if (a == Act.Heavy) return down ? mouse.rightButton.wasPressedThisFrame : mouse.rightButton.isPressed;
            }
            return false;
        }
#else
        static bool Check(Act a, bool down) => false;
#endif

        public static bool Held(Act a) => Check(a, false);
        public static bool Pressed(Act a) => Check(a, true);
    }
}
