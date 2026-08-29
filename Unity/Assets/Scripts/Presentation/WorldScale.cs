using UnityEngine;

namespace Vacancy
{
    /// <summary>
    /// Maps floorplan units onto Unity meters.
    /// Layout X → world X, layout Y → world Z, height is Y.
    /// </summary>
    public static class WorldScale
    {
        public const float UnitsPerMeter = 20f;
        public const float EyeHeight = 1.6f;
        public const float WallHeight = 2.8f;
        public const float CeilingY = 2.85f;
        public const float FloorDepth = 3.2f;
        public const float LookSensitivity = 2.4f;
        public static float BasementFloorY => -FloorDepth;
        public static float UpperFloorY => FloorDepth;

        public static Vector3 ToWorld(float layoutX, float layoutY, float height = 0f)
        {
            return new Vector3(layoutX / UnitsPerMeter, height, layoutY / UnitsPerMeter);
        }

        public static Vector3 Size(float layoutW, float worldHeight, float layoutH)
        {
            return new Vector3(layoutW / UnitsPerMeter, worldHeight, layoutH / UnitsPerMeter);
        }

        public static float Meters(float layoutUnits) => layoutUnits / UnitsPerMeter;

        public static void FromWorld(Vector3 world, out float layoutX, out float layoutY)
        {
            layoutX = world.x * UnitsPerMeter;
            layoutY = world.z * UnitsPerMeter;
        }
    }
}
