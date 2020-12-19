namespace Syncio.Common.Models
{
	public class SyncTarget
	{
		public string Id { get; set; }
		public string Type { get; set; }
		public string ConnectionString { get; set; }
		public RetryPolicy RetryPolicy { get; set; }
    }
}
