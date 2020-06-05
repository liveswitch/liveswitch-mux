using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public struct Size : IEquatable<Size>
    {
        public int Width;
        public int Height;

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

        public bool Equals(Size size)
        {
            return size.Width == Width && size.Height == Height;
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        public static Size Empty = new Size(0, 0);

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
