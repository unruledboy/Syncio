using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Syncio.Common.Models;

namespace Syncio.Common.Interfaces
{
	public interface IRequestProcessor
    {
		event EventHandler<ProgressEventArgs> Progress;

		string Id { get; }

		Dictionary<string, long> Stats { get; }

		void Start(SyncConfig syncConfig, Func<SyncRequest, object, Task<bool>> onRequest);

		void Stop();

		void Setup(TaskConfig config);

		long GetHighWatermak();

		void ProcessHistory();
	}
}
