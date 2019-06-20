using DatabaseBatch.Extensions;

namespace DatabaseBatch.Infrastructure
{
    public class BaseSqlReader
    {
        protected readonly string[] _baseDelimiter;
        protected readonly string _sql;
        private int _nextIndex, _beforeIndex;
        public BaseSqlReader(string sql) : this(sql, new string[] { ";" })
        {

        }
        public BaseSqlReader(string[] delimiter, string sql) : this(sql, delimiter)
        {
        }
        public BaseSqlReader(string sql, string[] delimiter)
        {
            _sql = sql.Replace(new string[] { "\r\n", "\t", "\n", "\r" }, " ");
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
            if (_nextIndex == -1)
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
