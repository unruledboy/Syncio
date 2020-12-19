using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;

namespace Syncio.Providers.SqlServer
{
	public class SqlServerRequestProcessor : IRequestProcessor
	{
		public event EventHandler<ProgressEventArgs> Progress;
		private TaskConfig config;
		private SqlDataAccess dataAccess;
		private readonly static object syncRoot = new object();
		private Timer timer;
		private Func<SyncRequest, object, Task<bool>> onRequest;
		private volatile bool processCancelled = false;
		private long runCount = 0;
		private long executedCount = 0;
		private long requestCount = 0;
		private long processedCount = 0;
		private long historyDeleteCount = 0;
		private long processedDeleteCount = 0;
		private long processedUpdateCount = 0;
		private long processedInsertCount = 0;

		public void Setup(TaskConfig config)
		{
			this.config = config;
			dataAccess = new SqlDataAccess(config.Source.ConnectionString);

			InitializeSyncTables();
			InitializeMappingTables();
		}

		public void Start(SyncConfig syncConfig, Func<SyncRequest, object, Task<bool>> onRequest)
		{
			Stop();
			this.onRequest = onRequest;
			processCancelled = false;
			timer = new Timer(x => Process(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(syncConfig.SyncIntervalInMS));
		}

		public void Stop()
		{
			timer?.Dispose();
			processCancelled = true;
		}

		#region Initialization

		private void InitializeMappingTables()
		{
			foreach (var mapping in config.Mappings)
			{
				#region defaults

				var tableName = SqlServerUtils.QuoteName(mapping.Source);
				var tableNormalName = SqlServerUtils.NormalizeName(mapping.Source);
				var (pkColumns, columns) = MappingInitializer.Initialize(mapping, dataAccess);

				#endregion

				#region table definition

				var pk = $"{Constants.KeyMappingTablePk} bigint IDENTITY(1,1) PRIMARY KEY";
				var createdDate = $"{Constants.KeyMappingTableCreatedDate} datetime2";
				var columnList = GetPkColumnMappings(mapping).Concat(mapping.Columns).Select(x =>
				{
					var column = columns[SqlServerUtils.NormalizeNameWithSpace(x.Source)];
					var maxLength = column.DataType == "decimal" ? $"({column.NumericPrecision})" : (column.MaxLength == 0 ? string.Empty : $"({column.MaxLength.ToString(CultureInfo.InvariantCulture)})");
					var dataType = $"{SqlServerUtils.QuoteName(column.DataType)} {maxLength}";
					var nullable = column.IsNullable ? "NULL" : "NOT NULL";
					return $"{SqlServerUtils.QuoteName(x.Source)} {dataType} {nullable}";
				});
				var columnDefinitions = SqlServerUtils.ConcatByComma(new[] { pk, createdDate }.Concat(columnList));
				var mappingTableName = $"{Constants.KeyMappingPrefix}_{tableNormalName}";
				var mappingTableNameQuoted = $"{SqlServerUtils.QuoteName(SqlServerUtils.DefaultSchema)}.{SqlServerUtils.QuoteName(mappingTableName)}";
				var indexColumns = SqlServerUtils.ConcatByComma(mapping.Source.Pks.Select(x => SqlServerUtils.QuoteName(x)));
				var sql = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{mappingTableNameQuoted}') AND type in (N'U'))
BEGIN
	CREATE TABLE {mappingTableNameQuoted} (
	{columnDefinitions})
	CREATE INDEX IX_{mappingTableName}_Keys ON {mappingTableNameQuoted} ({indexColumns}) INCLUDE ({Constants.KeyMappingTableCreatedDate})
END";

				if (dataAccess.Execute(sql) != SqlServerUtils.NonDataAffected)
					Progress?.Invoke(this, new ProgressEventArgs { Message = $"Mapping table created: {mappingTableNameQuoted}" });

				#endregion

				#region triggers

				var triggerColumnList = mapping.Columns.Select(x => SqlServerUtils.QuoteName(x.Source));
				var triggerColumns = SqlServerUtils.ConcatByComma(mapping.Source.Pks.Concat(triggerColumnList));
				//update trigger
				var updateTriggerName = $"{tableNormalName}_{Constants.KeyMappingPrefix}_Update";
				var updateTrigger = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{updateTriggerName}') AND type in (N'TR'))
EXEC dbo.sp_executesql @statement = N'
CREATE TRIGGER {updateTriggerName}
ON {tableName}
AFTER UPDATE
AS
BEGIN
	IF App_Name() = ''{Constants.AppName}''
		RETURN
	INSERT INTO {mappingTableName} ({triggerColumns}, {Constants.KeyMappingTableCreatedDate})
		SELECT {triggerColumns}, SYSUTCDATETIME()
			FROM INSERTED
	{GetInsertRequestSql(Operation.Update, mapping.Type)}
END
'";
				//insert trigger
				var insertTriggerName = $"{tableNormalName}_{Constants.KeyMappingPrefix}_Insert";
				var insertTrigger = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{insertTriggerName}') AND type in (N'TR'))
EXEC dbo.sp_executesql @statement = N'
CREATE TRIGGER {insertTriggerName}
ON {tableName}
AFTER INSERT
AS
BEGIN
	IF App_Name() = ''{Constants.AppName}''
		RETURN
	INSERT INTO {mappingTableName} ({triggerColumns}, {Constants.KeyMappingTableCreatedDate})
		SELECT {triggerColumns}, SYSUTCDATETIME()
			FROM INSERTED
	{GetInsertRequestSql(Operation.Insert, mapping.Type)}
END
'";

				//delete trigger
				var deleteTriggerName = $"{tableNormalName}_{Constants.KeyMappingPrefix}_Delete";
				var deleteTrigger = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{deleteTriggerName}') AND type in (N'TR'))
EXEC dbo.sp_executesql @statement = N'
CREATE TRIGGER {deleteTriggerName}
ON {tableName}
AFTER DELETE
AS
BEGIN
	IF App_Name() = ''{Constants.AppName}''
		RETURN
	{GetInsertRequestSql(Operation.Delete, mapping.Type)}
END
'";
				if (dataAccess.Execute($@"{updateTrigger}
{insertTrigger}
{deleteTrigger}") != SqlServerUtils.NonDataAffected)
					Progress?.Invoke(this, new ProgressEventArgs { Message = $"Triggers for mapping table created: {mappingTableName}" });

				#endregion
			}
		}

		private void InitializeSyncTables()
		{
			var requestTableName = SqlServerUtils.NormalizeName(config.Source.RequestTable.Table);
			var settingTableName = SqlServerUtils.NormalizeName(config.Source.SettingTable.Table);
			var sql = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{RequestTable}') AND type in (N'U'))
	CREATE TABLE {RequestTable}(
		[Id] [bigint] IDENTITY(1,1) NOT NULL,
		[Operation] [int] NOT NULL,
		[ResourceId] [bigint] NOT NULL,
		[Type] [int] NOT NULL,
		[CreatedDate] [datetime2](7) NOT NULL,
		[FinishedDate] [datetime2](7) NULL,
	 CONSTRAINT [PK_{requestTableName}] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)
	)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{SettingTable}') AND type in (N'U'))
BEGIN
	CREATE TABLE {SettingTable}(
		[Code] [nvarchar](100) NOT NULL,
		[Value] [nvarchar](500) NOT NULL,
		[Description] [nvarchar](500) NULL,
	 CONSTRAINT [PK_{settingTableName}] PRIMARY KEY CLUSTERED 
	(
		[Code] ASC
	)
	)
	INSERT {SettingTable} ([Code], [Value], [Description]) VALUES (N'{SqlServerUtils.CodeHighWatermark}', N'0', NULL)
END";
			dataAccess.Execute(sql);
		}

		IEnumerable<ColumnMapping> GetPkColumnMappings(Mapping mapping) => mapping.Source.Pks.Select(x => new ColumnMapping { Source = x });

		string SettingTable => SqlServerUtils.QuoteName(config.Source.SettingTable);

		string RequestTable => SqlServerUtils.QuoteName(config.Source.RequestTable);

		string GetInsertRequestSql(Operation operation, int type) => $@"INSERT INTO {RequestTable} ({Constants.KeyOperation}, {Constants.KeyResourceId}, {Constants.KeyType}, {Constants.KeyCreatedDate})
			SELECT {(int)operation}, @@IDENTITY, {type}, SYSUTCDATETIME() FROM INSERTED";

		#endregion

		public string Id => "SqlServer";

		long LastHighWatermark { get; set; } = -1;

		public long GetHighWatermak()
		{
			return dataAccess.GetValue<long>($"SELECT CAST(Value AS bigint) FROM {SettingTable} WHERE Code = @Code", new Dictionary<string, object> { { "@Code", SqlServerUtils.CodeHighWatermark } });
		}

		public Dictionary<string, long> Stats => new Dictionary<string, long>
			{
				{ "RunCount", runCount },
				{ "ExecutedCount", executedCount },
				{ "RequestCount", requestCount },
				{ "ProcessedCount", processedCount },
				{ "HistoryDeleteCount", historyDeleteCount},
				{ "ProcessedDeleteCount", processedDeleteCount},
				{ "ProcessedInsertCount", processedInsertCount},
				{ "ProcessedUpdateCount", processedUpdateCount}
			};

		private void Process()
		{
			Interlocked.Increment(ref runCount);
			var lockTaken = false;
			try
			{
				lockTaken = Monitor.TryEnter(syncRoot);
				if (lockTaken)
					ProcessCore();
			}
			finally
			{
				if (lockTaken)
					Monitor.Exit(syncRoot);
			}
		}

		private void ProcessCore()
		{
			var highWatermark = GetHighWatermak();
			if (highWatermark > LastHighWatermark || highWatermark == 0)
			{
				Interlocked.Increment(ref executedCount);
				Run(highWatermark);
			}
		}

		private void Run(long highWatermark)
		{
			dataAccess.DoBiz($"SELECT TOP (@BufferSize) Id, Type, Operation, ResourceId, CreatedDate FROM {RequestTable} WHERE Id > @HighWatermark", x =>
			{
				using (var reader = x.ExecuteReader())
				{
					while (reader.Read())
					{
						if (processCancelled)
							break;
						Interlocked.Increment(ref requestCount);
						SyncRequest request = null;
						try
						{
							var id = (long)reader["Id"];
							var type = (int)reader["Type"];
							var operation = (Operation)(int)reader["Operation"];
							var resourceId = (long)reader["ResourceId"];
							var mapping = config.Mappings.First(m => m.Type == type);
							var columns = SqlServerUtils.ConcatByComma(GetPkColumnMappings(mapping).Concat(new[] { new ColumnMapping { Source = Constants.KeyMappingTableCreatedDate } }).Concat(mapping.Columns).Select(c => SqlServerUtils.QuoteName(c.Source)));
							var tableNormalName = $"{SqlServerUtils.NormalizeName(mapping.Source)}";
							var mappingTableName = $"{Constants.KeyMappingPrefix}_{tableNormalName}";
							var data = dataAccess.GetData($"SELECT {columns} FROM {mappingTableName} WHERE {Constants.KeyMappingTablePk} = @Pk", new Dictionary<string, object> { { "@Pk", resourceId } });
							if (data.Rows.Count > 0)
							{
								var createdDate = (DateTime)data.Rows[0][Constants.KeyMappingTableCreatedDate];
								request = new SyncRequest { Id = id, Type = type, Operation = operation, CreatedDate = createdDate, TaskType = config.Source.Type, TaskName = config.Name };
								Progress?.Invoke(this, new ProgressEventArgs { Message = $"Request found", Request = request });
								request.ResourceIds.AddRange(mapping.Source.Pks.Select(pk => data.Rows[0][SqlServerUtils.NormalizeNameWithSpace(pk)]));
								if (onRequest(request, data).GetAwaiter().GetResult())
								{
									Interlocked.Increment(ref processedCount);
									switch (operation)
									{
										case Operation.Update:
											Interlocked.Increment(ref processedUpdateCount);
											break;
										case Operation.Insert:
											Interlocked.Increment(ref processedInsertCount);
											break;
										case Operation.Delete:
											Interlocked.Increment(ref processedDeleteCount);
											break;
									}

									string historySql;
									switch (config.Source.HistoryStrategy.Type)
									{
										case HistoryStrategyType.ProcessAndDelete:
											Interlocked.Increment(ref historyDeleteCount);
											historySql = $@"DELETE FROM {mappingTableName} WHERE {Constants.KeyMappingTablePk} = @Pk
											DELETE FROM {RequestTable} WHERE Id = @Id";
											break;
										case HistoryStrategyType.None:
											historySql = $"UPDATE {RequestTable} SET FinishedDate = SYSUTCDATETIME() WHERE Id = @Id";
											break;
										default:
											historySql = string.Empty;
											break;
									}
									dataAccess.Execute($@"UPDATE {SettingTable} SET Value = @HighWatermark WHERE Code = @Code
										{historySql}"
										, new Dictionary<string, object> { { "@Id", id }, { "@Code", SqlServerUtils.CodeHighWatermark }, { "@HighWatermark", highWatermark }, { "@Pk", resourceId } });
								}
							}
							else
								Progress?.Invoke(this, new ProgressEventArgs { Message = $"Requested record not found", Request = request });
							LastHighWatermark = highWatermark;
						}
						catch (Exception ex)
						{
							Progress?.Invoke(this, new ProgressEventArgs { Message = $"Exception encountered while processing request: {ex.Message}", Request = request });
						}
					}
				}
			}, new Dictionary<string, object> { { "@BufferSize", config.BufferSize }, { "@HighWatermark", highWatermark } });
		}

		public void ProcessHistory()
		{
			foreach (var mapping in config.Mappings)
			{
				var tableNormalName = SqlServerUtils.NormalizeName(mapping.Source);
				var mappingTableName = $"{Constants.KeyMappingPrefix}_{tableNormalName}";
				var indexColumns = SqlServerUtils.ConcatByComma(mapping.Source.Pks.Select(x => SqlServerUtils.QuoteName(x)));
				var highWatermark = GetHighWatermak();
				//keep most recent records for conflict policy verification
				var affected = dataAccess.Execute($@"DELETE r
		FROM {RequestTable} r 
		INNER JOIN (SELECT ROW_NUMBER() OVER (PARTITION BY Type, ResourceId ORDER BY CreatedDate DESC) AS RowId, Id FROM {RequestTable} WHERE Id < @HighWatermark AND Type = @Type) h ON r.Id = h.Id
		WHERE h.RowId > 1
	DELETE m
		FROM {mappingTableName} m
		INNER JOIN (SELECT ROW_NUMBER() OVER (PARTITION BY {indexColumns} ORDER BY {Constants.KeyMappingTableCreatedDate} DESC) AS RowId, {Constants.KeyMappingTablePk} FROM {mappingTableName} WHERE {Constants.KeyMappingTablePk} < @HighWatermark AND Type = @Type) h ON m.{Constants.KeyMappingTablePk} = h.{Constants.KeyMappingTablePk}
		WHERE h.RowId > 1"
					, new Dictionary<string, object> { { "@HighWatermark", highWatermark }, { "@Type", mapping.Type } });
				Interlocked.Add(ref processedUpdateCount, affected);
			}
		}
	}
}
