using System;
using Syncio.Common.Models;

namespace Syncio.Common.Interfaces
{
	public interface ISyncTransport
	{
		event EventHandler<ProgressEventArgs> Progress;
		void Start(Func<SyncRequest, object, SyncResult> request);
		void Stop();
        void LogPayload(SyncConfig config, TaskConfig task, SyncRequest request, object payload);
    }
}
