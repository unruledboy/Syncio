namespace Syncio.Common.Models
{
	public class SyncSource : SyncTarget
    {
		public SyncObjectWithPk SettingTable { get; set; }
		public SyncObjectWithPk RequestTable { get; set; }
		public HistoryStrategy HistoryStrategy { get; set; }
	}
}
