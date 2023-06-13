using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using Dignus.Collections;
using Dignus.DependencyInjection.Attribute;
using Dignus.Log;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DatabaseBatch.Infrastructure
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Transient)]
    public class MySqlManager : ISqlManager
    {
        private readonly ArrayList<MySqlScript> _outputTableScripts = new();

        private readonly Dictionary<string, TableInfoModel> _scriptTables = new();

        private Dictionary<string, TableInfoModel> _tableToDatabase;
        private Dictionary<string, List<IndexModel>> _dbIndexTables;

        private readonly Config _config;
        private readonly DBContext _dbContext;
        public MySqlManager(Config config, DBContext dbContext)
        {
            _config = config;
            _dbContext = dbContext;
        }
        public void Init()
        {
            _tableToDatabase = GetMySqlTableInfo();
            _dbIndexTables = GetMySqlIndexInfo();
        }
        private Dictionary<string, List<IndexModel>> GetMySqlIndexInfo()
        {
            using (var conn = new MySqlConnection(_dbContext.GetConnString()))
            {
                conn.Open();

                var tables = new Dictionary<string, List<IndexModel>>();
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
                    var indexModel = new IndexModel()
                    {
                        TableName = tableName,
                        IndexName = indexName,
                    };

                    if (!tables.ContainsKey(indexModel.TableName))
                    {
                        tables.Add(indexModel.TableName, new List<IndexModel>());
                    }
                    tables[indexModel.TableName].Add(indexModel);
                }
                return tables;
            }
        }

        private Dictionary<string, TableInfoModel> GetMySqlTableInfo()
        {
            using MySqlConnection conn = new(_dbContext.GetConnString());

            conn.Open();

            var tables = new Dictionary<string, TableInfoModel>();

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
                var columnTypes = reader["COLUMN_TYPE"].ToString().ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var columnOption = "";
                if (columnTypes.Length > 1)
                {
                    columnOption = columnTypes.Skip(1).Aggregate((opton, next) => $"{opton} {next}");
                }
                var column = new ParseSqlData()
                {
                    ColumnName = columnName,
                    ColumnDataType = columnTypes[0],
                    ColumnOptions = columnOption
                };

                if (!tables.ContainsKey(tableName))
                {
                    tables.Add(tableName, new TableInfoModel());
                }
                tables[tableName].Columns.Add(column.ColumnName, column);
            }

            return tables;
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
            

            //기본 테이블에 추가 데이터 추가
            LoadAlterTable();

            MakeTableScript();

            LoadSp();
            
            //병합후 스크립트화
            OutputScript();
        }


        private bool DataTypeCompare(ColumnModel left,  ColumnModel right)
        {
            if (MySqlParseHelper.BaseMySqlDataType.TryGetValue(left.ColumnDataType, out string value1) == false)
            {
                value1 = left.ColumnDataType;
            }

            if (MySqlParseHelper.BaseMySqlDataType.TryGetValue(right.ColumnDataType, out string value2) == false)
            {
                value2 = right.ColumnDataType;
            }

            return value1.ToLower() == value2.ToLower();
        }

        private void MakeTableScript()
        {
            //테이블 변경점을 찾아보자
            foreach (var table in _scriptTables.Values)
            {
                if (_tableToDatabase.TryGetValue(table.TableName, out TableInfoModel connectionTable))
                {
                    foreach (var column in table.Columns)
                    {
                        if (connectionTable.Columns.TryGetValue(column.Key.ToLower(), out ParseSqlData parseSqlData))
                        {
                            if(DataTypeCompare(column.Value, parseSqlData) == false)
                            {
                                MySqlParseHelper.AlterMySqlColumn(column.Value);
                                LogHelper.Info($"Table[ {table.TableName} ] ColumnName[ {column.Value.ColumnName} ] [ {parseSqlData.ColumnDataType} ] 에서 [ {column.Value.ColumnDataType} ] 으로 변경됩니다.");
                            }
                        }
                        else
                        {
                            //테이블에 없음
                            if (column.Value.CommandType == CommandType.Add && column.Value.ClassificationType == ClassificationType.Column)
                            {
                                LogHelper.Info($"Table[ {table.TableName} ] ColumnName[ {column.Value.ColumnName} ( {column.Value.ColumnDataType} ) ] (이)가 추가됩니다.");
                                var output = MySqlParseHelper.AlterMySqlColumn(column.Value);
                            }
                            else if (column.Value.CommandType == CommandType.Change && column.Value.ClassificationType == ClassificationType.Column)
                            {
                                LogHelper.Info($"Table[ {table.TableName} ] ColumnName[ {column.Value.ColumnName} ] 에서 ColumnName[ {column.Value.ChangeColumnName} ] [ {column.Value.ColumnDataType} ] 으로 변경됩니다.");
                                MySqlParseHelper.AlterMySqlColumnChange(column.Value);
                            }
                            else
                            {
                                LogHelper.Info($"Table[ {table.TableName} ] ColumnName[ {column.Value.ColumnName} ] [ {column.Value.ColumnDataType} ]");
                                throw new Exception("Unknown Error");
                            }
                        }
                    }
                }
            }

            Console.WriteLine();
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

                if (MySqlParseHelper.CheckConnectDatabase(query, out string database))
                {
                    if (!database.ToLower().Equals(_dbContext.GetDatabaseName()))
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                        continue;
                    }
                }
                if (MySqlParseHelper.ParseAlterCommand(query, out List<ParseSqlData> parseSqlDatas))
                {
                    foreach (var item in parseSqlDatas)
                    {
                        if (item.CommandType == CommandType.Alter)
                        {
                            InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Table[ {item.TableName} ] [ {item.Command} ] (이)가 실행됩니다.");
                        }
                    }
                }

                //if (MySqlParseHelper.CheckConnectDatabase(sql, out string database))
                //{
                //    if (!database.ToUpper().Equals(connectedDatabaseName))
                //    {
                //        continue;
                //    }
                //}
                //if (MySqlParseHelper.ParseAlterCommnad(sql, out List<ParseSqlData> parseSqlDatas))
                //{
                //    foreach (var data in parseSqlDatas)
                //    {
                //        if (data.ClassificationType == ClassificationType.Columns)
                //        {
                //            if (data.CommandType == CommandType.Alter)
                //            {
                //                LogHelper.Info($"Table[ {data.TableName} ] [ {data.Command} ] (이)가 실행됩니다.");
                //                LogHelper.Info("");
                //                _outputOtherBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                //            }
                //            else if (_scriptTables.ContainsKey(data.TableName))
                //            {

                    //                if (_scriptTables[data.TableName].Columns.ContainsKey(data.ColumnName))
                    //                {
                    //                    if (data.CommandType == CommandType.Change)
                    //                    {
                    //                        _scriptTables[data.TableName].Columns.Remove(data.ColumnName);
                    //                        _scriptTables[data.TableName].Columns.Add(data.ChangeColumnName, data);
                    //                    }
                    //                    if (data.CommandType == CommandType.Drop)
                    //                    {
                    //                        _scriptTables[data.TableName].Columns.Remove(data.ColumnName);
                    //                    }
                    //                }
                    //                else
                    //                {
                    //                    _scriptTables[data.TableName].Columns.Add(data.ColumnName, data);
                    //                }
                    //            }
                    //            //Create Table에 정보가 없는 경우 현재 접속한 DB 에서 Table 정보를 가져왔음. 
                    //            else if (_dbTables.ContainsKey(data.TableName) && !_scriptTables.ContainsKey(data.TableName))
                    //            {
                    //                var option = _dbTables[data.TableName].TableOption;
                    //                _scriptTables.Add(data.TableName, new TableInfoModel()
                    //                {
                    //                    Columns = _dbTables[data.TableName].Columns.ToDictionary(r => r.Key, r => r.Value),
                    //                    TableName = data.TableName,
                    //                    TableOption = _dbTables[data.TableName].TableOption
                    //                });
                    //            }
                    //        }
                    //        else
                    //        {
                    //            if (string.IsNullOrEmpty(data.ColumnName))
                    //            {
                    //                LogHelper.Info($"Table[ {data.TableName} ] [ {data.Command} ] 명시적 이름이 없습니다. 이미 변경이 이뤄졌을 수도 있습니다.");
                    //                Console.ReadKey();
                    //                continue;
                    //            }

                    //            var index = _dbIndexTables[data.TableName].Find(r => r.IndexName == data.ColumnName);
                    //            if (index == null && data.CommandType == CommandType.Add)
                    //            {
                    //                LogHelper.Info($"Table[ {data.TableName} ] Name[ {data.ColumnName} ] [ {data.Command} ] (이)가 추가됩니다.");

                    //                _dbIndexTables[data.TableName].Add(new IndexModel()
                    //                {
                    //                    IndexName = data.ColumnName,
                    //                    TableName = data.TableName
                    //                });
                    //                _outputTableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                    //            }
                    //            else if (index != null && data.CommandType == CommandType.Drop)
                    //            {
                    //                LogHelper.Info($"Table[ {data.TableName} ] Name[ {data.Command} ] (이)가 제거됩니다.");

                    //                _dbIndexTables[data.TableName].Add(new IndexModel()
                    //                {
                    //                    IndexName = data.ColumnName,
                    //                    TableName = data.TableName
                    //                });
                    //                _outputTableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                    //            }
                    //            else if (data.CommandType == CommandType.Alter)
                    //            {
                    //                LogHelper.Info($"Table[ {data.TableName} ] [ {data.Command} ] (이)가 실행됩니다.");

                    //                _outputTableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                    //            }
                    //        }
                    //    }
                    //}
            }
            Console.WriteLine();
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
                MySqlScript script = new MySqlScript(query);

                if (MySqlParseHelper.CheckConnectDatabase(query, out string database))
                {
                    if (!database.ToLower().Equals(_dbContext.GetDatabaseName()))
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                        continue;
                    }
                }

                if (MySqlParseHelper.ParseCreateTableCommand(query, out TableInfoModel parseTableData))
                {
                    if(_tableToDatabase.ContainsKey(parseTableData.TableName.ToLower()) == false)
                    {
                        _outputTableScripts.Add(script);
                        InputManager.Instance.WriteLine(ConsoleColor.DarkBlue, $"Table[ {parseTableData.TableName} ] (이)가 생성됩니다.");
                    }
                    _scriptTables.Add(parseTableData.TableName, parseTableData);
                }
            }

            Console.WriteLine();
        }

        private void Deployment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            InputManager.Instance.WriteLine(ConsoleColor.DarkGreen, $"Read File : {path}");

            var sqlCommand = File.ReadAllText(path);

            if(string.IsNullOrEmpty(sqlCommand) == true)
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
                if (MySqlParseHelper.CheckConnectDatabase(query, out string database))
                {
                    if (!database.ToLower().Equals(_dbContext.GetDatabaseName()))
                    {
                        InputManager.Instance.WriteLine(ConsoleColor.Red, $"File {files[i].Name} Database [ {_dbContext.GetDatabaseName()} ] 과 [ {database} ](이)가 다릅니다.");
                        continue;
                    }
                    _outputTableScripts.Add(new(query));
                }
            }
        }
        private void OutputScript()
        {
            Console.WriteLine();
            InputManager.Instance.WriteLine(ConsoleColor.White, ">>>Make Output Scripts");
            if (File.Exists(Consts.OutputScript))
                File.Delete(Consts.OutputScript);

            StringBuilder sb = new StringBuilder();

            foreach(var item in _outputTableScripts)
            {
                sb.AppendLine(item.Query);
            }

            sb.AppendLine();

            File.AppendAllText(Consts.OutputScript, sb.ToString());

            InputManager.Instance.WriteLine(ConsoleColor.White, $"Create Script File : {Consts.OutputScript}");
        }
        private List<FileInfo> GetSqlFiles(DirectoryInfo directory)
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (var dir in directory.GetDirectories())
            {
                files.AddRange(GetSqlFiles(dir));
            }
            files.AddRange(directory.GetFiles().Where(r => r.Extension.ToLower() == ".sql"));
            return files;
        }
    }
}
