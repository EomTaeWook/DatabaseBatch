using DatabaseBatch.Extensions;
using DatabaseBatch.Models;
using Dignus.DependencyInjection.Attributes;

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
        private static readonly string[] replacements = ["`", "(", ")"];
        private static readonly string[] separator = [" ", "(", ")"];

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

            return value1.Equals(value2, StringComparison.CurrentCultureIgnoreCase);
        }
        public List<SqlParseTableData> ParseAlterCommand(string query)
        {
            var sqlParseTableData = new List<SqlParseTableData>();
            var reader = new MySqlReader(query, [";"]);
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
                SqlParseTableData table = new();
                if (command == CommandType.Alter || command == CommandType.Drop)
                {
                    table.TableName = splits[2].ToLower().Replace("`", "");
                    table.CommandType = command;
                }

                MySqlReader bodyReader = new(string.Join(" ", splits.Skip(3)), [","]);
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
                    if (splits[1].Equals("index", StringComparison.CurrentCultureIgnoreCase) ||
                        splits[1].Equals("key", StringComparison.CurrentCultureIgnoreCase))
                    {
                        columnName = splits[2];
                        sqlInfoData.ClassificationType = ClassificationType.Index;
                    }
                    else if (splits[1].Equals("constraint", StringComparison.CurrentCultureIgnoreCase))
                    {
                        columnName = splits[2];
                        if (splits[3].Equals("foreign", StringComparison.CurrentCultureIgnoreCase))
                        {
                            sqlInfoData.ClassificationType = ClassificationType.ForeignKey;
                        }
                        else if (splits[3].Equals("primary", StringComparison.CurrentCultureIgnoreCase))
                        {
                            sqlInfoData.ClassificationType = ClassificationType.PrimaryKey;
                        }
                        else
                        {
                            throw new InvalidOperationException($"what the case? T-T");
                        }
                    }
                    else if (splits[1].Equals("foreign", StringComparison.CurrentCultureIgnoreCase))
                    {
                        columnName = splits[3];
                        sqlInfoData.ClassificationType = ClassificationType.ForeignKey;
                    }
                    else if (splits[1].Equals("primary", StringComparison.CurrentCultureIgnoreCase))
                    {
                        columnName = splits[3];
                        sqlInfoData.ClassificationType = ClassificationType.PrimaryKey;
                    }
                    else if (splits[1].Equals("column", StringComparison.CurrentCultureIgnoreCase))
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
                    sqlInfoData.ColumnName = columnName.Replace(replacements, "");

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

                var openIndex = line.IndexOf('(');
                var closeIndex = line.LastIndexOf(')');
                if (openIndex == -1)
                {
                    throw new Exception($"Create Table Parse Error: {query}");
                }

                var body = line.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();
                MySqlReader bodyReader = new(body, [","]);
                while (bodyReader.NextLine(out string bodyLine))
                {
                    splits = bodyLine.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    if (splits[0].StartsWith("primary", StringComparison.CurrentCultureIgnoreCase))
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
                    else if (splits[0].StartsWith("constraint", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (splits[2].Equals("foreign", StringComparison.CurrentCultureIgnoreCase))
                        {
                            tableInfoData.ForeignKeyNames.Add(splits[1].Replace("`", ""));
                        }
                    }
                    else if (splits[0].StartsWith("foreign", StringComparison.CurrentCultureIgnoreCase))
                    {
                        tableInfoData.ForeignKeyNames.Add(GetDefaultFkName(tableInfoData.TableName, defaultFkIndex++));
                    }
                    else if (splits[0].StartsWith("index", StringComparison.CurrentCultureIgnoreCase) ||
                        splits[0].StartsWith("key", StringComparison.CurrentCultureIgnoreCase))
                    {
                        splits = bodyLine.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        tableInfoData.IndexNames.Add(splits[1].Replace("`", ""));
                    }
                    else
                    {
                        var column = new SqlParseColumnData
                        {
                            ColumnName = splits[0]
                        };
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
                if (line.StartsWith("use", StringComparison.CurrentCultureIgnoreCase))
                {
                    database = line.Split(' ').Skip(1).Aggregate((l, r) => $"{l} {r}").Replace("`", "");
                    return database.ToLower();

                }
            }
            return null;
        }
    }
}
