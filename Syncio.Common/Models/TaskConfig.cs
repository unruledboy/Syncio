using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class TaskConfig
    {
		public string Name { get; set; }
		public int BufferSize { get; set; }
		public SyncSource Source { get; set; }
		public List<SyncTarget> Targets { get; set; }
        public SyncTransport Transport { get; set; }
		public List<Mapping> Mappings { get; set; }
		public LogStrategy LogStrategy { get; set; }
    }
}
