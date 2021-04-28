using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public class LayoutOutput
    {
        public string ChannelId { get; set; }

        public string ApplicationId { get; set; }

        public Size Size { get; set; }

        public int Margin { get; set; }

        public dynamic ToDynamic()
        {
            return new
            {
                channelId = ChannelId,
                applicationId = ApplicationId,
                size = Size.ToDynamic(),
                margin = Margin
            };
        }

        public static LayoutOutput FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var size = lookup.ContainsKey("size") ? Size.FromDynamic(value.size) as Size? : null;
            if (size == null)
            {
                return null;
            }

            var margin = lookup.ContainsKey("margin") ? value.width as double? : null;
            if (margin == null)
            {
                return null;
            }

            return new LayoutOutput
            {
                ChannelId = lookup.ContainsKey("channelId") ? value.channelId as string : null,
                ApplicationId = lookup.ContainsKey("applicationId") ? value.applicationId as string : null,
                Size = size.Value,
                Margin = (int)margin.Value
            };
        }
    }
}
