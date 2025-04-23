using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamDirsync.Logging
{
    public interface ILogger
    {
        public Task Log(string message, bool addTimestamp = true);
    }
}
