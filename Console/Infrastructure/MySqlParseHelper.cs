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
            var replacement = new string[] { "\r\n", "\t", "\n" };
            var fileReadIndex = 0;
            var findKeyword = new char[] { ';' };

            while(fileReadIndex <= sql.Count())
            {
                int beforeFileIndex = fileReadIndex;
                fileReadIndex = sql.IndexOfAny(findKeyword, fileReadIndex);
                if (fileReadIndex == -1)
                {
                    break;
                }
                string line = sql.Substring(beforeFileIndex, fileReadIndex - beforeFileIndex).Trim().Replace(replacement, " ");
                if (line.ToUpper().StartsWith("USE"))
                {
                    fileReadIndex = line.Count() + 1;
                    continue;
                }

                string table = "ALTER TABLE ";
                int subFindIndex = line.IndexOf(' ', table.Length);
                var tableName = line.Substring(table.Length, subFindIndex - table.Length).Replace("`", "");
                while (subFindIndex <= line.Length)
                {
                    int subBeforeFindIndex = subFindIndex++;
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
            //Command를 뜯어내고
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
                //Index, 외래키 기본키 이름 뜯기
                foreach (var key in Consts.MySqlReservedKeyword.Keys)
                {
                    if (!body.ToUpper().StartsWith(key))
                        continue;

                    foreach(var value in Consts.MySqlReservedKeyword[key])
                    {
                        var idx = body.ToUpper().IndexOf(value);
                        if (idx == -1)
                            idx = body.Length;
                        var nameZone = body.Substring(key.Length, idx - key.Length);
                        nameZone = nameZone.Replace("`", " ").Trim();
                        var splits = nameZone.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (splits.Length == 0)
                            parseSqlData.ColumnName = "";
                        else
                            parseSqlData.ColumnName = splits[0];
                        break;
                    }
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
                throw new Exception($"CreateTable Parse Error: {sql}");
            }
            var line = sql.Substring(0, openIndex);
            var tableNameIndex = line.ToUpper().IndexOf(keyword) + keyword.Length;

            var tableName = sql.Substring(tableNameIndex, openIndex - tableNameIndex).Replace("`", "").Trim();
            var body = sql.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();

            var findIndex = 0;
            tableInfoData.TableName = tableName;

            while (findIndex <= body.Length)
            {
                int beforeIndex = findIndex;
                findIndex = body.IndexOf(",", findIndex);
                if (findIndex == -1)
                {
                    findIndex = body.Length;
                }

                line = body.Substring(beforeIndex, findIndex - beforeIndex).Trim();
                bool isReservedKeyword = false;
                var queue = new Queue<string>();
                foreach (var key in Consts.MySqlReservedKeyword.Keys)
                {
                    isReservedKeyword = line.ToUpper().StartsWith(key);
                    if (isReservedKeyword == false)
                        continue;
                    queue.Enqueue(key);
                    break;
                }
                if(isReservedKeyword == false)//기본 컬럼
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
                else
                {
                    int maxIndex = 0;
                    int minIndex = beforeIndex;
                    while (queue.Count > 0)
                    {
                        var item = queue.Dequeue();
                        
                        var index = body.IndexOf(item, minIndex);
                        if (index == -1)
                        {
                            maxIndex = body.Length;
                        }
                        if (maxIndex <= index)
                        {
                            maxIndex = index + item.Length;
                        }
                        findIndex = maxIndex;
                        //InputManager.Instance.Write(body.Substring(beforeIndex, findIndex - beforeIndex).Trim());

                        minIndex = findIndex;
                        if (!Consts.MySqlReservedKeyword.ContainsKey(item))
                            continue;
                        foreach (var key in Consts.MySqlReservedKeyword[item])
                        {
                            queue.Enqueue(key);
                        }
                    }
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
