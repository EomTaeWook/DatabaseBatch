
namespace DatabaseBatch.Models
{
    public class TableInfoModel
    {
        public string TableName { get; set; }

        public Dictionary<string, SqlParseColumnData> Columns { get; set; } = new Dictionary<string, SqlParseColumnData>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> IndexNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ForeignKeyNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
