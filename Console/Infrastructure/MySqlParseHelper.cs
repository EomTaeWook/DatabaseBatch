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
            var fileReadIndex = 0;
            var beforeFileIndex = fileReadIndex;
            var line = "";
            var findKeyword = new char[] { ';' };

            while(fileReadIndex <= sql.Count())
            {
                beforeFileIndex = fileReadIndex;
                fileReadIndex = sql.IndexOfAny(findKeyword, fileReadIndex);
                if (fileReadIndex == -1)
                {
                    break;
                }
                line = sql.Substring(beforeFileIndex, fileReadIndex - beforeFileIndex).Trim().Replace(replacement, " ");
                if(line.ToUpper().StartsWith("USE"))
                {
                    fileReadIndex = line.Count() + 1;
                    continue;
                }

                var subFindIndex = 0;
                var subBeforeFindIndex = subFindIndex;
                var table = "ALTER TABLE ";
                subFindIndex = line.IndexOf(' ', table.Length);
                var tableName = line.Substring(table.Length, subFindIndex - table.Length).Replace("`", "");
                while (subFindIndex <= line.Length)
                {
                    subBeforeFindIndex = subFindIndex++;
                    subFindIndex = line.IndexOfAny(new char[] { ',', ';' }, subFindIndex);
                    if (subFindIndex == -1)
                        subFindIndex = line.Length;

                    var newCommand = line.Substring(subBeforeFindIndex, subFindIndex - subBeforeFindIndex).Trim();

                    if (!ParseSubAlterTableCommand(newCommand, out ParseSqlData parseSqlData))
                    {
                        throw new Exception($"sql : {sql}");
                    }
                    parseSqlData.TableName = tableName;
                    parseSqlDatas.Add(parseSqlData);
                    subFindIndex++;
                }
                fileReadIndex++;
            }
            return true;

        }
        private static bool ParseSubAlterTableCommand(string line, out ParseSqlData parseSqlData)
        {
            parseSqlData = new ParseSqlData();
            var commandIndex = line.IndexOf(" ");
            var command = line.Substring(0, commandIndex++);

            //이후 무엇이 바뀌었는지 뜯어낸다.
            var body = line.Substring(commandIndex, line.Length - commandIndex);
            if (!Enum.TryParse(command, true, out CommandType type))
            {
                return false;
            }
            parseSqlData.CommandType = type;
            var isReserved = false;
            foreach (var key in Consts.MySqlReservedKeyword.Keys)
            {
                if (body.ToUpper().StartsWith(key))
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
                foreach (var key in Consts.MySqlReservedKeyword.Keys)
                {
                    if (!body.ToUpper().StartsWith(key))
                        continue;
                    int tempBeforeIndex = 0, tempAfterIndex = 0;
                    for (int i = 0; i < Consts.MySqlReservedKeyword[key].Count; i++)
                    {
                        tempBeforeIndex = body.IndexOf(Consts.MySqlReservedKeyword[key][i]);
                        if (tempBeforeIndex == -1)
                            continue;
                        if (Consts.MySqlReservedKeyword[key][i] != " " && Consts.MySqlReservedKeyword[key][i] != "`")
                        {
                            tempAfterIndex = tempBeforeIndex;
                            tempBeforeIndex = key.Length;
                            continue;
                        }
                        tempAfterIndex = body.IndexOf(Consts.MySqlReservedKeyword[key][i], tempBeforeIndex + 1);
                        if (tempAfterIndex == -1)
                            tempAfterIndex = body.Length;
                    }
                    parseSqlData.ColumnName = body.Substring(key.Length, tempAfterIndex - tempBeforeIndex).Replace("`", "").Trim();
                    break;
                }

                parseSqlData.Command = body;
            }
            return true;
        }
        public static bool ParseCreateTableCommnad(string sql , out TableInfoModel tableInfoData)
        {
            var keyword = "CREATE TABLE";
            var openIndex = sql.IndexOf("(");
            var closeIndex = sql.LastIndexOf(")");
            tableInfoData = new TableInfoModel()
            {
                Columns = new Dictionary<string, ParseSqlData>(),
            };
            
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

            while (findIndex <= body.Length)
            {
                beforeIndex = findIndex;
                findIndex = body.IndexOf(",", findIndex);
                if (findIndex == -1)
                {
                    findIndex = body.Length;
                }

                line = body.Substring(beforeIndex, findIndex - beforeIndex).Trim();
                isReservedKeyword = false;
                
                foreach(var key in Consts.MySqlReservedKeyword.Keys)
                {
                    isReservedKeyword = line.ToUpper().StartsWith(key);
                    if(isReservedKeyword)
                    {
                        if (!key.Equals("CONSTRAINT") && !key.Equals("FOREIGN KEY"))//base
                        {
                            findIndex = body.IndexOf(")", beforeIndex) + 1;
                        }
                        //else//외래키인 경우
                        //{
                        //    //var maxIndex = 0;
                        //    //for(int i=0; i< Consts.MySqlReservedKeyword[key].Count; i++)
                        //    //{
                        //    //    var index = line.IndexOf(Consts.MySqlReservedKeyword[key][i]);
                        //    //    if (maxIndex <= index)
                        //    //    {
                        //    //        maxIndex = index + Consts.MySqlReservedKeyword[key][i].Length;
                        //    //    }
                        //    //}
                        //    //foreach (var word in Consts.MySqlFKOptionKeyword)
                        //    //{
                        //    //    var index = line.IndexOf(word, maxIndex);
                        //    //    if (maxIndex < index)
                        //    //        maxIndex = index + word.Length;
                        //    //}
                        //    //findIndex += maxIndex;
                        //}
                        break;
                    }
                }
                if (isReservedKeyword == false)
                {
                    var datas = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    var option = "";
                    for (int ii = 2; ii < datas.Count; ii++)
                    {
                        if (datas[ii].StartsWith("(") && datas[ii].EndsWith(")"))
                        {
                            datas[1] += datas[ii];
                        }
                        else
                        {
                            option += $"{datas[ii]} ";
                        }

                    }
                    if (Consts.BaseMySqlDataType.ContainsKey(datas[1]))
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
