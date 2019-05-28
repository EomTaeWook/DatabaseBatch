using System.Collections.Generic;

namespace DatabaseBatch.Models
{
    public class TableInfoModel
    {
        public string TableName { get; set; }
        public string TableOption { get; set; } = "";
        public Dictionary<string, ParseSqlData> Columns { get; set; } = new Dictionary<string, ParseSqlData>();
    }
}
