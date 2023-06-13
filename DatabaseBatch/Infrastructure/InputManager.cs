using Dignus.Framework;

namespace DatabaseBatch.Infrastructure
{
    internal class InputManager : Singleton<InputManager>
    {
        public void WriteLine(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        public void Write(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.Write(message);
        }
    }
}
