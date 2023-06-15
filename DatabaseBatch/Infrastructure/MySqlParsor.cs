using DatabaseBatch.Extensions;
using DatabaseBatch.Models;
using Dignus.DependencyInjection.Attribute;

namespace DatabaseBatch.Infrastructure
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Singleton)]
    public class MySqlParsor
    {
        private readonly Dictionary<string, string> _mySqlDataType = new(StringComparer.OrdinalIgnoreCase)
        {
            {"int", "int(11)" },
            { "varchar", "varchar(100)"},
            { "long", "mediumtext"},
            { "bigint", "bigint(20)"},
        };

        public bool DataTypeCompare(ColumnModel left, ColumnModel right)
        {
            if (_mySqlDataType.TryGetValue(left.ColumnDataType, out string value1) == false)
            {
                value1 = left.ColumnDataType;
            }

            if (_mySqlDataType.TryGetValue(right.ColumnDataType, out string value2) == false)
            {
                value2 = right.ColumnDataType;
            }

            return value1.ToLower() == value2.ToLower();
        }
        public List<SqlParseTableData> ParseAlterCommand(string query)
        {
            var sqlParseTableData = new List<SqlParseTableData>();
            var reader = new MySqlReader(query, new string[] {";" });
            while (reader.NextLine(out string line))
            {
                if(string.IsNullOrEmpty(line) == true)
                {
                    continue;
                }
                var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (Enum.TryParse(splits[0], true, out CommandType command) == false)
                {
                    continue;
                }
                SqlParseTableData table = new SqlParseTableData();
                if (command == CommandType.Alter || command == CommandType.Drop)
                {
                    table.TableName = splits[2].ToLower().Replace("`", "");
                    table.CommandType = command;
                }

                MySqlReader bodyReader = new(string.Join(" ", splits.Skip(3)), new string[] { "," });
                while (bodyReader.NextLine(out string bodyLine))
                {
                    splits = bodyLine.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    var offset = 0;
                    var sqlInfoData = new SqlParseColumnData();

                    if (Enum.TryParse(splits[0], true, out command) == false)
                    {
                        continue;
                    }

                    sqlInfoData.CommandType = command;

                    string columnName;
                    if (splits[1].ToLower() == "index" ||
                        splits[1].ToLower() == "key")
                    {
                        columnName = splits[2];
                        sqlInfoData.ClassificationType = ClassificationType.Index;
                    }
                    else if (splits[1].ToLower() == "constraint")
                    {
                        columnName = splits[2];
                        if(splits[3].ToLower() == "foreign")
                        {
                            sqlInfoData.ClassificationType = ClassificationType.ForeignKey;
                        }
                        else if(splits[3].ToLower() == "primary")
                        {
                            sqlInfoData.ClassificationType = ClassificationType.PrimaryKey;
                        }
                        else
                        {
                            throw new InvalidOperationException($"what the case? T-T");
                        }
                    }
                    else if (splits[1].ToLower() == "foreign")
                    {
                        columnName = splits[3];
                        sqlInfoData.ClassificationType = ClassificationType.ForeignKey;
                    }
                    else if (splits[1].ToLower() == "primary")
                    {
                        columnName = splits[3];
                        sqlInfoData.ClassificationType = ClassificationType.PrimaryKey;
                    }
                    else if (splits[1].ToLower() == "column")
                    {
                        columnName = splits[2];
                        sqlInfoData.ClassificationType = ClassificationType.Column;
                    }
                    else
                    {
                        offset = -1;
                        columnName = splits[1];
                        sqlInfoData.ClassificationType = ClassificationType.Column;

                    }

                    sqlInfoData.CommandType = command;
                    sqlInfoData.ColumnName = columnName.Replace(new string[] { "`", "(", ")" }, "");

                    if (sqlInfoData.ClassificationType == ClassificationType.Column)
                    {
                        if (_mySqlDataType.TryGetValue(splits[3 + offset], out string dataType))
                        {
                            sqlInfoData.ColumnDataType = dataType;
                        }
                        else
                        {
                            sqlInfoData.ColumnDataType = splits[3 + offset];
                        }
                    }

                    for (int i = 4 + offset; i < splits.Length; ++i)
                    {
                        sqlInfoData.ColumnOptions += $" {splits[i]}";
                    }
                    sqlInfoData.ColumnOptions = sqlInfoData.ColumnOptions?.Trim();

                    table.SqlParseColumnDatas.Add(sqlInfoData);
                }

                sqlParseTableData.Add(table);
            }
            return sqlParseTableData;
        }
        private string GetDefaultFkName(string tableName, int index)
        {
            return $"{tableName}_ibfk_{index}";
        }
        public TableInfoModel ParseCreateTableCommand(string query)
        {
            var tableInfoData = new TableInfoModel();
            var reader = new MySqlReader(query);
            var defaultFkIndex = 1;
            while (reader.NextLine(out string line))
            {
                if (string.IsNullOrEmpty(line) == true)
                {
                    continue;
                }
                var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (Enum.TryParse(splits[0], true, out CommandType command) == false)
                {
                    continue;
                }
                if (command != CommandType.Create)
                {
                    continue;
                }

                tableInfoData.TableName = splits[2].ToLower().Replace("`", "");

                var openIndex = line.IndexOf("(");
                var closeIndex = line.LastIndexOf(')');
                if (openIndex == -1)
                {
                    throw new Exception($"Create Table Parse Error: {query}");
                }

                var body = line.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();
                MySqlReader bodyReader = new(body, new string[] { "," });
                while (bodyReader.NextLine(out string bodyLine))
                {
                    splits = bodyLine.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    if (splits[0].ToLower().StartsWith("primary"))
                    {

                        do
                        {
                            if (bodyLine.Contains(')'))
                            {
                                break;
                            }
                        }
                        while (bodyReader.NextLine(out bodyLine));
                    }
                    else if (splits[0].ToLower().StartsWith("constraint"))
                    {
                        if (splits[2].ToLower() == "foreign")
                        {
                            tableInfoData.ForeignKeyNames.Add(splits[1].Replace("`", ""));
                        }
                    }
                    else if (splits[0].ToLower().StartsWith("foreign"))
                    {
                        tableInfoData.ForeignKeyNames.Add(GetDefaultFkName(tableInfoData.TableName, defaultFkIndex++));
                    }
                    else if(splits[0].ToLower().StartsWith("index") ||
                        splits[0].ToLower().StartsWith("key") )
                    {
                        splits = bodyLine.Split(new string[] { " ", "(", ")" }, StringSplitOptions.RemoveEmptyEntries);
                        tableInfoData.IndexNames.Add(splits[1].Replace("`", ""));
                    }
                    else
                    {
                        var column = new SqlParseColumnData();
                        column.ColumnName = splits[0];
                        var endIndex = splits[1].LastIndexOf(')');
                        if (endIndex == -1)
                        {
                            column.ColumnDataType = splits[1];
                        }
                        else
                        {
                            column.ColumnDataType = splits[1][..(endIndex + 1)];

                            var options = splits[1][(endIndex + 1)..];
                            if (string.IsNullOrEmpty(options) == false)
                            {
                                column.ColumnOptions = options;
                            }
                        }
                        for (var ii = 2; ii < splits.Length; ++ii)
                        {
                            column.ColumnOptions += $" {splits[ii]}";
                        }
                        column.ColumnOptions = column.ColumnOptions?.Trim();
                        column.ClassificationType = ClassificationType.Column;
                        column.CommandType = CommandType.Add;
                        tableInfoData.Columns.Add(column.ColumnName, column);
                    }
                }
            }

            return tableInfoData;
        }
        public string GetConnectDatabase(string sql)
        {
            string database = null;
            var reader = new MySqlReader(sql);
            while (reader.NextLine(out string line))
            {
                if (line.ToLower().StartsWith("use"))
                {
                    database = line.Split(' ').Skip(1).Aggregate((l, r) => $"{l} {r}").Replace("`", "");
                    return database.ToLower();
                    
                }
            }
            return null;
        }
    }
}
