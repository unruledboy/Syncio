using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Syncio.Common.LogProviders
{
    public class FileLogger
    {
        private readonly ConcurrentQueue<Tuple<string, string>> pendingLogs = new ConcurrentQueue<Tuple<string, string>>();
        private readonly static object syncRoot = new object();
        private readonly Timer timer;

        public FileLogger()
        {
            timer = new Timer(x => WriteLog(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private void WriteLog()
        {
            var lockTaken = false;
            try
            {
                lockTaken = Monitor.TryEnter(syncRoot);
                if (lockTaken)
                {
                    while (pendingLogs.TryDequeue(out var result))
                        WriteLogCore(result);
                }
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(syncRoot);
            }
        }

        public void Log(Tuple<string, string> message)
        {
            pendingLogs.Enqueue(message); //todo: buffer size
        }

        private static void WriteLogCore(Tuple<string, string> log)
        {
            var path = $@"{AppDomain.CurrentDomain.BaseDirectory}\Logs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            File.AppendAllText(Path.Combine(path, log.Item1), log.Item2);
        }
    }
}
