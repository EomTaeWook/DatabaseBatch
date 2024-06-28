using Dignus.DependencyInjection.Attributes;

namespace DatabaseBatch.Models
{
    [Injectable(Dignus.DependencyInjection.LifeScope.Singleton)]
    public class DBContext
    {
        private readonly string _connString;
        private readonly string _databaseName;
        public DBContext(DBConfig config)
        {
            _connString = $"Server={config.EndPoint}; Port={config.Port}; Database={config.Database}; Uid={config.UserId}; Pwd={config.Password};";

            _databaseName = config.Database.ToLower();
        }
        public string GetDatabaseName()
        {
            return _databaseName;
        }
        public string GetConnString()
        {
            return _connString;
        }
    }

}
