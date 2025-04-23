using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamDirsync.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string logPath;
        private FileLogger(string logPath)
        {
            this.logPath = logPath;
        }
        public async Task Log(string message, bool addTimestamp = true)
        {
            try
            {
                using StreamWriter sr = new(logPath, true);
                if (addTimestamp)
                    await sr.WriteLineAsync($"[{DateTime.Now}] {message}");
                else
                    await sr.WriteLineAsync(message);
                sr.Close();
                

            }
            catch(Exception e)
            {
                Console.WriteLine($"Unable to save a log message to a file [{logPath}] Issue: {e}");
            }
        }
        public static bool TryCreate(out FileLogger? logger,string logPath)
        {
            try
            {
                if (!File.Exists(logPath))
                    File.Create(logPath);
                logger = new FileLogger(logPath);
                Console.WriteLine($"Dirsync by @Ancheey");
                return true;
            }
            catch
            {
                logger = null;
                return false;
            }
            
        }
    }
}
