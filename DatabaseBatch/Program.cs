using DatabaseBatch.Infrastructure;
using DatabaseBatch.Models;
using Dignus.DependencyInjection;
using Dignus.Extensions.Log;
using Dignus.Log;
using System.Reflection;
using System.Text.Json;


LogBuilder.Configuration(LogConfigXmlReader.Load("./DignusLog.config")).Build();

try
{
    var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("./config.json"));

    var serviceContainer = new ServiceContainer();
    serviceContainer.RegisterDependencies(Assembly.GetExecutingAssembly());
    serviceContainer.RegisterType(config);
    serviceContainer.RegisterType(config.DbConfig);
    serviceContainer.RegisterType(config.Publish);
    
    var manager = serviceContainer.Resolve<MySqlManager>();
    manager.Init();
    manager.MakeScript();

    Console.WriteLine();
    InputManager.Instance.WriteLine(ConsoleColor.White, "작업이 완료되었습니다. 배포하시겠습니까?(Y)");
    var input = Console.ReadKey();
    if (input.KeyChar == 'Y' || input.KeyChar == 'y')
    {
        manager.Publish();
        InputManager.Instance.WriteLine(ConsoleColor.White, "배포가 완료되었습니다.");
    }
}
catch (Exception ex)
{
    LogHelper.Error(ex);
}

Console.WriteLine();
InputManager.Instance.WriteLine(ConsoleColor.White, "프로그램을 종료합니다. 아무키나 누르세요.");
Console.ReadKey();