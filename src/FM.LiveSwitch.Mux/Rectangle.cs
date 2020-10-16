using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public struct Rectangle : IEquatable<Rectangle>
    {
        public Point Origin { get; }
        public Size Size { get; }

        public Rectangle(Point origin, Size size)
        {
            Origin = origin;
            Size = size;
        }

        public Rectangle Clone()
        {
            return new Rectangle(Origin.Clone(), Size.Clone());
        }

        public bool Equals(Rectangle obj)
        {
            return obj.Origin.Equals(Origin) && obj.Size.Equals(Size);
        }

        public override string ToString()
        {
            return $"{Origin}:{Size}";
        }

        public static Rectangle Empty { get; } = new Rectangle(Point.Zero, Size.Empty);

        public dynamic ToDynamic()
        {
            return new
            {
                origin = Origin.ToDynamic(),
                size = Size.ToDynamic()
            };
        }

        public static Rectangle? FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var origin = lookup.ContainsKey("origin") ? Point.FromDynamic(value.origin) as Point? : null;
            if (origin == null)
            {
                return null;
            }

            var size = lookup.ContainsKey("size") ? Size.FromDynamic(value.size) as Size? : null;
            if (size == null)
            {
                return null;
            }

            return new Rectangle(origin.Value, size.Value);
        }
    }
}
