using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public class SqlParseHelper
    {
        public static Tuple<string, List<TableInfoModel>> ParseMysqlDDLCommnad(string sql)
        {
            var keyword = "CREATE TABLE";
            var openIndex = sql.IndexOf("(");
            var closeIndex = sql.LastIndexOf(")");
            var line = sql.Substring(0, openIndex);
            var tableNameIndex = line.ToUpper().IndexOf(keyword) + keyword.Length;

            var tableName = sql.Substring(tableNameIndex, openIndex - tableNameIndex).Replace("`", "").Trim();
            var body = sql.Substring(openIndex + 1, closeIndex - openIndex - 1);
            var columnDatas = body.Split(',');

            var columns = new List<TableInfoModel>();
            for(int i=0; i< columnDatas.Length; i++)
            {
                var datas = columnDatas[i].Trim().Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries).ToList();
                var option = "";
                for (int ii=2; ii< datas.Count; ii++)
                {
                    option += $"{datas[ii]} ";
                }
                columns.Add(new TableInfoModel()
                {
                    ColumnName = datas[0].Replace("`", ""),
                    ColumnType = datas[1],
                    Options = option,
                    TableName = tableName
                });
            }

            return Tuple.Create(tableName, columns);
        }
        
        public static string AlterMySqlColumn(TableInfoModel model, AlterTableType type)
        {
            return $"ALTER TABLE `{model.TableName}` {type.ToString()} COLUMN `{model.ColumnName}` {(type != AlterTableType.Drop ?$"{model.ColumnType} {model.Options}" : "")};";
        }
    }
}
