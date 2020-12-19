using System;
using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class SyncRequest
    {
		public string TaskName { get; set; }
		public string TaskType { get; set; }
		public long Id { get; set; }
		public List<object> ResourceIds { get; set; } = new List<object>();
		public int Type { get; set; }
		public Operation Operation { get; set; }
		public DateTime CreatedDate { get; set; }
	}
}
