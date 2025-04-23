using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamDirsync.Logging
{
    public class ConsoleLogger : ILogger
    {
        public async Task Log(string message, bool addTimestamp = true)
        {
            if(addTimestamp)
                Console.WriteLine($"[{DateTime.Now:HH:mm}] {message}");
            else
                Console.WriteLine(message);
        }
    }
}
