using System;
using System.Linq;
using Xunit;

namespace FM.LiveSwitch.Mux.Test
{
    public class LayoutTests
    {
        [Fact]
        public void BasicJavaScriptLayout()
        {
            var connectionId = Guid.NewGuid().ToString();

            var layout = Layout.Calculate(LayoutType.JS, new[]
            {
                new LayoutInput
                {
                    ConnectionId = connectionId,
                    ConnectionTag = "tag",
                    ClientId = Guid.NewGuid().ToString(),
                    DeviceId = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid().ToString(),
                    AudioDisabled = false,
                    AudioMuted = false,
                    VideoDisabled = false,
                    VideoMuted = false,
                    Size = new Size(640, 480)
                }
            }, new LayoutOutput
            {
                ApplicationId = Guid.NewGuid().ToString(),
                ChannelId = Guid.NewGuid().ToString(),
                Margin = 0,
                Size = new Size(1280, 720)
            }, "layout.js");

            Assert.Equal(0, layout.Margin);
            Assert.Single(layout.Views);
            Assert.Equal(1280, layout.Size.Width);
            Assert.Equal(720, layout.Size.Height);

            var record = layout.Views.Single();
            Assert.Equal(connectionId, record.Key);

            var view = record.Value;
            Assert.Equal(0, view.Bounds.Origin.X);
            Assert.Equal(0, view.Bounds.Origin.Y);
            Assert.Equal(640, view.Bounds.Size.Width);
            Assert.Equal(480, view.Bounds.Size.Height);
            Assert.Equal(550, view.Frame.Origin.X);
            Assert.Equal(90, view.Frame.Origin.Y);
            Assert.Equal(180, view.Frame.Size.Width);
            Assert.Equal(180, view.Frame.Size.Height);
        }
    }
}
