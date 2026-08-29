using System.Collections.Generic;
using UnityEngine;

namespace Vacancy
{
    /// <summary>
    /// Hallway cameras aimed at each stay-room door. Feed is a RenderTexture
    /// for the office PC — the player camera is left alone.
    /// </summary>
    public sealed class CctvSystem
    {
        public const int FeedWidth = 640;
        public const int FeedHeight = 360;

        readonly HotelLayout layout;
        readonly Camera cam;
        readonly RenderTexture feed;
        readonly Dictionary<int, Housing> housings = new Dictionary<int, Housing>();
        readonly Material bodyMat;
        readonly Material lensMat;
        readonly Material idleLed;
        readonly Material liveLed;

        public RenderTexture Feed => feed;
        public int SelectedRoomId { get; private set; }
        public bool Watching { get; private set; }
        public int RoomCount => layout.Rooms != null ? layout.Rooms.Count : 0;

        public CctvSystem(HotelLayout layout, Transform parent)
        {
            this.layout = layout;
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            bodyMat = new Material(shader) { color = Palette.Hex("#2a3038") };
            lensMat = new Material(shader) { color = Palette.Hex("#1a3040") };
            idleLed = new Material(shader) { color = Palette.Hex("#6a2020") };
            liveLed = new Material(shader) { color = Palette.Hex("#e04040") };

            feed = new RenderTexture(FeedWidth, FeedHeight, 16)
            {
                name = "HallCctv",
                antiAliasing = 1
            };
            feed.Create();

            var go = new GameObject("HallCctvCamera");
            go.transform.SetParent(parent, false);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = false;
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex("#10141c");
            cam.targetTexture = feed;
            cam.depth = -20;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var listener = go.GetComponent<AudioListener>();
            if (listener != null) Object.Destroy(listener);

            var housingsRoot = new GameObject("HallCctvBoxes").transform;
            housingsRoot.SetParent(parent, false);
            if (layout.Rooms != null)
            {
                foreach (var room in layout.Rooms)
                {
                    housings[room.Id] = BuildHousing(housingsRoot, room);
                }
            }

            if (layout.RoomCount > 0) SelectRoom(1);
        }

        public void SetWatching(bool watching)
        {
            Watching = watching;
            if (cam != null) cam.enabled = watching;
            if (watching && SelectedRoomId < 1) SelectRoom(1);
            else ApplyLed();
        }

        public void SelectRoom(int roomId)
        {
            if (layout.Rooms == null || layout.Rooms.Count == 0) return;
            int max = layout.Rooms.Count;
            int id = roomId;
            if (id < 1) id = max;
            if (id > max) id = 1;
            SelectedRoomId = id;
            PoseCamera(id);
            ApplyLed();
        }

        public void SelectNext(int delta)
        {
            SelectRoom(SelectedRoomId + delta);
        }

        public string Caption(GameState state)
        {
            if (SelectedRoomId < 1 || layout.Rooms == null || SelectedRoomId > layout.Rooms.Count)
            {
                return "NO SIGNAL";
            }

            var planned = layout.Rooms[SelectedRoomId - 1];
            string walk = planned.DoorSide == "south" ? "north walk" : "west walk";
            string floor = planned.Level > 0 ? "upper" : "ground";
            string lockBit = "";
            if (state?.Rooms != null && SelectedRoomId <= state.Rooms.Count)
            {
                var room = state.Rooms[SelectedRoomId - 1];
                if (!room.Unlocked) lockBit = "  ·  locked";
                else if (room.Status == "occupied") lockBit = "  ·  occupied";
            }

            return $"CAM {SelectedRoomId:00}  Room {SelectedRoomId}  {walk}  {floor}{lockBit}";
        }

        public void Dispose()
        {
            if (cam != null) cam.targetTexture = null;
            if (feed != null)
            {
                feed.Release();
                Object.Destroy(feed);
            }
        }

        void PoseCamera(int roomId)
        {
            HallPose(layout, roomId, out var camPos, out var lookAt);
            if (cam == null) return;
            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);
        }

        Housing BuildHousing(Transform parent, PlannedRoom room)
        {
            HallPose(layout, room.Id, out var camPos, out var lookAt);
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = $"Cctv-{room.Id}";
            root.transform.SetParent(parent, false);
            Object.Destroy(root.GetComponent<Collider>());
            root.transform.position = camPos;
            root.transform.LookAt(lookAt);
            root.transform.localScale = new Vector3(0.16f, 0.1f, 0.22f);
            root.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = "Lens";
            lens.transform.SetParent(root.transform, false);
            Object.Destroy(lens.GetComponent<Collider>());
            lens.transform.localPosition = new Vector3(0f, 0f, 0.52f);
            lens.transform.localRotation = Quaternion.identity;
            lens.transform.localScale = new Vector3(0.45f, 0.45f, 0.2f);
            lens.GetComponent<Renderer>().sharedMaterial = lensMat;

            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            led.name = "Led";
            led.transform.SetParent(root.transform, false);
            Object.Destroy(led.GetComponent<Collider>());
            led.transform.localPosition = new Vector3(0.32f, 0.28f, 0.52f);
            led.transform.localRotation = Quaternion.identity;
            led.transform.localScale = new Vector3(0.14f, 0.14f, 0.12f);
            led.GetComponent<Renderer>().sharedMaterial = idleLed;

            return new Housing { Root = root.transform, Led = led.GetComponent<Renderer>() };
        }

        void ApplyLed()
        {
            foreach (var pair in housings)
            {
                bool live = Watching && pair.Key == SelectedRoomId;
                if (pair.Value.Led != null) pair.Value.Led.sharedMaterial = live ? liveLed : idleLed;
            }
        }

        public static void HallPose(HotelLayout layout, int roomId, out Vector3 cameraPos, out Vector3 lookAt)
        {
            cameraPos = Vector3.zero;
            lookAt = Vector3.forward;
            if (layout?.Rooms == null || roomId < 1 || roomId > layout.Rooms.Count) return;

            var room = layout.Rooms[roomId - 1];
            float nx = 0f;
            float ny = 1f;
            if (room.DoorOpening != null)
            {
                nx = room.DoorOpening.Normal.X;
                ny = room.DoorOpening.Normal.Y;
            }
            else if (room.DoorSide == "east")
            {
                nx = 1f;
                ny = 0f;
            }
            else if (room.DoorSide == "west")
            {
                nx = -1f;
                ny = 0f;
            }
            else if (room.DoorSide == "north")
            {
                nx = 0f;
                ny = -1f;
            }

            float hall = 50f;
            if (layout.Floor != null)
            {
                if (room.DoorSide == "east" && layout.Floor.WalkWest.W > 0f) hall = layout.Floor.WalkWest.W;
                else if (room.DoorSide == "west" && layout.Floor.WalkWest.W > 0f) hall = layout.Floor.WalkWest.W;
                else if (room.DoorSide == "south" && layout.Floor.WalkNorth.H > 0f) hall = layout.Floor.WalkNorth.H;
                else if (room.DoorSide == "north" && layout.Floor.WalkNorth.H > 0f) hall = layout.Floor.WalkNorth.H;
            }

            float camDist = hall * 0.62f;
            float doorX = room.Door.X;
            float doorY = room.Door.Y;
            float floorY = room.Level > 0 ? WorldScale.UpperFloorY : 0f;
            cameraPos = WorldScale.ToWorld(doorX + nx * camDist, doorY + ny * camDist, floorY + 2.35f);
            lookAt = WorldScale.ToWorld(doorX + nx * 6f, doorY + ny * 6f, floorY + 1.35f);
        }

        struct Housing
        {
            public Transform Root;
            public Renderer Led;
        }
    }
}
