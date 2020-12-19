using System.Collections.Generic;

namespace Syncio.Common.Models
{
	public class SyncObjectWithPks : SyncObject
    {
		public List<string> Pks { get; set; }
	}
}
