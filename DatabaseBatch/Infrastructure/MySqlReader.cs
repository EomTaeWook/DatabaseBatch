using DatabaseBatch.Extensions;
using System.Text;

namespace DatabaseBatch.Infrastructure
{
    public class MySqlReader
    {
        private readonly string[] _delimiters;
        private readonly StringBuilder _sb = new();
        private readonly SimpleStringReader _reader;
        private static readonly string[] stringArray = [";", "$$"];
        private static readonly string[] replacements = ["\r\n", "\t", "\n", "\r"];

        public MySqlReader(string query) : this(query, stringArray)
        {
        }
        public MySqlReader(string query, string[] delimiters)
        {
            query = query.Replace(replacements, " ");
            _reader = new SimpleStringReader(query);
            _delimiters = delimiters;
        }
        public bool NextLine(out string line)
        {
            line = null;

            if (_reader.Peek() == char.MinValue)
            {
                return false;
            }

            while (_reader.Peek() != char.MinValue)
            {
                char ch = _reader.Read();
                var nextCh = _reader.Peek();

                foreach (var delimiter in _delimiters)
                {
                    if (delimiter == ch.ToString())
                    {
                        line = _sb.ToString().Trim();
                        _sb.Clear();
                        return true;
                    }
                    else if (delimiter == new string(ch, nextCh).ToString())
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
