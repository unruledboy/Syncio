using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class SyncTransport
	{
		public string Type { get; set; }
		public List<SyncSetting> Settings { get; set; }
	}
}
