using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public struct Point : IEquatable<Point>
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Point Clone()
        {
            return new Point(X, Y);
        }

        public bool Equals(Point point)
        {
            return point.X == X && point.Y == Y;
        }

        public override string ToString()
        {
            return $"{X},{Y}";
        }

        public static Point operator +(Point point1, Point point2)
        {
            return new Point(point1.X + point2.X, point1.Y + point2.Y);
        }

        public static Point operator -(Point point1, Point point2)
        {
            return new Point(point1.X - point2.X, point1.Y - point2.Y);
        }

        public static Point Zero = new Point(0, 0);

        public dynamic ToDynamic()
        {
            return new
            {
                x = X,
                y = Y
            };
        }

        public static Point? FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var x = lookup.ContainsKey("x") ? value.x as double? : null;
            if (x == null)
            {
                return null;
            }

            var y = lookup.ContainsKey("y") ? value.y as double? : null;
            if (y == null)
            {
                return null;
            }

            return new Point((int)x.Value, (int)y.Value);
        }
    }
}
