using System.Collections.Generic;

namespace Syncio.Common
{
	public class Constants
    {
		public const string KeyMappingPrefix = "Syncio";
		public const string KeyMappingTablePk = KeyMappingPrefix + "Pk";
		public const string KeyMappingTableCreatedDate = KeyMappingPrefix + KeyCreatedDate;

		public const string AppName = KeyMappingPrefix;
		
		public const string KeyId = "Id";
		public const string KeyType = "Type";
		public const string KeyOperation = "Operation";
		public const string KeyResourceId = "ResourceId";
		public const string KeyName = "Name";
		public const string KeyTaskType = "TaskType";
		public const string KeyCreatedDate = "CreatedDate";
		public const string KeyPayload = "Payload";

		public const string SettingConnectionString = "ConnectionString";

		public const string ValuePlaceholder = "@";

		public static Dictionary<Operation, string> Operations = new Dictionary<Operation, string>
		{
			{ Operation.Insert, "Insert" },
			{ Operation.Update, "Update" },
			{ Operation.Delete, "Delete" },
		};
	}
}
