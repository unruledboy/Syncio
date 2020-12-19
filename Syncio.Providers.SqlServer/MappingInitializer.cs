using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;
using Syncio.Providers.SqlServer.Models;

namespace Syncio.Providers.SqlServer
{
	class MappingInitializer
	{
		public static (IEnumerable<PkColumn> pkColumns, Dictionary<string, Column> columns) Initialize(Mapping mapping, IDataAccess dataAccess)
		{
			var pkColumns = dataAccess.GetData($@"SELECT ccu.COLUMN_NAME, ccu.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
    INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE AS ccu
        ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
WHERE tc.TABLE_SCHEMA = @Schema
    AND tc.TABLE_NAME = @TableName
    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'"
					, new Dictionary<string, object> { { "@TableName", mapping.Source.Table }, { "@Schema", mapping.Source.Schema } })
					.Rows.Cast<DataRow>().Select(x =>
						new PkColumn
						{
							ColumnName = Convert.ToString(x["COLUMN_NAME"]),
							ConstraintName = Convert.ToString(x["CONSTRAINT_NAME"])
						}).ToList();
			var columns = dataAccess.GetData($"SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, NUMERIC_PRECISION, NUMERIC_SCALE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @Schema"
					, new Dictionary<string, object> { { "@TableName", mapping.Source.Table }, { "@Schema", mapping.Source.Schema } })
				.Rows.Cast<DataRow>().Select(x =>
					new Column
					{
						Position = Convert.ToInt32(x["ORDINAL_POSITION"]),
						ColumnName = Convert.ToString(x["COLUMN_NAME"]),
						DataType = Convert.ToString(x["DATA_TYPE"]),
						IsNullable = Convert.ToString(x["IS_NULLABLE"]).AllEquals("YES"),
						MaxLength = x["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value ? 0 : Convert.ToInt32(x["CHARACTER_MAXIMUM_LENGTH"]),
						NumericPrecision = x["NUMERIC_PRECISION"] == DBNull.Value ? string.Empty : $"{Convert.ToInt32(x["NUMERIC_PRECISION"])},{Convert.ToInt32(x["NUMERIC_SCALE"])}"
					})
				.OrderBy(x => x.Position).ToDictionary(x => x.ColumnName);

			if (mapping.Source.Pks.Count == 0)
				mapping.Source.Pks.AddRange(pkColumns.Select(x => x.ColumnName));

			if (mapping.Target.Pks.Count == 0)
				mapping.Target.Pks.AddRange(pkColumns.Select(x => x.ColumnName));

			if (mapping.Columns.Count == 0)
				mapping.Columns.AddRange(columns.Where(x => !pkColumns.Any(c => c.ColumnName.AllEquals(x.Value.ColumnName))).Select(x => x.Value).Select(x => new ColumnMapping { Source = x.ColumnName }));

			foreach (var column in mapping.Columns)
			{
				if (string.IsNullOrEmpty(column.Target))
					column.Target = column.Source;
			}

			return (pkColumns, columns);
		}
	}
}
