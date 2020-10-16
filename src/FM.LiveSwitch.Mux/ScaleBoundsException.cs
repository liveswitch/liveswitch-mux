using System;

namespace FM.LiveSwitch.Mux
{
    public class ScaleBoundsException : Exception
    {
        public ScaleBoundsException(string message)
            : base(message)
        { }
    }
}
