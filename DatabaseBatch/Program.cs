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
            var serviceProvider = InitDependencies();

            var mode = SelectMode();

            if (mode == 1)
            {
                var mySqlGenerator = serviceProvider.GetService<MySqlGenerator>();
                mySqlGenerator.GenerateScriptFromDirectory();
            }
            else
            {
                var manager = serviceProvider.GetService<MySqlManager>();
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
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex);
        }

        Console.WriteLine();
        InputManager.Instance.WriteLine(ConsoleColor.White, "프로그램을 종료합니다. 아무키나 누르세요.");
        Console.ReadKey();
    }
    private static IServiceProvider InitDependencies()
    {
        Config config = JsonSerializer.Deserialize<Config>(File.ReadAllText("./config.json"));

        var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterDependencies(Assembly.GetExecutingAssembly());
        serviceContainer.RegisterType(config);
        serviceContainer.RegisterType(config.DbConfig);
        serviceContainer.RegisterType(config.Publish);

        return serviceContainer.Build();
    }
    private static int SelectMode()
    {
        Console.WriteLine("Select an operation mode:");

        Console.WriteLine("1. Generate script from current directory.");
        Console.WriteLine("2. Connect to database and process changes.");

        while (true)
        {
            Console.Write("Enter your choice (1 or 2): ");
            var key = Console.ReadKey();
            Console.ReadLine();
            Console.WriteLine(); // To ensure subsequent outputs are on a new line.

            switch (key.KeyChar)
            {
                case '1':
                    return 1;

                case '2':
                    return 2;

                default:
                    Console.WriteLine("Invalid input. Please enter '1' or '2'.");
                    break;
            }
        }
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