using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public struct Size : IEquatable<Size>
    {
        public int Width { get; }
        public int Height { get; }

        [JsonIgnore]
        public int PixelCount { get { return Width * Height; } }

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public Size Clone()
        {
            return new Size(Width, Height);
        }

        public override bool Equals(object obj)
        {
            var size = obj as Size?;
            if (size == null)
            {
                return false;
            }
            return Equals(size.Value);
        }

        public override int GetHashCode()
        {
            var hash = 13;
            hash = (hash * 7) + Width.GetHashCode();
            hash = (hash * 7) + Height.GetHashCode();
            return hash;
        }

        public bool Equals(Size obj)
        {
            return obj.Width == Width && obj.Height == Height;
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        public static Size Empty { get; } = new Size(0, 0);

        public static bool operator ==(Size size1, Size size2)
        {
            return size1.Equals(size2);
        }
        public static bool operator !=(Size size1, Size size2)
        {
            return !size1.Equals(size2);
        }

        public static Size operator *(Size size, double value)
        {
            return new Size((int)(size.Width * value), (int)(size.Height * value));
        }

        public static Size operator /(Size size, double value)
        {
            return new Size((int)(size.Width / value), (int)(size.Height / value));
        }

        public dynamic ToDynamic()
        {
            return new
            {
                width = Width,
                height = Height
            };
        }

        public static Size? FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var width = lookup.ContainsKey("width") ? value.width as double? : null;
            if (width == null)
            {
                return null;
            }

            var height = lookup.ContainsKey("height") ? value.height as double? : null;
            if (height == null)
            {
                return null;
            }

            return new Size((int)width.Value, (int)height.Value);
        }
    }
}
