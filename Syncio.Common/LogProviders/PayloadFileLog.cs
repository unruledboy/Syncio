using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using System;

namespace Syncio.Common.LogProviders
{
    public class PayloadFileLog : ILogger
    {
        private readonly FileLogger fileLogger = new FileLogger();

        public LogLevel LogLevel { get; set; }

        public void Log(string message, LogLevel logLevel = LogLevel.Info, SyncRequest request = null)
        {
            if ((int)logLevel >= (int)LogLevel)
                fileLogger.Log(new Tuple<string, string>(string.Format("Syncio payload {0}.txt", request.Id), message));
        }
    }
}
