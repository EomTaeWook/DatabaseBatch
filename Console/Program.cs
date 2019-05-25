using DatabaseBatch.Infrastructure;
using DatabaseBatch.Models;
using System;
using System.IO;

namespace DatabaseBatch
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(Consts.ConfigPath));
                
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n작업이 완료되었습니다. 아무키나 누르세요.");
            Console.ReadKey();
        }
    }
}
