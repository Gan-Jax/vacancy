using UnityEngine;

namespace Vacancy
{
    public static class Palette
    {
        public static readonly Color Wall = Hex("#39445c");
        public static readonly Color Corridor = Hex("#4f5d78");
        public static readonly Color LobbyFloor = Hex("#6a5a48");
        public static readonly Color LobbyWall = Hex("#8a7355");
        public static readonly Color Doorway = Hex("#c4a574");
        public static readonly Color OfficeFloor = Hex("#3d4a63");
        public static readonly Color OfficeWall = Hex("#9eb6e0");
        public static readonly Color Clean = Hex("#5cb85c");
        public static readonly Color Occupied = Hex("#5b8def");
        public static readonly Color Inspect = Hex("#e6b422");
        public static readonly Color Repair = Hex("#c45c2a");
        public static readonly Color Locked = Hex("#2a3142");
        public static readonly Color Player = Hex("#6ecbff");
        public static readonly Color RadioBody = Hex("#2a2f3a");
        public static readonly Color RadioKnob = Hex("#c96545");
        public static readonly Color Paper = Hex("#d8c9a3");
        public static readonly Color HudBg = Hex("#1a1f2e");
        public static readonly Color HudPanel = Hex("#243049");
        public static readonly Color Text = Hex("#e8edf5");
        public static readonly Color Muted = Hex("#9aa8c0");
        public static readonly Color Accent = Hex("#ffd166");

        public static Color Hex(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            return Color.magenta;
        }

        public static Color FloorColor(float hour)
        {
            float h = ((hour % 24f) + 24f) % 24f;
            if (h >= 5 && h < 8) return Hex("#3a4258");
            if (h >= 8 && h < 17) return Hex("#2f384c");
            if (h >= 17 && h < 21) return Hex("#33334f");
            return Hex("#1b2133");
        }

        public static Color DirtColor(string level)
        {
            if (level == "light") return Hex("#d4925a");
            if (level == "heavy") return Hex("#8f3b2a");
            return Hex("#c96545");
        }

        public static Color RoomColor(Room room)
        {
            if (!room.Unlocked) return Locked;
            switch (room.Status)
            {
                case "clean": return Clean;
                case "dirty": return DirtColor(room.DirtLevel);
                case "occupied": return Occupied;
                case "needs_inspection": return Inspect;
                case "needs_repair": return Repair;
                default: return Color.gray;
            }
        }
    }
}
