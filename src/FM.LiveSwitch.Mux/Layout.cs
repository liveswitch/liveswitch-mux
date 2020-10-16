using Jint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class Layout
    {
        public Size Size { get; private set; }

        public int Margin { get; private set; }

        public Dictionary<string, LayoutView> Views { get; private set; }

        public static Layout Calculate(LayoutType type, LayoutInput[] inputs, LayoutOutput output, string javascriptFile)
        {
            var layout = new Layout
            {
                Margin = output.Margin,
                Size = output.Size
            };

            // get frames
            var frames = layout.CalculateFrames(type, inputs, output, javascriptFile);

            // set bounds
            var views = new Dictionary<string, LayoutView>();
            for (var i = 0; i < frames.Length; i++)
            {
                views[inputs[i].ConnectionId] = new LayoutView(frames[i], new Rectangle(Point.Zero, inputs[i].Size));
            }
            layout.Views = views;

            return layout;
        }

        private Rectangle[] CalculateFrames(LayoutType type, LayoutInput[] inputs, LayoutOutput output, string javascriptFile)
        {
            switch (type)
            {
                case LayoutType.HStack:
                    return CalculateStackFrames(true, inputs);
                case LayoutType.VStack:
                    return CalculateStackFrames(false, inputs);
                case LayoutType.HGrid:
                    return CalculateGridFrames(true, inputs);
                case LayoutType.VGrid:
                    return CalculateGridFrames(false, inputs);
                case LayoutType.JS:
                    return CalculateJSFrames(inputs, output, javascriptFile);
                default:
                    throw new Exception("Unrecognized layout.");
            }
        }

        private Rectangle[] CalculateStackFrames(bool horizontal, LayoutInput[] inputs)
        {
            var layoutBoundsWidth = horizontal ? inputs.Select(input => input.Size.Width).Sum() : inputs.Select(input => input.Size.Width).Max();
            var layoutBoundsHeight = horizontal ? inputs.Select(input => input.Size.Height).Max() : inputs.Select(input => input.Size.Height).Sum();
            var layoutBoundsSize = new Size(layoutBoundsWidth, layoutBoundsHeight);

            var layoutFrameSize = Size.Clone();
            var layoutMarginSum = Margin * (inputs.Length - 1);

            // don't scale the margin
            if (horizontal)
            {
                layoutFrameSize = new Size(layoutFrameSize.Width - layoutMarginSum, layoutFrameSize.Height);
            }
            else
            {
                layoutFrameSize = new Size(layoutFrameSize.Width, layoutFrameSize.Height - layoutMarginSum);
            }

            var layoutFrame = new Rectangle(Point.Zero, layoutFrameSize);
            var layoutBounds = new Rectangle(Point.Zero, layoutBoundsSize);
            var scale = new LayoutView(layoutFrame, layoutBounds).ScaleBounds(false);

            var frameX = 0;
            var frameY = 0;
            var frames = new Rectangle[inputs.Length];
            for (var i = 0; i < inputs.Length; i++)
            {
                frames[i] = new Rectangle(new Point(frameX, frameY), horizontal
                    ? new Size((int)(inputs[i].Size.Width * scale), Size.Height)
                    : new Size(Size.Width, (int)(inputs[i].Size.Height * scale)));

                if (horizontal)
                {
                    frameX += frames[i].Size.Width + Margin;
                }
                else
                {
                    frameY += frames[i].Size.Height + Margin;
                }
            }
            return frames;
        }

        private Rectangle[] CalculateGridFrames(bool horizontal, LayoutInput[] inputs)
        {
            return CalculateInlineFrames(new Rectangle(Point.Zero, Size), inputs.Length, horizontal, Margin);
        }

        private Rectangle[] CalculateJSFrames(LayoutInput[] inputs, LayoutOutput output, string javascriptFile)
        {
            var engine = new Engine((options) =>
            {
                options.AllowDebuggerStatement(false);
                options.LimitRecursion(64);
                options.LocalTimeZone(TimeZoneInfo.Utc);
                options.MaxStatements(16384);
            });
            engine.Execute(File.ReadAllText(javascriptFile));
            var result = engine.Invoke("layout", inputs.Select(x => x.ToDynamic()).ToArray(), output.ToDynamic());
            if (result == null)
            {
                throw new Exception("Missing return value from JS 'layout' function.");
            }
            var dynamicFrames = result.ToObject() as dynamic[];
            if (dynamicFrames == null)
            {
                throw new Exception("Unexpected return value from JS 'layout' function.");
            }
            if (dynamicFrames.Length != inputs.Length)
            {
                throw new Exception($"Unexpected array length ({dynamicFrames.Length}, expected {inputs.Length}) in return value from JS 'calculate' function.");
            }
            var frames = new Rectangle[dynamicFrames.Length];
            for (var i = 0; i < dynamicFrames.Length; i++)
            {
                var frame = Rectangle.FromDynamic(dynamicFrames[i]) as Rectangle?;
                if (frame == null)
                {
                    throw new Exception($"Unexpected array value at index {i} in return value from JS 'layout' function.");
                }
                frames[i] = frame.Value;
            }
            return frames;
        }

        // the below code is minimally modified from LayoutPreset in IceLink/LiveSwitch.

        private static Rectangle[] CalculateInlineFrames(Rectangle layout, int count, bool horizontal, int margin)
        {
            var frames = new List<Rectangle>();

            var table = CalculateTable(new Size(layout.Size.Width + margin, layout.Size.Height + margin), count);
            var colCount = table.ColumnCount;
            var rowCount = table.RowCount;
            var cellWidth = table.CellSize.Width;
            var cellHeight = table.CellSize.Height;

            // DO NOT TOUCH THIS

            var inlineMargin_2 = DivideByTwo(margin);
            if (horizontal)
            {
                var i = 0;
                var cellY = layout.Origin.Y + inlineMargin_2;
                var extraYPixels = layout.Size.Height - (rowCount * cellHeight) + margin;
                for (var r = 0; r < rowCount; r++)
                {
                    var extraY = (r < extraYPixels) ? 1 : 0;
                    var numColsThisRow = colCount;
                    if (r == rowCount - 1)
                    {
                        numColsThisRow = (count - i);
                    }

                    var cellX = layout.Origin.X + inlineMargin_2;
                    if (r == rowCount - 1 && rowCount > 1)
                    {
                        var subX = cellX - inlineMargin_2;
                        var subY = cellY - inlineMargin_2;
                        frames.AddRange(CalculateInlineFrames(new Rectangle(new Point(subX, subY), new Size(layout.Origin.X + layout.Size.Width, layout.Origin.Y + layout.Size.Height - subY)), numColsThisRow, horizontal, margin));
                    }
                    else
                    {
                        var extraXPixels = layout.Size.Width - (numColsThisRow * cellWidth) + margin;
                        for (var c = 0; c < numColsThisRow; c++, i++)
                        {
                            var extraX = (c < extraXPixels) ? 1 : 0;
                            var frame = CalculateInlineFrame(new Rectangle(new Point(cellX, cellY), new Size(cellWidth + extraX, cellHeight + extraY)), margin);
                            frames.Add(frame);
                            cellX += cellWidth + extraX;
                        }
                    }
                    cellY += cellHeight + extraY;
                }
            }
            else
            {
                var i = 0;
                var cellX = layout.Origin.X + inlineMargin_2;
                var extraXPixels = layout.Size.Width - (colCount * cellWidth) + margin;
                for (var c = 0; c < colCount; c++)
                {
                    var extraX = (c < extraXPixels) ? 1 : 0;
                    var numRowsThisCol = rowCount;
                    if (c == colCount - 1)
                    {
                        numRowsThisCol = (count - i);
                    }

                    var cellY = layout.Origin.Y + inlineMargin_2;
                    if (c == colCount - 1 && colCount > 1)
                    {
                        var subX = cellX - inlineMargin_2;
                        var subY = cellY - inlineMargin_2;
                        frames.AddRange(CalculateInlineFrames(new Rectangle(new Point(subX, subY), new Size(layout.Origin.X + layout.Size.Width - subX, layout.Origin.Y + layout.Size.Height)), numRowsThisCol, horizontal, margin));
                    }
                    else
                    {
                        var extraYPixels = layout.Size.Height - (numRowsThisCol * cellHeight) + margin;
                        for (var r = 0; r < numRowsThisCol; r++, i++)
                        {
                            var extraY = (r < extraYPixels) ? 1 : 0;
                            var frame = CalculateInlineFrame(new Rectangle(new Point(cellX, cellY), new Size(cellWidth + extraX, cellHeight + extraY)), margin);
                            frames.Add(frame);
                            cellY += cellHeight + extraY;
                        }
                    }
                    cellX += cellWidth + extraX;
                }
            }
            return frames.ToArray();
        }

        private static Rectangle CalculateInlineFrame(Rectangle cellLayout, int margin)
        {
            var inlineMargin_2 = DivideByTwo(margin);
            var x = cellLayout.Origin.X - inlineMargin_2;
            var y = cellLayout.Origin.Y - inlineMargin_2;
            var width = cellLayout.Size.Width - margin;
            var height = cellLayout.Size.Height - margin;
            return new Rectangle(new Point(x, y), new Size(width, height));
        }

        private static int DivideByTwo(int value)
        {
            return (int)Math.Floor(value / 2.0);
        }

        private static LayoutTable CalculateTable(Size tableSize, int count)
        {
            var greatestSize = 0.0d;
            var colCount = 1.0d;
            var rowCount = 1.0d;
            for (var cols = (double)count; cols >= 1; cols--)
            {
                var rows = Math.Ceiling(count / cols);
                var widthSize = tableSize.Width / cols;
                var heightSize = tableSize.Height / rows;
                var size = (widthSize < heightSize ? widthSize : heightSize);
                if (size >= greatestSize)
                {
                    greatestSize = size;
                    colCount = cols;
                    rowCount = rows;
                }
            }

            var cellWidth = (int)Math.Floor(tableSize.Width / colCount);
            var cellHeight = (int)Math.Floor(tableSize.Height / rowCount);
            return new LayoutTable((int)colCount, (int)rowCount, new Size(cellWidth, cellHeight));
        }
    }
}
