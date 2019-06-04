using DatabaseBatch.Infrastructure;
using DatabaseBatch.Infrastructure.Interface;
using DatabaseBatch.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

                InputManager.Instance.Write("작업이 완료되었습니다. 배포하시겠습니까?(Y)");
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
                var st = new StackTrace(ex, true);
                var frames = st.GetFrames().Select(f => 
                new
                {
                    FileName = f.GetFileName(),
                    LineNumber = f.GetFileLineNumber(),
                    ColumnNumber = f.GetFileColumnNumber(),
                    Method = f.GetMethod(),
                    Class = f.GetMethod().DeclaringType,
                });
                foreach(var frame in frames)
                {
                    if (string.IsNullOrEmpty(frame.FileName))
                        continue;
                    InputManager.Instance.WriteError($"File : {frame.FileName} line : {frame.LineNumber}");
                }
                InputManager.Instance.WriteError($"");

                InputManager.Instance.WriteError($"{ex.Message} \r\n {ex.StackTrace}");
                InputManager.Instance.WriteError($"");
            }
            InputManager.Instance.Write("\n프로그램을 종료합니다. 아무키나 누르세요.");
            Console.ReadKey();
        }
    }
}
