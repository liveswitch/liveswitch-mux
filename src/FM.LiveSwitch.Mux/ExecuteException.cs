using System;

namespace FM.LiveSwitch.Mux
{
    public class ExecuteException : Exception
    {
        public ExecuteException(string message)
            : base(message)
        { }

        public ExecuteException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
