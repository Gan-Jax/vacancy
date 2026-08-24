namespace Vacancy
{
    /// <summary>
    /// Runtime look/control options. The pause Settings page writes these.
    /// </summary>
    public static class PlayerSettings
    {
        public const float DefaultLookSensitivity = 2.4f;
        public const float MinLookSensitivity = 0.6f;
        public const float MaxLookSensitivity = 6f;
        public const float LookSensitivityStep = 0.2f;

        public static float LookSensitivity = DefaultLookSensitivity;
        public static bool InvertY;

        public static void NudgeLookSensitivity(float delta)
        {
            LookSensitivity = Geometry.Clamp(
                LookSensitivity + delta,
                MinLookSensitivity,
                MaxLookSensitivity);
        }

        public static string LookSensitivityLabel()
        {
            return LookSensitivity.ToString("0.0");
        }
    }
}
