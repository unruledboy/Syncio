using System;

namespace Syncio.Common.Utils
{
	public class InputEventArgs : EventArgs
	{
		public string Name { get; set; }
		public string Value { get; set; }
	}
}
