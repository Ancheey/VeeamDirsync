using VeeamDirsync.Logging;
using VeeamDirsync.Syncing;

namespace VeeamDirsync
{
    internal class Program
    {
        private const int DefaultIntervalMinutes = 30;
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(DefaultIntervalMinutes);

        static async Task Main(string[] args)
        {
            try
            {
                var (source, target, interval, loggers) = ParseArguments(args);
                if (!DataSyncer.TryCreate(out var syncer, source, target, loggers) || syncer is null)
                {
                    Console.WriteLine("Failed to initialize synchronizer");
                    return;
                }

                await RunSyncLoop(syncer, interval, loggers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
        }

        private static (string source, string target, TimeSpan interval, ILogger[] loggers) ParseArguments(string[] args)
        {
            if (args.Length < 2)
                throw new ArgumentException("Source and target directories must be specified");

            var loggers = new List<ILogger> { new ConsoleLogger() };
            var interval = ParseInterval(args);
            AddFileLoggerIfSpecified(args, loggers);

            return (args[0], args[1], interval, loggers.ToArray());
        }

        private static TimeSpan ParseInterval(string[] args)
        {
            if (args.Length <= 2)
                return DefaultInterval;

            if (!int.TryParse(args[2], out var minutes) || minutes <= 0)
            {
                Console.WriteLine($"Invalid interval, using default {DefaultIntervalMinutes} minutes");
                return DefaultInterval;
            }

            return TimeSpan.FromMinutes(minutes);
        }

        private static void AddFileLoggerIfSpecified(string[] args, List<ILogger> loggers)
        {
            if (args.Length <= 3)
                return;

            if (FileLogger.TryCreate(out var fileLogger, args[3]) && fileLogger != null)
            {
                loggers.Add(fileLogger);
            }
        }

        private static async Task RunSyncLoop(DataSyncer syncer, TimeSpan interval, ILogger[] loggers)
        {
            while (true)
            {
                try
                {
                    await loggers.Log($"Starting synchronization (Interval: {interval.TotalMinutes} minutes)");
                    await syncer.Sync();
                    await loggers.Log($"Synchronization completed. Next run in {interval.TotalMinutes} minutes");
                }
                catch (Exception ex)
                {
                    await loggers.Log($"Synchronization failed: {ex.Message}");
                }

                await Task.Delay(interval);
            }
        }
    }
}
