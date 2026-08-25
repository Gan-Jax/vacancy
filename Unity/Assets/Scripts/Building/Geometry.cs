namespace Vacancy
{
    [System.Serializable]
    public struct Point
    {
        public float X;
        public float Y;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public struct Rect
    {
        public float X;
        public float Y;
        public float W;
        public float H;

        public Rect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public Point Center => new Point(X + W / 2f, Y + H / 2f);

        public bool Contains(float px, float py, float pad = 0f)
        {
            return px >= X + pad && px <= X + W - pad && py >= Y + pad && py <= Y + H - pad;
        }

        public bool Contains(Point point, float pad = 0f) => Contains(point.X, point.Y, pad);
    }

    public sealed class Door
    {
        public string Side;
        public float Width;
        public Point Center;
        public Point Normal;
    }

    public static class Geometry
    {
        public static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        public static float Dist(Point a, Point b) => Dist(a.X, a.Y, b.X, b.Y);

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int FloorToTile(float value, float tile)
        {
            return (int)System.Math.Floor(value / tile) * (int)tile;
        }
    }
}
