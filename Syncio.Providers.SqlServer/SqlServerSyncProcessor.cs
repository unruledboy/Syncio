using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Providers.SqlServer
{
	public class SqlServerSyncProcessor : ISyncProcessor
	{
		public event EventHandler<ProgressEventArgs> Progress;
		private SyncRole role;
		private TaskConfig config;
		private readonly ConcurrentDictionary<string, SqlDataAccess> dataAccesses = new ConcurrentDictionary<string, SqlDataAccess>();
		private long processedDeleteCount = 0;
		private long processedAndDeletedCount = 0;
		private long processedUpdateCount = 0;
		private long processedAndUpdatedCount = 0;
		private long processedInsertCount = 0;
		private long processedAndInsertedCount = 0;

		public void Setup(SyncRole role, TaskConfig config)
		{
			this.role = role;
			this.config = config;

            foreach (var target in config.Targets)
            {
                var dataAccess = new SqlDataAccess(target.ConnectionString);
                foreach (var mapping in config.Mappings)
                    MappingInitializer.Initialize(mapping, dataAccess);
                dataAccesses.TryAdd(target.Id, dataAccess);
            }
        }

		public string Id => "SqlServer";

		public Dictionary<string, long> Stats => new Dictionary<string, long>
			{
				{ "ProcessedDeleteCount", processedDeleteCount},
				{ "ProcessedAndDeletedCount", processedAndDeletedCount},
				{ "ProcessedInsertCount", processedInsertCount},
				{ "ProcessedAndInsertdCount", processedAndInsertedCount},
				{ "ProcessedUpdateCount", processedUpdateCount},
				{ "ProcessedAndUpdatedCount", processedAndUpdatedCount}
			};

		public Dictionary<string, bool> Run(SyncRequest request, object payload)
		{
			var mapping = config.Mappings.First(x => x.Type == request.Type);
			var data = Serializer.DeserializeText<DataTable>((string)payload);
			var row = data.Rows[0];
			var pkConditions = SqlServerUtils.ConcatByComma(mapping.Target.Pks.Select(x => $"{SqlServerUtils.QuoteName(x)} = @{SqlServerUtils.NormalizeNameWithSpace(x)}"));
			var table = $"{SqlServerUtils.QuoteName(mapping.Target.Schema)}.{SqlServerUtils.QuoteName(mapping.Target.Table)}";
            var result = new ConcurrentDictionary<string, bool>();

            Parallel.ForEach(config.Targets, target =>
            {
                int affected = ProcessRow(request, target, mapping, row, pkConditions, table);
                if (affected == SqlServerUtils.NonDataAffected)
                {
                    switch (target.RetryPolicy)
                    {
                        case RetryPolicy.RetryOnce:
                            affected = ProcessRow(request, target, mapping, row, pkConditions, table);
                            break;
                    }
                }
                result.TryAdd(target.Id, affected != SqlServerUtils.NonDataAffected);
            });

            return result.ToDictionary(x => x.Key, x => x.Value);
        }

        private int ProcessRow(SyncRequest request, SyncTarget target, Mapping mapping, DataRow row, string pkConditions, string table)
        {
            var dataAccess = dataAccesses[target.Id];
            var parameters = new Dictionary<string, object>(); //todo: what if pk is updated... WTF???
            var affected = SqlServerUtils.NonDataAffected;
            foreach (var pk in mapping.Target.Pks)
            {
                var columnName = SqlServerUtils.NormalizeNameWithSpace(pk);
                parameters.Add($"@{columnName}", row[columnName]);
            }
            switch (request.Operation)
            {
                case Operation.Update:
                    var tableNormalName = $"{SqlServerUtils.NormalizeName(mapping.Source)}";
                    var mappingTableName = $"{SqlServerUtils.QuoteName(SqlServerUtils.DefaultSchema)}.{Constants.KeyMappingPrefix}_{tableNormalName}";
                    var mostRecentUpdatedDate = dataAccess.GetValue<DateTime?>($@"IF EXISTS (SELECT NULL FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table AND TABLE_SCHEMA = @Schema)
	SELECT MAX(CreatedDate) FROM {mappingTableName} WHERE {pkConditions}
ELSE
	SELECT NULL", new Dictionary<string, object>(parameters) { { "@Table", mappingTableName }, { "@Schema", SqlServerUtils.QuoteName(mapping.Source.Schema) } });
                    var needToUpdate = false;
                    if (mostRecentUpdatedDate == null)
                        needToUpdate = true;
                    else if (request.CreatedDate > mostRecentUpdatedDate)
                        needToUpdate = true;
                    else if ((role == SyncRole.Hub && mapping.ConflictPolicy == ConflictPolicy.MemberWin)
                        || (role == SyncRole.Member && mapping.ConflictPolicy == ConflictPolicy.HubWin))
                        needToUpdate = true;
                    if (needToUpdate)
                    {
                        var updateColumns = SqlServerUtils.ConcatByComma(mapping.Columns.Select(x => $"{SqlServerUtils.QuoteName(x.Target)} = @{SqlServerUtils.NormalizeNameWithSpace(x.Target)}"));
                        Progress?.Invoke(this, new ProgressEventArgs { Message = $"Updating table {table}", Request = request });
                        foreach (var item in mapping.Columns)
                            parameters.Add($"@{SqlServerUtils.NormalizeNameWithSpace(item.Target)}", row[SqlServerUtils.NormalizeNameWithSpace(item.Source)]);
                        affected = dataAccess.Execute($"UPDATE {table} SET {updateColumns} WHERE {pkConditions}", parameters);
                    }
                    else
                        Progress?.Invoke(this, new ProgressEventArgs { Message = $"Update to table {table} ignored because of conflict", Request = request });
                    Interlocked.Increment(ref processedUpdateCount);
                    if (affected != SqlServerUtils.NonDataAffected)
                        Interlocked.Increment(ref processedAndUpdatedCount);
                    break;
                case Operation.Insert:
                    var pkColumns = mapping.Source.Pks.Select((x, i) => new ColumnMapping { Target = mapping.Target.Pks[i], Source = x });
                    var insertColumns = SqlServerUtils.ConcatByComma(pkColumns.Concat(mapping.Columns).Select(x => $"{SqlServerUtils.QuoteName(x.Target)}"));
                    var valueColumns = SqlServerUtils.ConcatByComma(pkColumns.Concat(mapping.Columns).Select(x => $"@{SqlServerUtils.NormalizeNameWithSpace(x.Target)}"));
                    Progress?.Invoke(this, new ProgressEventArgs { Message = $"Inserting into table {table}", Request = request });
                    foreach (var item in mapping.Columns)
                        parameters.Add($"@{SqlServerUtils.NormalizeNameWithSpace(item.Target)}", row[SqlServerUtils.NormalizeNameWithSpace(item.Source)]);
                    affected = dataAccess.Execute($"INSERT INTO {table} ({insertColumns}) VALUES ({valueColumns})", parameters);
                    Interlocked.Increment(ref processedInsertCount);
                    if (affected != SqlServerUtils.NonDataAffected)
                        Interlocked.Increment(ref processedAndInsertedCount);
                    break;
                case Operation.Delete:
                    Progress?.Invoke(this, new ProgressEventArgs { Message = $"Deleting from table {table}", Request = request });
                    affected = dataAccess.Execute($"DELETE FROM {table} WHERE {pkConditions}", parameters);
                    Interlocked.Increment(ref processedDeleteCount);
                    if (affected != SqlServerUtils.NonDataAffected)
                        Interlocked.Increment(ref processedAndDeletedCount);
                    break;
            }

            return affected;
        }
    }
}