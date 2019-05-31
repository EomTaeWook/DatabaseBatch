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

        public static readonly string[] MySqlReservedKeyword = new string[]
        {
            "PRIMARY KEY",
            "INDEX ",
            "INDEX`",
            "INDEX(",
            "FOREIGN KEY",
            "CONSTRAINT",
           
            "COLUMN",
            "FULLTEXT ",
            "SPATIAL ",
            "UNIQUE INDEX",
            "FULLTEXT INDEX",
            "SPATIAL INDEX ",

        };
        public static readonly string[] MySqlFKOptionKeyword = new string[]
        {
            "ON UPDATE RESTRICT",
            "ON UPDATE CASCADE",
            "ON UPDATE SET NULL",
            "ON UPDATE NO ACTION",

            "ON DELETE RESTRICT",
            "ON DELETE CASCADE",
            "ON DELETE SET NULL",
            "ON DELETE NO ACTION",
        };
    }
}
