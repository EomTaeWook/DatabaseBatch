using DatabaseBatch.Infrastructure;
using DatabaseBatch.Models;
using Dignus.DependencyInjection;
using Dignus.DependencyInjection.Extensions;
using Dignus.Log;
using Dignus.Log.LogTargets;
using Dignus.Log.Models;
using Dignus.Log.Rules;
using System.Reflection;
using System.Text.Json;


internal class Program
{
    private static void Main(string[] args)
    {
        InitLog();
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
    }
    private static void InitLog()
    {
        var logConfiguration = new LogConfiguration();
        ConsoleLogTarget consoleLogTarget = new()
        {
            LogFormatRenderer = "${message} | ${callerFileName} : ${callerLineNumber}",
            ErrorColor = ConsoleColor.Red,
            InfoColor = ConsoleColor.Green,
            FatalColor = ConsoleColor.DarkRed
        };
        var rule = new LoggerRule("console", LogLevel.Debug, consoleLogTarget);
        logConfiguration.AddLogRule("console rule", rule);
        LogBuilder.Configuration(logConfiguration);
        LogBuilder.Build();
        LogHelper.SetLogger(LogManager.GetLogger("console"));
    }
}