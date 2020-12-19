using System;

namespace Syncio.Common.Utils
{
	public static class ExtensionMethods
	{
		public static string GetFullDate(this DateTime date) => date.ToString("yyyy-MM-dd HH:mm:ss.fff");

		public static bool AllEquals(this string text, string compare) => text == null && compare == null || (text != null && text.Equals(compare, StringComparison.InvariantCulture));
	}
}
