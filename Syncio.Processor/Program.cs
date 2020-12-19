using System;
using System.Linq;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.LogProviders;
using Syncio.Common.Utils;

namespace Syncio.Processor
{
	class Program
	{
		static void Main(string[] args)
		{
			var loggers = new[] { (ILogger)new ConsoleLogger(), new ProgressFileLog() }.ToList();
			AppDomain.CurrentDomain.UnhandledException += (s, e) => loggers.ForEach(x => x.Log(e.ExceptionObject is Exception exception ? $"Unhandled exception encountered: {exception.Message}" : "Unknown exception", LogLevel.Exception));
			RequestProcessor.Instance.Progress += (s, e) => loggers.ForEach(x => x.Log(e.Message, e.Level, e.Request));

			var configLoader = new ConfigLoader();
			configLoader.Input += (s, e) =>
			{
				Console.WriteLine($"Enter parameter: {e.Name}");
				e.Value = Console.ReadLine();
			};
			var config = configLoader.Load();
			loggers.ForEach(x => x.LogLevel = config.LogLevel);
			RequestProcessor.Instance.Initialize(config);
			RequestProcessor.Instance.Start();
			Console.WriteLine("Request processor started");

			while (true)
			{
				var command = Console.ReadLine().ToLowerInvariant();
				switch (command)
				{
					case "s":
						RequestProcessor.Instance.Start();
						break;
					case "t":
						RequestProcessor.Instance.Stop();
						break;
					case "c":
						foreach (var task in config.Tasks)
						{
							loggers.ForEach(x => x.Log($"Stats for {task.Name}"));
							foreach (var stat in RequestProcessor.Instance.GetStats(task))
								loggers.ForEach(x => x.Log($"\t{stat.Key}: {stat.Value}"));
							loggers.ForEach(x => x.Log(string.Empty));
						}
						break;
					case "q":
						RequestProcessor.Instance.Stop();
						return;
				}
			}
		}
	}
}
