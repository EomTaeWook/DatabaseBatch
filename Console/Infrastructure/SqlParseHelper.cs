using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public class SqlParseHelper
    {
        private static readonly string[] _reservedKeyword = new string[]
        {
            "PRIMARY KEY",
            "INDEX",
            "FOREIGN KEY"
        };
        public static TableInfoModel ParseMysqlDDLCommnad(string sql)
        {
            var keyword = "CREATE TABLE";
            var openIndex = sql.IndexOf("(");
            var closeIndex = sql.LastIndexOf(")");
            var line = sql.Substring(0, openIndex);
            var tableNameIndex = line.ToUpper().IndexOf(keyword) + keyword.Length;

            var tableName = sql.Substring(tableNameIndex, openIndex - tableNameIndex).Replace("`", "").Trim();
            var body = sql.Substring(openIndex + 1, closeIndex - openIndex - 1);

            var findIndex = 0;
            var beforeIndex = 0;
            var isReservedKeyword = false;

            var tableInfoData = new TableInfoModel()
            {
                TableName = tableName,
                Columns = new List<ColumnModel>(),
            };

            while (true)
            {
                beforeIndex = findIndex;
                findIndex = body.IndexOf(",", findIndex);

                if (findIndex == -1)
                    break;
                
                line = body.Substring(beforeIndex, findIndex - beforeIndex).Trim();
                isReservedKeyword = false;
                for (int i=0; i<_reservedKeyword.Count(); i++)
                {
                    isReservedKeyword = line.ToUpper().Contains(_reservedKeyword[i]);
                    if (isReservedKeyword)
                        break;
                }
                if(isReservedKeyword == false)
                {
                    var datas = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    var option = "";
                    for (int ii = 2; ii < datas.Count; ii++)
                    {
                        option += $"{datas[ii]} ";
                    }
                    tableInfoData.Columns.Add(new ColumnModel()
                    {
                        ColumnName = datas[0].Replace("`", ""),
                        ColumnType = datas[1],
                        ColumnOptions = option,
                        TableName = tableName
                    });
                }
                else
                {
                    tableInfoData.TableOption = body.Substring(beforeIndex, body.Length - beforeIndex).Trim();
                    findIndex = body.Length - 1;
                }
                findIndex++;
            }
            return tableInfoData;
        }
        
        public static string AlterMySqlColumn(ColumnModel model, AlterTableType type)
        {
            return $"ALTER TABLE `{model.TableName}` {type.ToString()} COLUMN `{model.ColumnName}` {(type != AlterTableType.Drop ?$"{model.ColumnType} {model.ColumnOptions}" : "")};";
        }
    }
}
