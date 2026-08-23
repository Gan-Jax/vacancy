using UnityEngine;

namespace Vacancy
{
    public sealed class GameInput
    {
        public bool Up;
        public bool Down;
        public bool Left;
        public bool Right;
        public bool InteractPressed;
        public bool PausePressed;
        public bool VacancyPressed;
        public bool ReinforcePressed;
        public bool EscapePressed;

        public void Poll()
        {
            Up = Held(KeyCode.W) || Held(KeyCode.UpArrow);
            Down = Held(KeyCode.S) || Held(KeyCode.DownArrow);
            Left = Held(KeyCode.A) || Held(KeyCode.LeftArrow);
            Right = Held(KeyCode.D) || Held(KeyCode.RightArrow);
            InteractPressed = Pressed(KeyCode.E) || Pressed(KeyCode.Space);
            PausePressed = Pressed(KeyCode.P);
            VacancyPressed = Pressed(KeyCode.V);
            ReinforcePressed = Pressed(KeyCode.R);
            EscapePressed = Pressed(KeyCode.Escape);
        }

        static bool Held(KeyCode key) => Input.GetKey(key);
        static bool Pressed(KeyCode key) => Input.GetKeyDown(key);
    }
}
