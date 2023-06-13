namespace DatabaseBatch.Models
{
    public class Config
    {
        public string TablePath { get; set; }

        public string AlterTablePath { get; set; }

        public string StoredProcedurePath { get; set; }

        public DBConfig DbConfig { get; set; }

        public Publish Publish { get; set; }
    }
}
