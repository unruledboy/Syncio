using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Processor
{
    public class RequestProcessor
	{
		public event EventHandler<ProgressEventArgs> Progress;

		private static readonly RequestProcessor instance;
		private ConcurrentDictionary<string, ProcessTask> processTasks = new ConcurrentDictionary<string, ProcessTask>();
		private readonly static object syncRoot = new object();
        private SyncConfig config;

        static RequestProcessor() => instance = new RequestProcessor();

		private RequestProcessor() { }

		public static RequestProcessor Instance => instance;


		public void Initialize(SyncConfig config)
		{
			this.config = config;

			foreach (var task in config.Tasks)
			{
				var processor = ProviderManager.Instance.GetRequestProcessor<IRequestProcessor>(config, task.Source.Type);
				processor.Setup(task);
			}
		}

		public void Start()
		{
			Stop();

			Parallel.ForEach(config.Tasks, (task) =>
			{
				var processTask = GetProcessTask(task);
				processTask.Processor.Start(config, async (request, data) =>
				{
					Progress?.Invoke(this, new ProgressEventArgs { Message = $"Request processed", Request = request });
					bool result;
					try
					{
						result = await processTask.Transport.Send(request, data);
						Progress?.Invoke(this, new ProgressEventArgs { Message = $"Request sent", Request = request });
					}
					catch (Exception ex)
					{
						Progress?.Invoke(this, new ProgressEventArgs { Level = LogLevel.Exception, Message = $"Exception encountered: {ex.Message}", Request = request });
						result = false;
					}
					return result;
				});
			});
		}

		public void Stop()
		{
			foreach (var task in processTasks.Select(x => x.Value))
			{
				task.HistoryTimer?.Dispose();
				task.Processor.Stop();
				task.Transport.Progress -= OnProgress;
				task.Processor.Progress -= OnProgress;
			}
			processTasks.Clear();
		}

		public Dictionary<string, long> GetStats(TaskConfig config) => processTasks[config.Name]?.Processor.Stats;

		private ProcessTask GetProcessTask(TaskConfig config)
		{
			if (!processTasks.TryGetValue(config.Name, out var processTask))
			{
				processTask = new ProcessTask
				{
					Processor = ProviderManager.Instance.GetRequestProcessor<IRequestProcessor>(this.config, config.Source.Type),
					Transport = ProviderManager.Instance.GetRequestTransport<IRequestTransport>(this.config, config, config.Transport.Type)
				};
				processTask.Processor.Setup(config);
				processTask.Transport.Progress += OnProgress;
				processTask.Processor.Progress += OnProgress;
				if (config.Source.HistoryStrategy.Type == HistoryStrategyType.PeriodicDelete)
					processTask.HistoryTimer = new Timer(x => ProcessHistory(processTask.Processor), null, TimeSpan.Zero, TimeSpan.FromSeconds(config.Source.HistoryStrategy.IntervalInSecond));
				processTasks.TryAdd(config.Name, processTask);
			}
			return processTask;
		}

		private void ProcessHistory(IRequestProcessor processor)
		{
			var lockTaken = false;
			try
			{
				lockTaken = Monitor.TryEnter(syncRoot);
				if (lockTaken)
					ProcessHistoryCore(processor);
			}
			finally
			{
				if (lockTaken)
					Monitor.Exit(syncRoot);
			}
		}

		private void ProcessHistoryCore(IRequestProcessor processor)
		{
			processor.ProcessHistory();
		}

		private void OnProgress(object sender, ProgressEventArgs e)
		{
			Progress?.Invoke(sender, e);
		}
	}
}
