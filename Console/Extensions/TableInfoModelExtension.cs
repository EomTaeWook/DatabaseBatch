using DatabaseBatch.Models;

namespace DatabaseBatch.Extensions
{
    public static class ColumnModelExtension
    {
        public static bool NameCompare(this ColumnModel obj, ColumnModel other)
        {
            return obj.ColumnName.ToLower() == other.ColumnName.ToLower();
        }

        public static bool TypeCompare(this ColumnModel obj, ColumnModel other)
        {
            return obj.ColumnType.ToLower() == other.ColumnType.ToLower();
        }
    }
}
