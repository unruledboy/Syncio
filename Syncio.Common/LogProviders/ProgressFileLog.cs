using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;
using System;

namespace Syncio.Common.LogProviders
{
    public class ProgressFileLog : ILogger
    {
        private readonly FileLogger fileLogger = new FileLogger();

        public LogLevel LogLevel { get; set; }

        public void Log(string message, LogLevel logLevel = LogLevel.Info, SyncRequest request = null)
        {
            if ((int)logLevel >= (int)LogLevel)
            {
                message = string.Concat("[", DateTime.Now.GetFullDate(), "]", " ", request != null ? $"{message}, Task Name{request.TaskName}, Type: {request.Type}, Operation: {Constants.Operations[request.Operation]}, Resource Id: {string.Join("|", request.ResourceIds)}" : message, "\r\n");
                fileLogger.Log(new Tuple<string, string>(string.Format("Syncio {0}.txt", DateTime.Now.ToString("yyyy-MM-dd")), message));
            }
        }
    }
}
