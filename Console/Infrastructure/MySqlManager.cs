using DatabaseBatch.Extensions;
using DatabaseBatch.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlManager : BaseSqlManager
    {
        private StringBuilder _buffer = new StringBuilder();
        public override void Init(Config config)
        {
            _config = config;
        }
        public override void Publish()
        {
            var deployment = _config.Publish;
            for (var e = PublishDeploymentType.PreDeployment; e < PublishDeploymentType.Max; ++e )
            {
                var prop = deployment.GetType().GetProperties().Where(r => r.Name == e.ToString()).FirstOrDefault();
                var path = deployment.GetType().GetProperty(prop.Name).GetValue(deployment).ToString();
                Deployment(path, e);
            }
        }
        public override void MakeScript()
        {
            LoadTable();
            InputManager.Instance.WriteInfo("");
            LoadSp();
            InputManager.Instance.WriteInfo("");
            OutputScript();
            InputManager.Instance.WriteInfo("");
        }
        
        private void LoadTable()
        {
            var currentTableInfos = GetMySqlTableInfo(new MySqlConnection(_config.SqlConnect));

            _buffer.AppendLine($"DELIMITER $$");
            _buffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_create_table`;");
            _buffer.AppendLine($"CREATE PROCEDURE `make_create_table`() BEGIN");
            InputManager.Instance.WriteInfo($">>>>Load Table Files : {_config.TablePath}");

            var directoryInfo = new DirectoryInfo(_config.TablePath);
            var files = GetSqlFiles(directoryInfo);
            
            for(int i=0; i< files.Count; i++)
            {
                InputManager.Instance.Write($"Read File : {files[i].Name}");
                using (var sr = new StreamReader(files[i].OpenRead()))
                {
                    var sql = sr.ReadToEnd();
                    sr.Close();

                    if (string.IsNullOrEmpty(sql))
                        throw new Exception($"{files[i].Name} : 쿼리 문이 없습니다.");

                    var parseTableData = SqlParseHelper.ParseMysqlDDLCommnad(sql);
                   
                    if(currentTableInfos.ContainsKey(parseTableData.Item1.ToLower()))
                    {
                        var currentTableColumns = currentTableInfos[parseTableData.Item1.ToLower()];
                        for (int ii=0; ii< parseTableData.Item2.Count; ii++)
                        {
                            var column = currentTableColumns.Where(r => r.NameCompare(parseTableData.Item2[ii])).FirstOrDefault();
                            if(column == null)
                            {
                                _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(parseTableData.Item2[ii], AlterTableType.Add));
                                InputManager.Instance.WriteTrace($"Table[{parseTableData.Item1}] ColumnName[{parseTableData.Item2[ii].ColumnName}] (이)가 추가됩니다.");
                            }
                            else if(!column.TypeCompare(parseTableData.Item2[ii]))
                            {
                                _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(parseTableData.Item2[ii], AlterTableType.Modify));
                                InputManager.Instance.WriteTrace($"Table[{parseTableData.Item1}] ColumnName[{parseTableData.Item2[ii].ColumnName}] [{column.ColumnType}] 에서 [{parseTableData.Item2[ii].ColumnType}] 으로 변경됩니다.");
                            }
                        }
                        for (int ii = 0; ii < currentTableColumns.Count; ii++)
                        {
                            if(parseTableData.Item2.Any(r=> r.NameCompare(currentTableColumns[ii])))
                                continue;

                            _buffer.AppendLine(SqlParseHelper.AlterMySqlColumn(currentTableColumns[ii], AlterTableType.Drop));
                            InputManager.Instance.WriteTrace($"Table[{parseTableData.Item1} ColumnName[{currentTableColumns[ii].ColumnName}] (이)가 삭제됩니다.");
                        }
                    }
                    else
                    {
                        //새로 생긴 테이블
                        _buffer.AppendLine(sql);
                        InputManager.Instance.WriteTrace($"Table[{parseTableData.Item1} (이)가 생성됩니다.");
                    }
                }
            }

            _buffer.AppendLine($"END $$;");

            _buffer.AppendLine($"DELIMITER ;");

            _buffer.AppendLine($"CALL `make_create_table`();");
            _buffer.AppendLine($"DROP PROCEDURE IF EXISTS `make_create_table`;");

            _buffer.AppendLine();
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
            _buffer.AppendLine();
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

                    _buffer.AppendLine(sql);
                    _buffer.AppendLine();
                }
            }
        }
        private void OutputScript()
        {
            InputManager.Instance.WriteInfo(">>>Make Output Scripts");
            if (File.Exists(Consts.OutputScript))
                File.Delete(Consts.OutputScript);

            File.WriteAllText(Consts.OutputScript, _buffer.ToString());

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
