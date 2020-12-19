using System;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Common.LogProviders
{
	public class ConsoleLogger : ILogger
	{
		public LogLevel LogLevel { get; set; }

		public void Log(string message, LogLevel logLevel = LogLevel.Info, SyncRequest request = null)
		{
			if ((int)logLevel >= (int)LogLevel)
			{
				message = request != null ? $"{message}, Task Name{request.TaskName}, Type: {request.Type}, Operation: {Constants.Operations[request.Operation]}, Resource Id: {string.Join("|", request.ResourceIds)}" : message;
				Console.WriteLine(($"[{DateTime.Now.GetFullDate()}] {request?.Id}: {message}"));
			}
		}
	}
}
