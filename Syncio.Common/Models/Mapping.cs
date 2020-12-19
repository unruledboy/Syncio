using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class Mapping
    {
		public int Type { get; set; }
		public SyncObjectWithPks Source { get; set; }
        public SyncObjectWithPks Target { get; set; }
		public ConflictPolicy ConflictPolicy { get; set; }
		public List<ColumnMapping> Columns { get; set; }
	}
}
