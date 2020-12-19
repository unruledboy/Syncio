using System;
using System.Collections.Generic;
using Syncio.Common.Models;

namespace Syncio.Common.Interfaces
{
	public interface ISyncProcessor
    {
		event EventHandler<ProgressEventArgs> Progress;

		string Id { get; }

		void Setup(SyncRole role, TaskConfig config);

		Dictionary<string, long> Stats { get; }

        Dictionary<string, bool> Run(SyncRequest request, object payload);
    }
}
