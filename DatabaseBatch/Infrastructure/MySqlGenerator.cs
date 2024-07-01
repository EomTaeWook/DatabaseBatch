using DatabaseBatch.Models;
using Dignus.Collections;
using Dignus.DependencyInjection.Attributes;
using Dignus.Utils.Extensions;
using MySql.Data.MySqlClient;
using System.Text;

namespace DatabaseBatch.Infrastructure
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Transient)]
    internal class MySqlGenerator
    {
        private readonly Config _config;
        private readonly ArrayQueue<MySqlScript> _outputScripts = [];
        public MySqlGenerator(Config config)
        {
            _config = config;
        }
        public void GenerateScriptFromDirectory()
        {
            ProcessSqlFiles(LoadSqlFilesFromDirectory(_config.TablePath));
            ProcessSqlFiles(LoadSqlFilesFromDirectory(_config.AlterTablePath));
            ProcessSqlFiles(LoadSqlFilesFromDirectory(_config.StoredProcedurePath));
            OutputScripts();
        }
        private void OutputScripts()
        {
            StringBuilder stringBuilder = new();
            foreach (var script in _outputScripts)
            {
                stringBuilder.AppendLine(script.Query);
            }
            File.WriteAllText(Consts.OutputScript, stringBuilder.ToString());
            InputManager.Instance.WriteLine(ConsoleColor.Yellow, $"generate script : {Consts.OutputScript}");
        }
        private void ProcessSqlFiles(List<FileInfo> sqlFiles)
        {
            foreach (var file in sqlFiles)
            {
                InputManager.Instance.WriteLine(ConsoleColor.White, $"read file : {file.Name}");
                MySqlScript mySqlScript = new(File.ReadAllTextAsync(file.FullName).GetResult());
                _outputScripts.Add(mySqlScript);
            }
        }
        private List<FileInfo> LoadSqlFilesFromDirectory(string path)
        {
            var fileList = new List<FileInfo>();
            if (string.IsNullOrEmpty(path))
            {
                return fileList;
            }
            var directoryInfo = new DirectoryInfo(path);

            InputManager.Instance.WriteLine(ConsoleColor.Green, $"Path : {directoryInfo.FullName}");

            foreach (var directories in directoryInfo.GetDirectories())
            {
                fileList.AddRange(LoadSqlFilesFromDirectory(directories.FullName));
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (file.Extension.ToLower().Equals(".sql"))
                {
                    fileList.Add(file);
                }
            }
            return fileList;
        }
    }
}
