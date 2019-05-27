using DatabaseBatch.Infrastructure;
using DatabaseBatch.Infrastructure.Interface;
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
                ISqlManager manger = new MySqlManager();
                manger.Init(config);
                manger.MakeScript();

                InputManager.Instance.Write("작업이 완료되었습니다. 배포하시겠습니까?(Y/N)");
                var input = Console.ReadKey();
                if (input.KeyChar == 'Y' || input.KeyChar == 'y')
                {
                    InputManager.Instance.Write("");
                    manger.Publish();
                    InputManager.Instance.Write("");
                    InputManager.Instance.Write("배포가 완료되었습니다.");
                }
            }
            catch(Exception ex)
            {
                InputManager.Instance.WriteError($"{ex.Message} \r\n {ex.StackTrace}");
            }
            InputManager.Instance.Write("\n프로그램을 종료합니다. 아무키나 누르세요.");
            Console.ReadKey();
        }
    }
}
