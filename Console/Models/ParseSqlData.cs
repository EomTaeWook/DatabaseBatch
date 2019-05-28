using DatabaseBatch.Infrastructure;

namespace DatabaseBatch.Models
{
    public class ParseSqlData : ColumnModel
    {
        public CommandType CommandType { get; set; }

        public ClassificationType ClassificationType { get; set; } = ClassificationType.Max;

        public string Command { get; set; }

        public string ChangeColumnName { get; set; }
    }
}
