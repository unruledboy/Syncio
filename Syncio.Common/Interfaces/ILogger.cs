using Syncio.Common.Models;

namespace Syncio.Common.Interfaces
{
	public interface ILogger
	{
		LogLevel LogLevel { get; set; }
		void Log(string message, LogLevel logLevel = LogLevel.Info, SyncRequest request = null);
    }
}
