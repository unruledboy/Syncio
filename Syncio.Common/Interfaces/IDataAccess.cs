using System;
using System.Collections.Generic;
using System.Data;

namespace Syncio.Common.Interfaces
{
	public interface IDataAccess
    {
		void DoBiz(string commandText, Action<IDbCommand> commandAction, Dictionary<string, object> parameters = null);

		int Execute(string commandText, Dictionary<string, object> parameters = null);

		T GetValue<T>(string commandText, Dictionary<string, object> parameters = null);

		DataTable GetData(string commandText, Dictionary<string, object> parameters = null);
	}
}
