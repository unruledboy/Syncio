using System;
using System.Threading.Tasks;
using Syncio.Common.Models;

namespace Syncio.Common.Interfaces
{
	public interface IRequestTransport
	{
		event EventHandler<ProgressEventArgs> Progress;
		Task<bool> Send(SyncRequest request, object payload);
	}
}
