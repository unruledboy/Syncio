using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Syncer
{
    class SyncProcessor
	{
		public event EventHandler<ProgressEventArgs> Progress;

		private static readonly SyncProcessor instance;
		private SyncConfig config;
		private readonly ConcurrentDictionary<string, SyncTask> syncTasks = new ConcurrentDictionary<string, SyncTask>();

		static SyncProcessor() => instance = new SyncProcessor();

		private SyncProcessor() { }

		public static SyncProcessor Instance => instance;

		public void Start(SyncConfig config)
		{
			Stop();

			this.config = config;

			StartCore();
		}

		public void Stop()
		{
			foreach (var task in syncTasks.Select(x => x.Value))
			{
				task.Transport.Stop();
				task.Transport.Progress -= OnProgress;
				task.Processor.Progress -= OnProgress;
			}
			syncTasks.Clear();
		}

		public Dictionary<string, long> GetStats(TaskConfig config) => syncTasks[config.Name]?.Processor.Stats;

		private void StartCore()
		{
			foreach (var task in config.Tasks)
			{
				var syncTask = new SyncTask { Transport = ProviderManager.Instance.GetSyncTransport<ISyncTransport>(config, task, task.Transport.Type), Processor = ProviderManager.Instance.GetSyncProcessor<ISyncProcessor>(config, task.Source.Type) };
				syncTask.Transport.Progress += OnProgress;
				syncTask.Processor.Progress += OnProgress;
				syncTasks.TryAdd(task.Name, syncTask);
				syncTask.Transport.Start((request, payload) =>
				{
                    if (task.LogStrategy.LogPolicy == LogPolicy.Everything)
                        syncTask.Transport.LogPayload(config, task, request, payload);
                    try
                    {
					    syncTask.Processor.Setup(config.Role, task);
					    var result = syncTask.Processor.Run(request, payload);
                        var isSuccessful = result.All(x => x.Value);
                        if (task.LogStrategy.LogPolicy == LogPolicy.NotSuccessful)
                            syncTask.Transport.LogPayload(config, task, request, payload);
                        return new SyncResult { IsSuccessful = isSuccessful };
                    }
                    catch (Exception ex)
					{
                        if (task.LogStrategy.LogPolicy == LogPolicy.OnException)
                            syncTask.Transport.LogPayload(config, task, request, payload);
                        Progress?.Invoke(this, new ProgressEventArgs { Message = $"Exception encountered while syncing request: {ex.Message}", Request = request });
						return new SyncResult { IsSuccessful = false, Message = ex.Message };
					}
				});
			}
		}

        private void OnProgress(object sender, ProgressEventArgs e)
		{
			Progress?.Invoke(sender, e);
		}
	}
}