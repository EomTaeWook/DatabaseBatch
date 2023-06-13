using DatabaseBatch.Extensions;
using Dignus.Buffer;
using System.Reflection.PortableExecutable;
using System.Text;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlReader
    {
        private readonly string[] _delimiters;
        private StringBuilder _sb = new StringBuilder();
        private SimpleStringReader _reader;

        public MySqlReader(string query) : this(query , new string[] { ";", "$$" })
        {
        }
        public MySqlReader(string query, string[] delimiters)
        {
            query = query.Replace(new string[] { "\r\n", "\t", "\n", "\r" }, " ");

            _reader = new SimpleStringReader(query);
            _delimiters = delimiters;
        }
        public bool NextLine(out string line)
        {
            line = null;

            if (_reader.Peek() == -1)
            {
                return false;
            }

            while(_reader.Peek() != -1)
            {
                char ch = (char)_reader.Read();
                var nextCh = (char)_reader.Peek();

                foreach (var delimiter in _delimiters)
                {
                    if(delimiter == ch.ToString())
                    {
                        line = _sb.ToString().Trim();
                        _sb.Clear();
                        return true;
                    }
                    else if(delimiter == new string(ch, nextCh).ToString())
                    {
                        line = _sb.ToString().Trim();
                        _sb.Clear();
                        return true;
                    }
                }
                _sb.Append(ch);
            }
            line = _sb.ToString().Trim();
            return true;
        }
    }
}
