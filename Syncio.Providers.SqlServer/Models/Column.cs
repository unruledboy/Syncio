namespace Syncio.Providers.SqlServer.Models
{
	class Column
	{
		public string ColumnName { get; set; }
		public string DataType { get; set; }
		public int Position { get; set; }
		public int MaxLength { get; set; }
		public string NumericPrecision { get; set; }
		public bool IsNullable { get; set; }
	}
}