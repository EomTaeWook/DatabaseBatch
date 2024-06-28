using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using Dignus.Collections;
using Dignus.DependencyInjection.Attributes;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text;

namespace DatabaseBatch.Infrastructure
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Transient)]
    public class MySqlManager : ISqlManager
    {
        [Inject]
        public MySqlParsor MySqlParsor { get; private set; }
        private readonly ArrayQueue<MySqlScript> _outputTableScripts = [];

        private readonly Dictionary<string, TableInfoModel> _scriptTables = [];

        private readonly Dictionary<string, TableInfoModel> _databaseToTables;

        private readonly Config _config;
        private readonly DBContext _dbContext;
        private static readonly char[] separator = [' '];

        public MySqlManager(Config config, DBContext dbContext)
        {
            _config = config;
            _dbContext = dbContext;
            _databaseToTables = [];
        }
        public void Init()
        {
            InitMySqlTableInfo();
        }

        private void InitMySqlTableInfo()
        {
            using (MySqlConnection conn = new(_dbContext.GetConnString()))
            {
                conn.Open();

                var sqlCommand = $"SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE  FROM Information_schema.columns WHERE table_schema = '{conn.Database}';";

                var cmd = conn.CreateCommand();
                cmd.Connection = conn;
                cmd.CommandText = sqlCommand;
                cmd.CommandType = System.Data.CommandType.Text;

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var tableName = reader["TABLE_NAME"].ToString().ToLower();
                    var columnName = reader["COLUMN_NAME"].ToString().ToLower();
                    var columnTypes = reader["COLUMN_TYPE"].ToString().ToLower().Split(separator, StringSplitOptions.RemoveEmptyEntries);
                    var columnOption = "";
                    if (columnTypes.Length > 1)
                    {
                        columnOption = columnTypes.Skip(1).Aggregate((opton, next) => $"{opton} {next}");
                    }
                    var column = new SqlParseColumnData()
                    {
                        ColumnName = columnName,
                        ColumnDataType = columnTypes[0],
                        ColumnOptions = columnOption,
                        ClassificationType = ClassificationType.Column,
                    };

                    if (!_databaseToTables.TryGetValue(tableName, out TableInfoModel value))
                    {
                        value = new TableInfoModel()
                        {
                            TableName = tableName,
                        };
                        _databaseToTables.Add(tableName, value);
                    }

                    value.Columns.Add(column.ColumnName, column);
                }
            };

            using (MySqlConnection conn = new(_dbContext.GetConnString()))
            {
                conn.Open();
                var sqlCommand = $"SELECT DISTINCT TABLE_NAME, INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = '{conn.Database}' AND INDEX_NAME <> 'PRIMARY';";

                var cmd = conn.CreateCommand();
                cmd.Connection = conn;
                cmd.CommandText = sqlCommand;
                cmd.CommandType = System.Data.CommandType.Text;

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var tableName = reader["TABLE_NAME"].ToString().ToLower();
                    var indexName = reader["INDEX_NAME"].ToString().ToLower();

                    if (!_databaseToTables.ContainsKey(tableName))
                    {
                        continue;
                    }
                    _databaseToTables[tableName].IndexNames.Add(indexName);
                }
            }

            using (MySqlConnection conn = new(_dbContext.GetConnString()))
            {
                conn.Open();

                var sqlCommand = $"SELECT DISTINCT TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME \r\nFROM information_schema.TABLE_CONSTRAINTS where TABLE_SCHEMA = '{conn.Database}' AND CONSTRAINT_NAME <> 'PRIMARY';";
                var cmd = conn.CreateCommand();
                cmd.Connection = conn;
                cmd.CommandText = sqlCommand;
                cmd.CommandType = System.Data.CommandType.Text;

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var tableName = reader["TABLE_NAME"].ToString().ToLower();
                    var fkName = reader["CONSTRAINT_NAME"].ToString().ToLower();

                    if (!_databaseToTables.ContainsKey(tableName))
                    {
                        continue;
                    }
                    _databaseToTables[tableName].ForeignKeyNames.Add(fkName);
                }
            }
        }

        public void Publish()
        {
            Deployment(_config.Publish.PreDeployment);
            Deployment(Consts.OutputScript);
            Deployment(_config.Publish.PostDeployment);
        }
        public void MakeScript()
        {
            //기본 테이블 세팅
            LoadTable();
            //기본 테이블에 변경 데이터 적용
            LoadAlterTable();
            LoadSp();

            CompareWithDatabaseTable();
            OutputScript();
        }


        private void CompareWithDatabaseTable()
        {
            foreach (var scriptTable in _scriptTables.Values)
            {
                if (_databaseToTables.TryGetValue(scriptTable.TableName, out TableInfoModel databaseTable) == false)
                {
                    InputManager.Instance.WriteLine(ConsoleColor.Red, $"Table[ {scriptTable.TableName} ] 추가 됩니다.");
                    continue;
                }

                foreach (var scriptColumn in scriptTable.Columns.Values)
                {
                    if (databaseTable.Columns.TryGetValue(scriptColumn.ColumnName, out SqlParseColumnData databaseColumn) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {scriptTable.TableName} ] Column Name[ {scriptColumn.ColumnName} ( {scriptColumn.ColumnDataType} ) ] (이)가 추가됩니다.");
                        continue;
                    }

                    if (MySqlParsor.DataTypeCompare(scriptColumn, databaseColumn) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {scriptTable.TableName} ] Column Name[ {databaseColumn.ColumnName} ] [ {databaseColumn.ColumnDataType} ] 에서 [ {scriptColumn.ColumnDataType} ] 으로 변경됩니다.");
                    }
                }

                foreach (var databaseColumn in databaseTable.Columns.Values)
                {
                    if (scriptTable.Columns.TryGetValue(databaseColumn.ColumnName, out SqlParseColumnData scriptColumn) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {databaseTable.TableName} ] Column Name[ {databaseColumn.ColumnName} ( {databaseColumn.ColumnDataType} ) ] (이)가 제거됩니다.");
                        continue;
                    }
                }

                foreach (var index in scriptTable.IndexNames)
                {
                    if (databaseTable.IndexNames.TryGetValue(index, out _) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {scriptTable.TableName} ] Index Name[ {index} ] (이)가 추가됩니다.");
                        continue;
                    }
                }

                foreach (var index in databaseTable.IndexNames)
                {
                    if (scriptTable.IndexNames.TryGetValue(index, out _) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {databaseTable.TableName} ] Index Name[ {index} ] (이)가 제거됩니다.");
                        continue;
                    }
                }

                foreach (var index in scriptTable.ForeignKeyNames)
                {
                    if (databaseTable.ForeignKeyNames.TryGetValue(index, out _) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {scriptTable.TableName} ] Foreign Key Name[ {index} ] (이)가 추가됩니다.");
                        continue;
                    }
                }

                foreach (var index in databaseTable.ForeignKeyNames)
                {
                    if (scriptTable.ForeignKeyNames.TryGetValue(index, out _) == false)
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {databaseTable.TableName} ] Foreign Key Name[ {index} ] (이)가 제거됩니다.");
                        continue;
                    }
                }
            }

            foreach (var databaseTable in _databaseToTables.Values)
            {
                if (_scriptTables.TryGetValue(databaseTable.TableName, out _) == false)
                {
                    InputManager.Instance.WriteLine(ConsoleColor.Red, $"Table[ {databaseTable.TableName} ] 존재하지 않습니다.");
                    continue;
                }
            }
            Console.WriteLine();
        }
        private void ApplyTableChanges(SqlParseTableData alterScriptTable)
        {
            if (_scriptTables.TryGetValue(alterScriptTable.TableName, out TableInfoModel scriptTable) == false)
            {
                return;
            }

            foreach (var changed in alterScriptTable.SqlParseColumnDatas)
            {
                if (changed.ClassificationType == ClassificationType.Column)
                {
                    if (changed.CommandType == CommandType.Add)
                    {
                        scriptTable.Columns.Add(changed.ColumnName, changed);
                    }
                    else if (changed.CommandType == CommandType.Drop)
                    {
                        scriptTable.Columns.Remove(changed.ColumnName);
                    }
                    else if (changed.CommandType == CommandType.Modify)
                    {
                        scriptTable.Columns[changed.ColumnName] = changed;
                    }
                }
                else if (changed.ClassificationType == ClassificationType.Index)
                {
                    if (changed.CommandType == CommandType.Drop)
                    {
                        scriptTable.IndexNames.Remove(changed.ColumnName);
                    }
                    else if (changed.CommandType == CommandType.Add)
                    {
                        scriptTable.IndexNames.Add(changed.ColumnName);
                    }
                }
                else if (changed.ClassificationType == ClassificationType.ForeignKey)
                {
                    if (changed.CommandType == CommandType.Drop)
                    {
                        scriptTable.ForeignKeyNames.Remove(changed.ColumnName);
                    }
                    else if (changed.CommandType == CommandType.Add)
                    {
                        scriptTable.ForeignKeyNames.Add(changed.ColumnName);
                    }
                }
            }
        }

        private void LoadAlterTable()
        {
            InputManager.Instance.WriteLine(ConsoleColor.White, $">>>>Load AlterTable Files : {_config.AlterTablePath}");

            if (string.IsNullOrEmpty(_config.AlterTablePath))
                return;

            var directoryInfo = new DirectoryInfo(_config.AlterTablePath);
            var files = GetSqlFiles(directoryInfo);
            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write(ConsoleColor.DarkGreen, $"Read File : ");
                InputManager.Instance.WriteLine(ConsoleColor.White, $"{files[i].Name}");

                var query = File.ReadAllText(files[i].FullName);

                if (string.IsNullOrEmpty(query))
                    throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");

                var database = MySqlParsor.GetConnectDatabase(query);

                if (!_dbContext.GetDatabaseName().Equals(MySqlParsor.GetConnectDatabase(query)))
                {
                    InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                    continue;
                }
                var parseSqlDatas = MySqlParsor.ParseAlterCommand(query);

                foreach (var alterScriptTable in parseSqlDatas)
                {
                    ApplyTableChanges(alterScriptTable);
                }
                _outputTableScripts.Add(new(query));
                Console.WriteLine();
            }
        }
        private void LoadTable()
        {
            InputManager.Instance.WriteLine(ConsoleColor.White, $">>>>Load Table Files : {_config.TablePath}");

            if (string.IsNullOrEmpty(_config.TablePath))
                return;

            var directoryInfo = new DirectoryInfo(_config.TablePath);
            var files = GetSqlFiles(directoryInfo);

            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write(ConsoleColor.DarkGreen, $"Read File : ");
                InputManager.Instance.WriteLine(ConsoleColor.White, $"{files[i].Name}");
                var query = File.ReadAllText(files[i].FullName);
                if (string.IsNullOrEmpty(query))
                {
                    throw new Exception($"{files[i].FullName} : 쿼리 문이 없습니다.");
                }
                MySqlScript script = new(query);
                var database = MySqlParsor.GetConnectDatabase(query);
                if (!_dbContext.GetDatabaseName().Equals(database))
                {
                    InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                    continue;
                }

                var parseTableData = MySqlParsor.ParseCreateTableCommand(query);

                if (_databaseToTables.ContainsKey(parseTableData.TableName.ToLower()) == false)
                {
                    _outputTableScripts.Add(script);
                    InputManager.Instance.WriteLine(ConsoleColor.DarkBlue, $"Table[ {parseTableData.TableName} ] (이)가 생성됩니다.");
                }
                _scriptTables.Add(parseTableData.TableName, parseTableData);
            }

            Console.WriteLine();
        }

        private void Deployment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Read File : {path}");

            var sqlCommand = File.ReadAllText(path);

            if (string.IsNullOrEmpty(sqlCommand) == true)
            {
                return;
            }

            using var connection = new MySqlConnection(_dbContext.GetConnString());
            connection.Open();

            var command = new MySqlScript(connection, sqlCommand);
            command.Execute();

            connection.Close();
        }
        private void LoadSp()
        {
            InputManager.Instance.WriteLine(ConsoleColor.White, $">>>Load Stored Procedure Files : {_config.StoredProcedurePath}");

            if (string.IsNullOrEmpty(_config.StoredProcedurePath))
                return;

            var directoryInfo = new DirectoryInfo(_config.StoredProcedurePath);
            var files = GetSqlFiles(directoryInfo);

            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write(ConsoleColor.DarkGreen, $"Read File : ");
                InputManager.Instance.WriteLine(ConsoleColor.White, $"{files[i].Name}");
                var query = File.ReadAllText(files[i].FullName);
                if (string.IsNullOrEmpty(query))
                {
                    throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");
                }

                var database = MySqlParsor.GetConnectDatabase(query);

                if (!_dbContext.GetDatabaseName().Equals(MySqlParsor.GetConnectDatabase(query)))
                {
                    InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                    continue;
                }

                _outputTableScripts.Add(new(query));
            }
        }
        private void OutputScript()
        {
            Console.WriteLine();
            InputManager.Instance.WriteLine(ConsoleColor.White, ">>>Make Output Scripts");
            if (File.Exists(Consts.OutputScript))
                File.Delete(Consts.OutputScript);

            StringBuilder sb = new();

            foreach (var item in _outputTableScripts)
            {
                sb.AppendLine(item.Query);
            }

            sb.AppendLine();

            File.AppendAllText(Consts.OutputScript, sb.ToString());

            InputManager.Instance.WriteLine(ConsoleColor.White, $"Create Script File : {Consts.OutputScript}");
        }
        private List<FileInfo> GetSqlFiles(DirectoryInfo directory)
        {
            List<FileInfo> files = [];
            foreach (var dir in directory.GetDirectories())
            {
                files.AddRange(GetSqlFiles(dir));
            }
            files.AddRange(directory.GetFiles().Where(r => r.Extension.Equals(".sql", StringComparison.CurrentCultureIgnoreCase)));
            return files;
        }
    }
}
