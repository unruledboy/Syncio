using System.Threading;
using Syncio.Common.Interfaces;

namespace Syncio.Processor
{
	class ProcessTask
    {
		public IRequestProcessor Processor { get; set; }
		public IRequestTransport Transport { get; set; }
		public Timer HistoryTimer { get; internal set; }
	}
}
