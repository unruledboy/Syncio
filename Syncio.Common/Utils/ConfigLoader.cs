using System;
using System.IO;
using Syncio.Common.Models;

namespace Syncio.Common.Utils
{
	public class ConfigLoader
	{
		public event EventHandler<InputEventArgs> Input;

		public SyncConfig Load()
		{
			var config = Serializer.DeserializeText<SyncConfig>(File.ReadAllText("Config.json"));
			if (config.Role == SyncRole.None)
				config.Role = (SyncRole)Enum.Parse(typeof(SyncRole), GetInput($"Sync role (Hub/Member)"));

			foreach (var task in config.Tasks)
			{
				if (task.Source.ConnectionString == Constants.ValuePlaceholder)
					task.Source.ConnectionString = GetInput($"Source connection string for {task.Name}({task.Source.Type})");

                foreach (var target in task.Targets)
                {
                    if (target.ConnectionString == Constants.ValuePlaceholder)
                        target.ConnectionString = GetInput($"Target connection string for {task.Name}({target.Type})");
                }

                foreach (var setting in task.Transport.Settings)
				{
					if (setting.Value == Constants.ValuePlaceholder)
						setting.Value = GetInput($"Setting: {setting.Name} for {task.Name}({task.Transport.Type})");
				}
			}
			return config;
		}

		string GetInput(string name)
		{
			var input = new InputEventArgs { Name = name };
			Input?.Invoke(this, input);
			return input.Value;
		}
	}
}
