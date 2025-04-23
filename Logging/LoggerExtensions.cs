using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamDirsync.Logging
{
    public static class LoggerExtensions
    {
        public static async Task Log(this ILogger[] loggers, string message, bool addTimestamp = true)
        {
            await Task.WhenAll(loggers.Select(l => l.Log(message, addTimestamp)));
        }
    }
}
