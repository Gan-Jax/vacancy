using UnityEngine;

namespace Vacancy
{
    public sealed class InteractHover : MonoBehaviour
    {
        public string Kind;
        public int RoomId;
        public Vector3 Anchor;

        public string Caption()
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
                case "room": return RoomId > 0 ? "Room " + RoomId : "Room";
                default: return Kind;
            }
        }
    }
}
