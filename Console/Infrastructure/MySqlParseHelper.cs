using DatabaseBatch.Extensions;
using DatabaseBatch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlParseHelper
    {
        public static bool ParseMysqlAlterCommnad(string sql, out List<ParseSqlData> parseSqlDatas)
        {
            var table = "ALTER TABLE ";
            parseSqlDatas = new List<ParseSqlData>();
            var replacement = new string[] { "\r\n", "\t" };
            var findIndex = 0;
            var beforeIndex = findIndex;
            var line = "";
            var findKeyword = new char[] { ';' };

            while(findIndex<= sql.Count())
            {
                beforeIndex = findIndex;
                findIndex = sql.IndexOfAny(findKeyword, findIndex);
                if (findIndex == -1)
                {
                    break;
                }
                line = sql.Substring(beforeIndex, findIndex - beforeIndex).Trim().Replace(replacement, " ");

                var subBeforCommandFindIndex = 0;
                var subCommandFindIndex = line.IndexOf(' ', table.Length);
                
                var tableName = line.Substring(table.Length, subCommandFindIndex - table.Length).Replace("`", "");
                subBeforCommandFindIndex = subCommandFindIndex++;

                while(subCommandFindIndex <= line.Length)
                {
                    subBeforCommandFindIndex = subCommandFindIndex;
                    subCommandFindIndex = line.IndexOfAny(new char[] { ',', ';' }, subCommandFindIndex);
                    if (subCommandFindIndex == -1)
                        subCommandFindIndex = line.Length;

                    var newLine = line.Substring(subBeforCommandFindIndex, subCommandFindIndex - subBeforCommandFindIndex).Trim();

                    //Command 뜯어내고
                    var commandIndex = newLine.IndexOf(" ");
                    var command = newLine.Substring(0, commandIndex++);
                    
                    //이후 무엇이 바뀌었는지 뜯어낸다.
                    var body = newLine.Substring(commandIndex, newLine.Length - commandIndex);
                    var data = new ParseSqlData();
                    if (!Enum.TryParse(command, true, out CommandType type))
                    {
                        throw new Exception($"Alter Table Parse Error : {sql}");
                    }
                    data.TableName = tableName;
                    data.CommandType = type;
                    if (body.ToUpper().StartsWith("COLUMN"))
                    {
                        //컬럼이 변경인 경우 CreateTable에서 생성된 테이블에 변경점 적용
                        data.ClassificationType = ClassificationType.Columns;
                        var columnInfo = body.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                        var typeIndex = 2;
                        if (type == CommandType.Change)
                        {
                            data.ChangeColumnName = columnInfo[2].Replace("`", "");
                            typeIndex++;
                        }
                        data.ColumnName = columnInfo[1].Replace("`", "");
                        if (Consts.BaseMySqlDataType.ContainsKey(columnInfo[typeIndex].ToLower()))
                        {
                            data.ColumnType = columnInfo[typeIndex] = Consts.BaseMySqlDataType[columnInfo[typeIndex].ToLower()];
                        }
                        typeIndex++;
                        data.ColumnOptions = "";
                        for (int i= typeIndex; i< columnInfo.Count(); i++)
                        {
                            data.ColumnOptions += columnInfo[i] + " ";
                        }
                    }
                    else
                    {
                        data.Command = body;
                    }
                    parseSqlDatas.Add(data);
                    subCommandFindIndex++;
                }
                findIndex++;
            }
            return true;

        }
        public static bool ParseMysqlCreateTableCommnad(string sql , out TableInfoModel tableInfoData)
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

        public static string AlterMySqlColumn(ColumnModel model, CommandType type)
        {
            return $"ALTER TABLE `{model.TableName}` {type.ToString()} COLUMN `{model.ColumnName}` {(type != CommandType.Drop ? $"{model.ColumnType} {model.ColumnOptions}" : "")};";
        }
    }
}
