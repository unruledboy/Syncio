using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Syncio.Common;
using Syncio.Common.Interfaces;

namespace Syncio.Providers.SqlServer
{
	public class SqlDataAccess : IDataAccess
	{
		private readonly string connectionString;

		public SqlDataAccess(string connectionString)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);
			builder.ApplicationName = Constants.AppName;
			this.connectionString = builder.ToString();
		}

		public void DoBiz(string commandText, Action<IDbCommand> commandAction, Dictionary<string, object> parameters = null)
		{
			using (var connection = new SqlConnection(connectionString))
			{
				connection.Open();

				using (var command = new SqlCommand())
				{
					command.Connection = connection;
					command.CommandType = CommandType.Text;
					command.CommandText = commandText;
					if (parameters != null)
						foreach (var item in parameters)
						{
							command.Parameters.AddWithValue(item.Key, item.Value);
						}
					commandAction(command);
				}
			}
		}

		public int Execute(string commandText, Dictionary<string, object> parameters = null)
		{
			var affected = -1;
			DoBiz(commandText, (x) =>
				{
					affected = x.ExecuteNonQuery();
				}, parameters);
			return affected;
		}

		public DataTable GetData(string commandText, Dictionary<string, object> parameters = null)
		{
			var table = new DataTable();
			DoBiz(commandText, (x) =>
			{
				var adapter = new SqlDataAdapter((SqlCommand)x);
				adapter.Fill(table);
			}, parameters);
			return table;
		}

		public T GetValue<T>(string commandText, Dictionary<string, object> parameters = null)
		{
			T result = default;
			DoBiz(commandText, (x) =>
				{
					var value = x.ExecuteScalar();
					if (value != null && value != DBNull.Value)
						result = (T)value;
				}, parameters);
			return result;
		}
	}
}