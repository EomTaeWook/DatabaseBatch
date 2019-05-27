using System.Collections.Generic;

namespace DatabaseBatch.Models
{
    public class TableInfoModel
    {
        public string TableName { get; set; }
        public string TableOption { get; set; } = "";
        public List<ColumnModel> Columns { get; set; }
    }
}
