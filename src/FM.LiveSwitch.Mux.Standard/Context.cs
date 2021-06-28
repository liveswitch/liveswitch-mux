using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class Context
    {
        public Application[] Applications
        {
            get { return _Applications.Values.ToArray(); }
        }

        private readonly Dictionary<string, Application> _Applications = new Dictionary<string, Application>();

        private readonly ILoggerFactory _LoggerFactory;

        public Context(ILoggerFactory loggerFactory)
        {
            _LoggerFactory = loggerFactory;
        }

        public async Task<bool> ProcessLogEntry(LogEntry logEntry, MuxOptions options)
        {
            var applicationId = logEntry.ApplicationId;
            if (applicationId == null)
            {
                return false;
            }

            if (!_Applications.TryGetValue(applicationId, out var application))
            {
                _Applications[applicationId] = application = new Application(applicationId, logEntry.ExternalId, _LoggerFactory);
            }

            return await application.ProcessLogEntry(logEntry, options).ConfigureAwait(false);
        }
    }
}
