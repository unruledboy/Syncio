using System;
using Syncio.Common.Models;

namespace Syncio.Common
{
	public class ProgressEventArgs : EventArgs
    {
		public string Message { get; set; }
		public LogLevel Level { get; set; } = LogLevel.Info;
		public SyncRequest Request { get; set; }
	}
}
