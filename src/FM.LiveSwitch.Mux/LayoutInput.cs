using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public class LayoutInput
    {
        public string ConnectionId { get; set; }

        public string ClientId { get; set; }

        public string DeviceId { get; set; }

        public string UserId { get; set; }

        public Size Size { get; set; }

        public dynamic ToDynamic()
        {
            return new
            {
                connectionId = ConnectionId,
                clientId = ClientId,
                deviceId = DeviceId,
                userId = UserId,
                size = Size.ToDynamic()
            };
        }

        public static LayoutInput FromDynamic(dynamic value)
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

            return new LayoutInput
            {
                ConnectionId = lookup.ContainsKey("connectionId") ? value.connectionId as string : null,
                ClientId = lookup.ContainsKey("clientId") ? value.clientId as string : null,
                DeviceId = lookup.ContainsKey("deviceId") ? value.deviceId as string : null,
                UserId = lookup.ContainsKey("userId") ? value.userId as string : null,
                Size = size.Value
            };
        }
    }
}
