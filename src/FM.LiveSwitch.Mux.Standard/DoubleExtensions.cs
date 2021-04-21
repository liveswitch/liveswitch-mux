using System;

namespace FM.LiveSwitch.Mux
{
    static class DoubleExtensions
    {
        public static double Round(this double value, int places)
        {
            var factor = (long)Math.Pow(10, places);
            return Math.Round(value * factor) / factor;
        }
    }
}
