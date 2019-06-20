namespace DatabaseBatch.Models
{
    public class Config
    {
        public string TablePath { get; set; }

        public string AlterTablePath { get; set; }

        public string StoredProcedurePath { get; set; }

        public string SqlConnect { get; set; }

        public Publish Publish { get; set; }
    }
}
