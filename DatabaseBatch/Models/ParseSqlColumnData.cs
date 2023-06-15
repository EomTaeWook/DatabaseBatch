using DatabaseBatch.Infrastructure;

namespace DatabaseBatch.Models
{
    public class SqlParseColumnData : ColumnModel
    {
        public CommandType CommandType { get; set; } = CommandType.Max;

        public ClassificationType ClassificationType { get; set; } = ClassificationType.Max;

        public string ChangeColumnName { get; set; }
    }

    public class SqlParseTableData
    {
        public string TableName { get; set; }

        public CommandType CommandType { get; set; }

        public List<SqlParseColumnData> SqlParseColumnDatas { get; set; } = new List<SqlParseColumnData>();
    }

}
