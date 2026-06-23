using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP
{
    public class ConsoleApp : IServiceProvider
    {
        private ConsoleApp(string[]? args)
        {
            _host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(ConfigureLogging)
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private readonly IHost _host;

        public static ConsoleApp Current
        {
            get => field ?? throw new InvalidOperationException("应用程序尚未初始化");
            private set;
        }

        public static ConsoleApp Create(string[]? args)
        {
            Current = new ConsoleApp(args);
            return Current;
        }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            return _host.RunAsync(cancellationToken);
        }

        private void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logging)
        {
            logging.ClearProviders();
            logging.AddLog4Net("Config/log4net.config");
#if DEBUG
            logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Information);
#endif
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "terminal",
                    Version = "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ConsoleApp).Assembly);
        }

        public object? GetService(Type serviceType)
        {
            return _host.Services.GetService(serviceType);
        }
    }
}
