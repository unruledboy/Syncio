using System.Collections.Generic;
using Syncio.Common.Models;

namespace Syncio.Providers.SqlServer
{
	class SqlServerUtils
	{
		public const string DefaultSchema = "dbo";
		public const string LeftQuote = "[";
		public const string RightQuote = "]";
		public const int NonDataAffected = -1;

		public const string CodeHighWatermark = "HighWatermark";


		public static string QuoteName(string name) => name.IndexOf(LeftQuote) == -1 ? $"{LeftQuote}{name}{RightQuote}" : name;

		public static string QuoteName(SyncObject syncObject) => $"{QuoteName(string.IsNullOrEmpty(syncObject.Schema) ? DefaultSchema : syncObject.Schema)}.{QuoteName(syncObject.Table)}";

		public static string NormalizeName(string name) => NormalizeNameWithSpace(name).Replace(" ", string.Empty);

		public static string NormalizeNameWithSpace(string name) => name.Replace($"{LeftQuote}", string.Empty).Replace($"{RightQuote}", string.Empty); //todo: yeah, I know...

		public static string NormalizeName(SyncObject syncObject) => $"{NormalizeName(syncObject.Schema)}_{NormalizeName(syncObject.Table)}";

		public static string ConcatByComma(IEnumerable<string> values) => string.Join(", ", values);
	}
}
