using Dignus.Collections;

namespace DatabaseBatch.Infrastructure
{
    internal class SimpleStringReader
    {
        private readonly ArrayQueue<char> _buffer = [];
        public SimpleStringReader(string str)
        {
            _buffer.AddRange(str.ToCharArray());
        }
        public char Peek()
        {
            if (_buffer.Count == 0)
            {
                return char.MinValue;
            }
            return _buffer.Peek();
        }
        public char Read()
        {
            return _buffer.Read();
        }
    }
}
