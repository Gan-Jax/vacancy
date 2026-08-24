using UnityEngine;

namespace Vacancy
{
    public sealed class InteractHover : MonoBehaviour
    {
        public string Kind;
        public int RoomId;
        public Vector3 Anchor;

        public string Caption(GameState state = null)
        {
            switch (Kind)
            {
                case "radio": return "Radio";
                case "newspaper": return "Newspaper";
                case "phone": return "Phone";
                case "deskpc": return "Desk PC";
                case "office": return "Office PC";
                case "sign": return "Vacancy sign";
                case "desk": return "Front desk";
                case "room": return RoomCaption(state, RoomId);
                default: return Kind;
            }
        }

        static string RoomCaption(GameState state, int roomId)
        {
            if (roomId < 1) return "Room";
            if (state?.Rooms == null) return "Room " + roomId;

            Room hovered = null;
            Room nextLocked = null;
            foreach (var room in state.Rooms)
            {
                if (room.Id == roomId) hovered = room;
                if (!room.Unlocked && nextLocked == null) nextLocked = room;
            }

            if (hovered != null && !hovered.Unlocked)
            {
                if (nextLocked != null && hovered.Id != nextLocked.Id)
                {
                    return "Unlock Room " + nextLocked.Id + " first";
                }

                return "Unlock Room for $" + state.RoomUnlockCost();
            }

            return "Room " + roomId;
        }
    }
}
