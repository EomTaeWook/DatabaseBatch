using DatabaseBatch.Models;
using Dignus.DependencyInjection.Attribute;

namespace DatabaseBatch.Infrastructure
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Singleton)]
    public class MySqlParseProcessor
    {
        private Dictionary<string, string> _mySqlDataType = new(StringComparer.OrdinalIgnoreCase)
        {
            {"int", "int(11)" },
            { "varchar", "varchar(100)"},
            { "long", "mediumtext"},
            { "bigint", "bigint(20)"},
        };


        private readonly Dictionary<string, List<string>> _mySqlReservedKeyword = new(StringComparer.OrdinalIgnoreCase)
        {
            { "primary key", new List<string>(){")","," } },
            { "index", new List<string>(){ ")", ","} },
            { "key", new List<string>(){ ")", ","} },

            { "foreign key", new List<string>(){ "references" } },
            { "constraint", new List<string>(){ "foreign key" } },
            { "references", new List<string>(){ ")", "," } },

            { "column", new List<string>(){ "", } },

            { "unique index", new List<string>(){ ")", "," } },
            { "fulltext index", new List<string>(){ ")", "," } },
            { "spatial index", new List<string>(){ ")", "," } },
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
        public bool ParseAlterCommand(string query, out List<ParseSqlData> parseSqlDatas)
        {
            parseSqlDatas = new List<ParseSqlData>();
            var reader = new MySqlReader(query, new string[] { ",",";" });

            var tableName = string.Empty;
            while (reader.NextLine(out string line))
            {
                var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (Enum.TryParse(splits[0], true, out CommandType command) == false)
                {
                    continue;
                }

                if(command == CommandType.Alter || command == CommandType.Drop)
                {
                    tableName = splits[2].ToLower();
                    splits = splits.Skip(3).ToArray();
                }

                if (Enum.TryParse(splits[0], true, out command) == false)
                {
                    continue;
                }

                var changedData = new ParseSqlData();
                changedData.TableName = tableName;
                changedData.CommandType = command;
                changedData.ColumnName = splits[2];
                if(_mySqlDataType.TryGetValue(splits[3], out string dataType))
                {
                    changedData.ColumnDataType = dataType;
                }
                else
                {
                    changedData.ColumnDataType = splits[3];
                }
                for(int i=4; i< splits.Length; ++i)
                {
                    changedData.ColumnOptions += $" {splits[i]}";
                }
                changedData.ColumnOptions = changedData.ColumnOptions?.Trim();

                if (Enum.TryParse(splits[1], true, out ClassificationType classificationType) == true)
                {
                    changedData.ClassificationType = classificationType;
                }
                parseSqlDatas.Add(changedData);
            }
            return true;
        }
        
        public bool ParseCreateTableCommand(string sql, out TableInfoModel tableInfoData)
        {
            tableInfoData = new TableInfoModel();
            var reader = new MySqlReader(sql);
            while (reader.NextLine(out string line))
            {
                if(line.ToLower().StartsWith("create table") == false)
                {
                    continue;
                }

                var openIndex = line.IndexOf("(");
                var closeIndex = line.LastIndexOf(')');
                if (openIndex == -1)
                {
                    throw new Exception($"Create Table Parse Error: {sql}");
                }

                var tableNameStartIndex = line.ToLower().IndexOf("create table") + "create table".Length;
                tableInfoData.TableName = line[tableNameStartIndex..openIndex].Replace("`", "").Trim();

                var body = line.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();
                MySqlReader bodyReader = new(body, new string[] { "," });
                while (bodyReader.NextLine(out string bodyLine))
                {
                    bool isReservedKeyword = false;
                    var queue = new Queue<string>();

                    foreach (var reservedKeyword in _mySqlReservedKeyword.Keys)
                    {
                        isReservedKeyword = bodyLine.ToLower().StartsWith(reservedKeyword);
                        if (isReservedKeyword == false)
                            continue;
                        queue.Enqueue(reservedKeyword);
                        break;
                    }
                    if (isReservedKeyword == false)
                    {
                        var column = new ParseSqlData();
                        var splits = bodyLine.Split(" ", StringSplitOptions.RemoveEmptyEntries);
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
                    else
                    {
                        while (queue.Count > 0)
                        {
                            var reservedKeyword = queue.Dequeue();
                            if (_mySqlReservedKeyword.TryGetValue(reservedKeyword, out List<string> close))
                            {
                                do
                                {
                                    var isFind = false;
                                    foreach (var item in close)
                                    {
                                        if (bodyLine.ToLower().Contains(item) == true)
                                        {
                                            isFind = true;
                                            break;
                                        }
                                    }
                                    if (isFind)
                                    {
                                        break;
                                    }
                                }
                                while (bodyReader.NextLine(out bodyLine));

                                foreach (var item in close)
                                {
                                    queue.Enqueue(item);
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }
        public bool CheckConnectDatabase(string sql, out string database)
        {
            database = null;
            var reader = new MySqlReader(sql);
            while (reader.NextLine(out string line))
            {
                if (line.ToLower().StartsWith("use"))
                {
                    database = line.Split(' ').Skip(1).Aggregate((l, r) => $"{l} {r}").Replace("`", "");
                    return true;
                }
            }
            return false;
        }

        public string AlterMySqlColumnChange(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` CHANGE COLUMN `{model.ColumnName}` `{model.ChangeColumnName}` {model.ColumnDataType} {model.ColumnOptions};";
        }
        public string AlterMySqlColumn(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` {model.CommandType.ToString().ToLower()} COLUMN `{model.ColumnName}` {model.ColumnDataType} {(model.CommandType != CommandType.Drop ? $"{model.ColumnOptions}" : "")};";
        }
        public string CreateSqlCommand(ParseSqlData model)
        {
            return $"ALTER TABLE `{model.TableName}` {model.CommandType.ToString().ToLower()} {model.Command};";
        }
    }
}
