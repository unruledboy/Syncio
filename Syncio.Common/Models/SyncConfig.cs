using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class SyncConfig
    {
		public int SyncIntervalInMS { get; set; }
		public SyncRole Role { get; set; }
		public LogLevel LogLevel { get; set; } = LogLevel.Info;
		public List<TaskConfig> Tasks { get; set; }
		public Providers Providers { get; set; }
    }
}
