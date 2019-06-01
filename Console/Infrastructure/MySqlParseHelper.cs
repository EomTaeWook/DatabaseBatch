using DatabaseBatch.Extensions;
using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlParseHelper
    {
        public static bool ParseAlterCommnad(string sql, out List<ParseSqlData> parseSqlDatas)
        {
            parseSqlDatas = new List<ParseSqlData>();
            var replacement = new string[] { "\r\n", "\t" };
            var findIndex = 0;
            var beforeIndex = findIndex;
            var line = "";
            var findKeyword = new char[] { ';' };

            while(findIndex <= sql.Count())
            {
                beforeIndex = findIndex;
                findIndex = sql.IndexOfAny(findKeyword, findIndex);
                if (findIndex == -1)
                {
                    break;
                }
                line = sql.Substring(beforeIndex, findIndex - beforeIndex).Trim().Replace(replacement, " ");
                if(line.ToUpper().StartsWith("USE"))
                {
                    findIndex = line.Count() + 1;
                    continue;
                }
                if(!ParseSubAlterTableCommand(line, out ParseSqlData parseSqlData))
                {
                    throw new Exception($"sql : {sql}");
                }
                parseSqlDatas.Add(parseSqlData);
                findIndex++;
            }
            return true;

        }
        private static bool ParseSubAlterTableCommand(string line, out ParseSqlData parseSqlData)
        {
            var table = "ALTER TABLE ";
            parseSqlData = new ParseSqlData();
            var findIndex = line.IndexOf(' ', table.Length);

            var tableName = line.Substring(table.Length, findIndex - table.Length).Replace("`", "");
            var beforeFindIndex = findIndex++;

            while (findIndex <= line.Length)
            {
                beforeFindIndex = findIndex;
                findIndex = line.IndexOfAny(new char[] { ',', ';' }, findIndex);
                if (findIndex == -1)
                    findIndex = line.Length;

                var newLine = line.Substring(beforeFindIndex, findIndex - beforeFindIndex).Trim();
                if (newLine.ToUpper().StartsWith(table))
                {
                    return false;
                }

                //Command 뜯어내고
                var commandIndex = newLine.IndexOf(" ");
                var command = newLine.Substring(0, commandIndex++);

                //이후 무엇이 바뀌었는지 뜯어낸다.
                var body = newLine.Substring(commandIndex, newLine.Length - commandIndex);
                if (!Enum.TryParse(command, true, out CommandType type))
                {
                    return false;
                }
                parseSqlData.TableName = tableName;
                parseSqlData.CommandType = type;
                var isReserved = false;
                for (int i = 0; i < Consts.MySqlReservedKeyword.Count(); i++)
                {
                    if (body.ToUpper().StartsWith(Consts.MySqlReservedKeyword[i]))
                    {
                        isReserved = true;
                        break;
                    }
                }
                var columnNameIndex = isReserved ? 1 : 0;
                if (isReserved == false && type == CommandType.Alter)
                {
                    var columnInfo = body.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    parseSqlData.ColumnName = columnInfo[columnNameIndex].Replace("`", "");
                    parseSqlData.Command = body;
                }
                else if (isReserved == false || body.ToUpper().StartsWith("COLUMN"))
                {
                    parseSqlData.ClassificationType = ClassificationType.Columns;
                    var columnInfo = body.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var typeIndex = isReserved ? 2 : 1;
                    if (type == CommandType.Change)
                    {
                        parseSqlData.ChangeColumnName = columnInfo[2].Replace("`", "");
                        typeIndex++;
                    }
                    parseSqlData.ColumnName = columnInfo[columnNameIndex].Replace("`", "");
                    if (Consts.BaseMySqlDataType.ContainsKey(columnInfo[typeIndex].ToLower()))
                    {
                        parseSqlData.ColumnType = columnInfo[typeIndex] = Consts.BaseMySqlDataType[columnInfo[typeIndex].ToLower()];
                    }
                    typeIndex++;
                    parseSqlData.ColumnOptions = "";
                    for (int i = typeIndex; i < columnInfo.Count(); i++)
                    {
                        parseSqlData.ColumnOptions += columnInfo[i] + " ";
                    }
                }
                else
                {
                    foreach (var keyword in Consts.MySqlReservedKeyword)
                    {
                        if (!body.ToUpper().StartsWith(keyword))
                            continue;
                        var temp = new string(body.Skip(keyword.Length).ToArray()).Replace("`", " ").Trim();
                        var index = temp.IndexOfAny(new char[] { ',', ';', ' ' });
                        if (index == -1)
                        {
                            index = temp.Length;
                        }
                        parseSqlData.ColumnName = temp.Substring(0, index).Replace("`", "");
                        break;
                    }
                    parseSqlData.Command = body;
                }
                findIndex++;
            }

            return true;
        }
        public static bool ParseCreateTableCommnad(string sql , out TableInfoModel tableInfoData)
        {
            var keyword = "CREATE TABLE";
            var openIndex = sql.IndexOf("(");
            var closeIndex = sql.LastIndexOf(")");
            tableInfoData = new TableInfoModel();

            if (openIndex == -1)
            {
                throw new Exception($"CrateTable Parse Error: {sql}");
            }
            var line = sql.Substring(0, openIndex);
            var tableNameIndex = line.ToUpper().IndexOf(keyword) + keyword.Length;

            var tableName = sql.Substring(tableNameIndex, openIndex - tableNameIndex).Replace("`", "").Trim();
            var body = sql.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();

            var findIndex = 0;
            var beforeIndex = 0;
            var isReservedKeyword = false;

            tableInfoData.TableName = tableName;

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
                for (int i = 0; i < Consts.MySqlReservedKeyword.Count(); i++)
                {
                    isReservedKeyword = line.ToUpper().StartsWith(Consts.MySqlReservedKeyword[i]);
                    if (isReservedKeyword)
                    {
                        if (!Consts.MySqlReservedKeyword[i].Equals("CONSTRAINT") && !Consts.MySqlReservedKeyword[i].Equals("FOREIGN KEY"))//base
                        {
                            findIndex = body.IndexOf(")", beforeIndex) + 1;
                        }
                        else//외래키인 경우
                        {
                            beforeIndex = body.IndexOf("REFERENCES", beforeIndex) + 1;
                            findIndex = body.IndexOf(")", beforeIndex) + 1;
                            foreach (var word in Consts.MySqlFKOptionKeyword)
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
                        if(datas[ii].StartsWith("(") && datas[ii].EndsWith(")"))
                        {
                            datas[1] += datas[ii];
                        }
                        else
                        {
                            option += $"{datas[ii]} ";
                        }
                        
                    }
                    if(Consts.BaseMySqlDataType.ContainsKey(datas[1]))
                    {
                        datas[1] = Consts.BaseMySqlDataType[datas[1]];
                    }
                    var column = new ParseSqlData()
                    {
                        ColumnName = datas[0].Replace("`", ""),
                        ColumnType = datas[1],
                        ColumnOptions = option,
                        TableName = tableName
                    };
                    tableInfoData.Columns.Add(column.ColumnName, column);
                }

                findIndex++;
            }
            return true;
        }

        public static string AlterMySqlColumnChange(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` CHANGE COLUMN `{model.ColumnName}` `{model.ChangeColumnName}` {model.ColumnType} {model.ColumnOptions};";
        }
        public static string AlterMySqlColumn(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` {model.CommandType.ToString().ToUpper()} COLUMN `{model.ColumnName}` {model.ColumnType} {(model.CommandType != CommandType.Drop ? $"{model.ColumnOptions}" : "")};";
        }
        public static string CreateSqlCommand(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` {model.CommandType.ToString().ToUpper()} {model.Command};";
        }
    }
}
