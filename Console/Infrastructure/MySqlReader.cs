using DatabaseBatch.Extensions;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlReader
    {
        private readonly string[] _baseDelimiter;
        private readonly string _sql;
        private int _nextIndex, _beforeIndex;
        public MySqlReader(string sql) : this(sql, new string[] { ";" , "$$" })
        {
            
        }
        public MySqlReader(string[] delimiter, string sql) : this(sql, delimiter)
        {
        }
        public MySqlReader(string sql, string[] delimiter)
        {
            _sql = sql.Replace(new string[] { "\n", "\r\n", "\t", "\r" }, " ");
            _baseDelimiter = delimiter;
            _nextIndex = _beforeIndex = 0;

        }
        
        public bool NextLine(out string line)
        {
            if (_nextIndex >= _sql.Length)
            {
                line = null;
                return false;
            }
            _nextIndex = _sql.IndexOfAny(_baseDelimiter, _nextIndex, out int nextIndex);
            if(_nextIndex == -1)
            {
                _nextIndex = _sql.Length;
            }
            line = _sql.Substring(_beforeIndex, _nextIndex - _beforeIndex).Trim();
            _nextIndex += nextIndex;
            _beforeIndex = _nextIndex;
            return true;
        }
        
    }
}
