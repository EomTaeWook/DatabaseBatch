﻿using DatabaseBatch.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DatabaseBatch.Extensions;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlManager : BaseSqlManager
    {
        private StringBuilder _tableBuffer = new StringBuilder();
        private StringBuilder _otherBuffer = new StringBuilder();

        private Dictionary<string, TableInfoModel> _bufferTable = new Dictionary<string, TableInfoModel>();

        private Dictionary<string, TableInfoModel> _dbTable;
        private Dictionary<string, List<IndexModel>> _dbIndexTable;
        public override void Init(Config config)
        {
            _config = config;

            _dbTable = GetMySqlTableInfo(new MySqlConnection(config.SqlConnect));
            _dbIndexTable = GetMySqlIndexInfo(new MySqlConnection(config.SqlConnect));
        }
        public override void Publish()
        {
            var deployment = _config.Publish;
            for (var e = PublishDeploymentType.PreDeployment; e < PublishDeploymentType.Max; ++e)
            {
                var prop = deployment.GetType().GetProperties().Where(r => r.Name == e.ToString()).FirstOrDefault();
                var path = deployment.GetType().GetProperty(prop.Name).GetValue(deployment).ToString();
                Deployment(path, e);
            }
        }
        public override void MakeScript()
        {
            //기본 테이블 세팅
            LoadTable();
            InputManager.Instance.WriteInfo("");

            //기본 테이블에 추가 데이터 추가
            LoadAlterTable();
            MakeTableScript();
            InputManager.Instance.WriteInfo("");

            LoadSp();
            InputManager.Instance.WriteInfo("");
            //병합후 스크립트화
            OutputScript();
            InputManager.Instance.WriteInfo("");
        }
        private void MakeTableScript()
        {
            //테이블 변경점을 찾아보자
            foreach(var table in _bufferTable)
            {
                if(_dbTable.ContainsKey(table.Key))
                {
                    var connectDbTable = _dbTable[table.Key];
                    foreach (var column in table.Value.Columns)
                    {
                        if(connectDbTable.Columns.ContainsKey(column.Key.ToLower()))
                        {
                            //변경
                            var connectDbColumn = connectDbTable.Columns[column.Key.ToLower()];
                            if(!connectDbColumn.TypeCompare(column.Value))
                            {
                                MySqlParseHelper.AlterMySqlColumn(column.Value);
                                InputManager.Instance.WriteTrace($"Table[{column.Value.TableName}] ColumnName[{column.Value.ColumnName}] [{connectDbColumn.ColumnType}] 에서 [{column.Value.ColumnType}] 으로 변경됩니다.");
                            }
                        }
                        else
                        {
                            if(column.Value.CommandType == CommandType.Add)
                            {
                                InputManager.Instance.WriteTrace($"Table[ {column.Value.TableName} ] ColumnName[ {column.Value.ColumnName} ] (이)가 추가됩니다.");
                                var output = MySqlParseHelper.AlterMySqlColumn(column.Value);
                            }
                            else if(column.Value.CommandType == CommandType.Change)
                            {
                                InputManager.Instance.WriteTrace($"Table[ {column.Value.TableName} ] ColumnName[ {column.Value.ColumnName} ] 에서 ColumnName[ {column.Value.ChangeColumnName} ] [ {column.Value.ColumnType} ] 으로 변경됩니다.");
                                MySqlParseHelper.AlterMySqlColumnChange(column.Value);
                            }
                            else
                            {
                                throw new Exception("Unknown Error");
                            }
                        }
                    }
                }

                InputManager.Instance.WriteTrace("");
            }
        }

        private void LoadAlterTable()
        {
            InputManager.Instance.WriteInfo($">>>>Load AlterTable Files : {_config.AlterTablePath}");

            _tableBuffer.AppendLine($"DELIMITER $$");
            _tableBuffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_alter_table`;");
            _tableBuffer.AppendLine($"CREATE PROCEDURE `make_alter_table`() BEGIN");

            var directoryInfo = new DirectoryInfo(_config.AlterTablePath);
            var files = GetSqlFiles(directoryInfo);
            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write($"Read File : {files[i].Name}");
                using (var sr = new StreamReader(files[i].OpenRead()))
                {
                    var sql = sr.ReadToEnd();
                    sr.Close();
                    if (string.IsNullOrEmpty(sql))
                        throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");

                    if(MySqlParseHelper.ParseMysqlAlterCommnad(sql, out List<ParseSqlData> parseSqlDatas))
                    {
                        foreach(var data in parseSqlDatas)
                        {
                            if(data.ClassificationType == ClassificationType.Columns)
                            {
                                if(data.CommandType == CommandType.Alter)
                                {
                                    InputManager.Instance.WriteTrace($"Table[ {data.TableName} ] [ {data.Command} ] (이)가 실행됩니다.");
                                    _otherBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                                }
                                else if (_bufferTable[data.TableName].Columns.ContainsKey(data.ColumnName))
                                {
                                    if (data.CommandType == CommandType.Change)
                                    {
                                        _bufferTable[data.TableName].Columns.Remove(data.ColumnName);
                                        _bufferTable[data.TableName].Columns.Add(data.ChangeColumnName, data);
                                    }
                                    if(data.CommandType == CommandType.Drop)
                                    {
                                        _bufferTable[data.TableName].Columns.Remove(data.ColumnName);
                                    }
                                }
                                else
                                {
                                    _bufferTable[data.TableName].Columns.Add(data.ColumnName, data);
                                }
                            }
                            else
                            {
                                var index = _dbIndexTable[data.TableName].Find(r=>r.IndexName == data.ColumnName);
                                if(index == null && data.CommandType == CommandType.Add)
                                {
                                    InputManager.Instance.WriteTrace($"Table[ {data.TableName} ] Name[ {data.ColumnName} ] [ {data.Command} ] (이)가 추가됩니다.");
                                    _dbIndexTable[data.TableName].Add(new IndexModel()
                                    {
                                        IndexName = data.ColumnName,
                                        TableName = data.TableName
                                    });
                                    _tableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                                }
                                else if (index != null && data.CommandType == CommandType.Drop)
                                {
                                    InputManager.Instance.WriteTrace($"Table[ {data.TableName} ] Name[ {data.Command} ] (이)가 제거됩니다.");
                                    _dbIndexTable[data.TableName].Add(new IndexModel()
                                    {
                                        IndexName = data.ColumnName,
                                        TableName = data.TableName
                                    });
                                    _tableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                                }
                                else if(data.CommandType == CommandType.Alter)
                                {
                                    InputManager.Instance.WriteTrace($"Table[ {data.TableName} ] [ {data.Command} ] (이)가 실행됩니다.");
                                    _tableBuffer.AppendLine(MySqlParseHelper.CreateSqlCommand(data));
                                }
                            }
                        }
                    }
                }
            }

            _tableBuffer.AppendLine($"END $$;");

            _tableBuffer.AppendLine($"DELIMITER ;");

            _tableBuffer.AppendLine($"CALL `make_alter_table`();");
            _tableBuffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_alter_table`;");

            _tableBuffer.AppendLine();

        }
        private void LoadTable()
        {
            //var currentDBTables = GetMySqlTableInfo(new MySqlConnection(_config.SqlConnect));

            _tableBuffer.AppendLine($"DELIMITER $$");
            _tableBuffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_create_table`;");
            _tableBuffer.AppendLine($"CREATE PROCEDURE `make_create_table`() BEGIN");
            InputManager.Instance.WriteInfo($">>>>Load Table Files : {_config.TablePath}");

            var directoryInfo = new DirectoryInfo(_config.TablePath);
            var files = GetSqlFiles(directoryInfo);

            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write($"Read File : {files[i].Name}");
                using (var sr = new StreamReader(files[i].OpenRead()))
                {
                    var sql = sr.ReadToEnd();
                    sr.Close();

                    if (string.IsNullOrEmpty(sql))
                        throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");

                    if (MySqlParseHelper.ParseMysqlCreateTableCommnad(sql, out TableInfoModel parseTableData))
                    {
                        if (!_dbTable.ContainsKey(parseTableData.TableName.ToLower()))
                        {
                            _tableBuffer.AppendLine(sql);
                            InputManager.Instance.WriteTrace($"Table[ {parseTableData.TableName} ] (이)가 생성됩니다.");
                        }
                        _bufferTable.Add(parseTableData.TableName, parseTableData);

                        //기존 코드 => Craete 기준으로 변경 감지
                        //    var dbColumns = currentDBTables[parseTableData.TableName.ToLower()].Columns;

                        //    foreach (var columnModel in parseTableData.Columns)
                        //    {
                        //        var dbColumn = dbColumns.Where(r => r.NameCompare(columnModel)).FirstOrDefault();
                        //        if (dbColumn == null)
                        //        {
                        //            _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(columnModel, AlterTableType.Add));
                        //            InputManager.Instance.WriteTrace($"Table[{parseTableData.TableName}] ColumnName[{columnModel.ColumnName}] (이)가 추가됩니다.");
                        //        }
                        //        else if (!dbColumn.TypeCompare(columnModel))
                        //        {
                        //            _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(columnModel, AlterTableType.Modify));
                        //            InputManager.Instance.WriteTrace($"Table[{parseTableData.TableName}] ColumnName[{columnModel.ColumnName}] [{dbColumn.ColumnType}] 에서 [{columnModel.ColumnType}] 으로 변경됩니다.");
                        //        }
                        //    }

                        //foreach (var column in dbColumns)
                        //{
                        //    if (parseTableData.Columns.Any(r => r.NameCompare(column)))
                        //        continue;
                        //    _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(column, AlterTableType.Drop));
                        //    InputManager.Instance.WriteTrace($"Table[{parseTableData.TableName}] ColumnName[{column.ColumnName}] (이)가 삭제됩니다.");
                        //} 
                    }
                }
            }

            _tableBuffer.AppendLine($"END $$;");

            _tableBuffer.AppendLine($"DELIMITER ;");

            _tableBuffer.AppendLine($"CALL `make_create_table`();");
            _tableBuffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_create_table`;");

            _tableBuffer.AppendLine();
        }

        private void Deployment(string path, PublishDeploymentType publishDeploymentType)
        {
            if (string.IsNullOrEmpty(path))
                return;

            InputManager.Instance.WriteInfo($">>>{publishDeploymentType.ToString()}");

            InputManager.Instance.Write($"Read File : {path}");

            var sqlCommand = File.ReadAllText(path);

            if (string.IsNullOrEmpty(path))
                throw new Exception($"File : { path } 내용이 없습니다.");

            using (var connection = new MySqlConnection(_config.SqlConnect))
            {
                connection.Open();
                var command = new MySqlScript(connection, sqlCommand);
                command.Execute();
                connection.Close();
            }
        }
        private void LoadSp()
        {
            _otherBuffer.AppendLine();
            InputManager.Instance.WriteInfo($">>>Load Stored Procedure Files : {_config.StoredProcedurePath}");
            var directoryInfo = new DirectoryInfo(_config.StoredProcedurePath);
            var files = GetSqlFiles(directoryInfo);
            for (int i = 0; i < files.Count; i++)
            {
                InputManager.Instance.Write($"Read File : {files[i].Name}");
                using (var sr = new StreamReader(files[i].OpenRead()))
                {
                    var sql = sr.ReadToEnd();
                    sr.Close();

                    if (string.IsNullOrEmpty(sql))
                        throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");

                    _otherBuffer.AppendLine(sql);
                    _otherBuffer.AppendLine();
                }
            }
        }
        private void OutputScript()
        {
            InputManager.Instance.WriteInfo(">>>Make Output Scripts");
            if (File.Exists(Consts.OutputScript))
                File.Delete(Consts.OutputScript);

            File.WriteAllText(Consts.OutputScript, _tableBuffer.ToString());

            File.AppendAllText(Consts.OutputScript, "\r\n");

            File.AppendAllText(Consts.OutputScript, _otherBuffer.ToString());

            InputManager.Instance.WriteTrace($"Create Script File : {Consts.OutputScript}");
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
