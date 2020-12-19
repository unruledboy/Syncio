using Syncio.Common.Interfaces;

namespace Syncio.Syncer
{
	class SyncTask
    {
		public ISyncProcessor Processor { get; set; }
		public ISyncTransport Transport { get; set; }
	}
}
