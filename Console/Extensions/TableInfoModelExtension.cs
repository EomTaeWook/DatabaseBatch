using DatabaseBatch.Models;

namespace DatabaseBatch.Extensions
{
    public static class TableInfoModelExtension
    {
        public static bool NameCompare(this TableInfoModel obj, TableInfoModel other)
        {
            return obj.ColumnName.ToLower() == other.ColumnName.ToLower();
        }

        public static bool TypeCompare(this TableInfoModel obj, TableInfoModel other)
        {
            return obj.ColumnType.ToLower() == other.ColumnType.ToLower();
        }
    }
}
