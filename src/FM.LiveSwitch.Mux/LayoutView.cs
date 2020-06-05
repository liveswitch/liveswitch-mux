using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public class LayoutView
    {
        public Rectangle Frame { get; set; }

        public Rectangle Bounds { get; set; }

        public LayoutView(Rectangle frame)
        {
            Frame = frame;
            Bounds = Rectangle.Empty;
        }

        public LayoutView(Rectangle frame, Rectangle bounds)
        {
            Frame = frame;
            Bounds = bounds;
        }

        public string GetSizeFilterChain(string inputTag, string outputTag)
        {
            return $"{inputTag}scale=w={Bounds.Size.Width}:h={Bounds.Size.Height}{outputTag}";
        }

        public string GetCropFilterChain(string inputTag, string outputTag)
        {
            return $"{inputTag}crop=w={Frame.Size.Width}:h={Frame.Size.Height}:x={-Bounds.Origin.X}:y={-Bounds.Origin.Y}{outputTag}";
        }

        public string GetOverlayChain(bool crop, string baseTag, string inputTag, string outputTag)
        {
            var origin = crop ? Frame.Origin : Frame.Origin + Bounds.Origin;
            return $"{baseTag}{inputTag}overlay=x={origin.X}:y={origin.Y}{outputTag}";
        }

        public double ScaleBounds(bool crop)
        {
            if (Bounds.Size.Width == 0)
            {
                throw new Exception("Cannot scale bounds with zero width.");
            }

            if (Bounds.Size.Height == 0)
            {
                throw new Exception("Cannot scale bounds with zero height.");
            }

            if (Frame.Size.Width == 0)
            {
                throw new Exception("Cannot scale bounds while frame has zero width.");
            }

            if (Frame.Size.Height == 0)
            {
                throw new Exception("Cannot scale bounds while frame has zero height.");
            }

            var outerRatio = (double)Frame.Size.Width / Frame.Size.Height;
            var innerRatio = (double)Bounds.Size.Width / Bounds.Size.Height;

            var x = 0;
            var y = 0;
            var width = Frame.Size.Width;
            var height = Frame.Size.Height;

            if (outerRatio != innerRatio)
            {
                if ((crop && outerRatio < innerRatio) || (!crop && outerRatio > innerRatio))
                {
                    width = (int)(Frame.Size.Height * innerRatio);
                    x = (Frame.Size.Width - width) / 2;
                }
                else
                {
                    height = (int)(Frame.Size.Width / innerRatio);
                    y = (Frame.Size.Height - height) / 2;
                }
            }

            var scale = (double)width / Bounds.Size.Width;

            Bounds = new Rectangle(new Point(x, y), new Size(width, height));

            return scale;
        }

        public dynamic ToDynamic()
        {
            return new
            {
                frame = Frame.ToDynamic(),
                bounds = Bounds.ToDynamic()
            };
        }

        public static LayoutView FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var frame = lookup.ContainsKey("frame") ? Rectangle.FromDynamic(value.frame) as Rectangle? : null;
            if (frame == null)
            {
                return null;
            }

            var bounds = lookup.ContainsKey("bounds") ? Rectangle.FromDynamic(value.bounds) as Rectangle? : null;
            if (bounds == null)
            {
                return new LayoutView(frame.Value);
            }

            return new LayoutView(frame.Value, bounds.Value);
        }
    }
}
