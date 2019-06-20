namespace DatabaseBatch.Infrastructure
{
    public class MySqlReader : BaseSqlReader
    {
        public MySqlReader(string sql) : base(sql, new string[] { ";", "$$"})
        {
        }
    }
}
