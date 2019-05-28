using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public abstract class BaseSqlManager : ISqlManager
    {
        public abstract void Init(Config config);
        public abstract void MakeScript();
        public abstract void Publish();

        protected Config _config;

        protected Dictionary<string, TableInfoModel> GetMySqlTableInfo(IDbConnection conn)
        {
            var tables = new Dictionary<string, TableInfoModel>();
            var sqlCommand = $"SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE  FROM Information_schema.columns WHERE table_schema = '{conn.Database}'";
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.Connection = conn;
            cmd.CommandText = sqlCommand;
            cmd.CommandType = CommandType.Text;

            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tableName = reader["TABLE_NAME"].ToString().ToLower();
                var columnName = reader["COLUMN_NAME"].ToString().ToLower();
                var columnTypes = reader["COLUMN_TYPE"].ToString().ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var columnOption = "";
                if (columnTypes.Count() > 1)
                {
                    columnOption = columnTypes.Skip(1).Aggregate((opton, next) => $"{opton} {next}");
                }
                var column = new ColumnModel()
                {
                    TableName = tableName,
                    ColumnName = columnName,
                    ColumnType = columnTypes[0],
                    ColumnOptions = columnOption
                };

                if (!tables.ContainsKey(column.TableName))
                {
                    tables.Add(column.TableName, new TableInfoModel() { Columns = new List<ColumnModel>() });
                }
                tables[column.TableName].Columns.Add(column);
            }
            conn.Close();
            return tables;
        }
    }
}
