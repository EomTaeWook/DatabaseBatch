using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using System.Collections.Generic;
using System.Data;

namespace DatabaseBatch.Infrastructure
{
    public abstract class BaseSqlManager : ISqlManager
    {
        public abstract void Init(Config config);
        public abstract void MakeScript();
        public abstract void Publish();

        protected Config _config;

        protected Dictionary<string, List<TableInfoModel>> GetMySqlTableInfo(IDbConnection conn)
        {
            var tables = new Dictionary<string, List<TableInfoModel>>();
            var sqlCommand = $"SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE  FROM Information_schema.columns WHERE table_schema = '{conn.Database}'";
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.Connection = conn;
            cmd.CommandText = sqlCommand;
            cmd.CommandType = CommandType.Text;

            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var model = new TableInfoModel()
                {
                    TableName = reader["TABLE_NAME"].ToString().ToLower(),
                    ColumnName = reader["COLUMN_NAME"].ToString().ToLower(),
                    ColumnType = reader["COLUMN_TYPE"].ToString().ToLower(),
                };
                if (!tables.ContainsKey(model.TableName))
                {
                    tables.Add(model.TableName, new List<TableInfoModel>());
                }
                tables[model.TableName].Add(model);
            }
            conn.Close();
            return tables;
        }
    }
}
