using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class Context
    {
        public Application[] Applications
        {
            get { return _Applications.Values.ToArray(); }
        }

        private readonly Dictionary<string, Application> _Applications = new Dictionary<string, Application>();

        public bool ProcessLogEntry(LogEntry logEntry, MuxOptions options, ILoggerFactory loggerFactory)
        {
            var applicationId = logEntry.ApplicationId;
            if (applicationId == null)
            {
                return false;
            }

            if (!_Applications.TryGetValue(applicationId, out var application))
            {
                _Applications[applicationId] = application = new Application(applicationId);
            }

            return application.ProcessLogEntry(logEntry, options, loggerFactory);
        }
    }
}
