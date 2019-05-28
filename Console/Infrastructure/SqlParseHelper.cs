using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public class SqlParseHelper
    {
        private static readonly string[] _reservedBeginKeyword = new string[]
        {
            "PRIMARY KEY",
            "INDEX ",
            "INDEX`",
            "INDEX(",
            "FOREIGN KEY",
            "CONSTRAINT",
        };
        private static readonly string[] _foreignKeyOptionKeyword = new string[]
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

        public static TableInfoModel ParseMysqlDDLCommnad(string sql)
        {
            var keyword = "CREATE TABLE";
            var openIndex = sql.IndexOf("(");
            var closeIndex = sql.LastIndexOf(")");

            if (openIndex == -1)
                throw new Exception($"SQL : {sql}");

            var line = sql.Substring(0, openIndex);
            var tableNameIndex = line.ToUpper().IndexOf(keyword) + keyword.Length;

            var tableName = sql.Substring(tableNameIndex, openIndex - tableNameIndex).Replace("`", "").Trim();
            var body = sql.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();

            var findIndex = 0;
            var beforeIndex = 0;
            var isReservedKeyword = false;

            var tableInfoData = new TableInfoModel()
            {
                TableName = tableName,
                Columns = new List<ColumnModel>(),
            };

            while (findIndex <= body.Count())
            {
                beforeIndex = findIndex;
                findIndex = body.IndexOf(",", findIndex);
                if (findIndex == -1)
                {
                    findIndex = body.Count();
                }

                line = body.Substring(beforeIndex, findIndex - beforeIndex).Trim();
                isReservedKeyword = false;
                for (int i = 0; i < _reservedBeginKeyword.Count(); i++)
                {
                    isReservedKeyword = line.ToUpper().StartsWith(_reservedBeginKeyword[i]);
                    if (isReservedKeyword)
                    {
                        if (!_reservedBeginKeyword[i].Equals("CONSTRAINT") && !_reservedBeginKeyword[i].Equals("FOREIGN KEY"))//base
                        {
                            findIndex = body.IndexOf(")", beforeIndex) + 1;
                        }
                        else//외래키인 경우
                        {
                            beforeIndex = body.IndexOf("REFERENCES", beforeIndex) + 1;
                            findIndex = body.IndexOf(")", beforeIndex) + 1;
                            foreach (var word in _foreignKeyOptionKeyword)
                            {
                                var tempIndex = body.IndexOf(word, findIndex);
                                if (tempIndex > findIndex)
                                    findIndex = tempIndex + word.Count();
                            }
                        }
                        break;
                    }
                }
                if (isReservedKeyword == false)
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

                findIndex++;
            }
            return tableInfoData;
        }

        public static string AlterMySqlColumn(ColumnModel model, AlterTableType type)
        {
            return $"ALTER TABLE `{model.TableName}` {type.ToString()} COLUMN `{model.ColumnName}` {(type != AlterTableType.Drop ? $"{model.ColumnType} {model.ColumnOptions}" : "")};";
        }
    }
}
