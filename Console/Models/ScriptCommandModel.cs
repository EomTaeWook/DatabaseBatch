using MySql.Data.MySqlClient;
using System.Data;

namespace DatabaseBatch.Models
{
    public class MyScriptCommandModel : MySqlScript, IDbCommand
    {
        public int ExecuteNonQuery()
        {
            return this.Execute();
        }

        IDbConnection IDbCommand.Connection { get => this.Connection; set => throw new System.NotImplementedException(); }
        public IDbTransaction Transaction { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public string CommandText { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public int CommandTimeout { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public CommandType CommandType { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public IDataParameterCollection Parameters => throw new System.NotImplementedException();

        public UpdateRowSource UpdatedRowSource { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public void Cancel()
        {
            throw new System.NotImplementedException();
        }

        public IDbDataParameter CreateParameter()
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public IDataReader ExecuteReader()
        {
            throw new System.NotImplementedException();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            throw new System.NotImplementedException();
        }

        public object ExecuteScalar()
        {
            throw new System.NotImplementedException();
        }

        public void Prepare()
        {
            throw new System.NotImplementedException();
        }
    }
}
