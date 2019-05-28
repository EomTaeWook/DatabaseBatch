using System.Collections.Generic;

namespace DatabaseBatch.Infrastructure
{
    public class Consts
    {
        public static string ConfigPath = "./config.json";
        public static string OutputScript = "./deployment.sql";

        public static Dictionary<string, string> BaseMySqlDataType = new Dictionary<string, string>()
        {
            {"int", "int(11)" },
            { "varchar", "varchar(100)"},
            { "long", "mediumtext"},
            { "bigint", "bigint(20)"},
        };
    }
}
