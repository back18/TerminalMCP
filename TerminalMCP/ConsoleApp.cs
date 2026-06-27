using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TerminalMCP.Services;
using TerminalMCP.Services.Implementations;
using TerminalMCP.Tools;

namespace TerminalMCP
{
    public class ConsoleApp
    {
        private ConsoleApp(string[]? args)
        {
            _host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(ConfigureLogging)
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private readonly IHost _host;

        public static ConsoleApp Create(string[]? args)
        {
            return new ConsoleApp(args);
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
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<ITerminalCaptureService, TerminalCaptureService>();
            services.AddSingleton<ITerminalProcessService, TerminalProcessService>();
            services.AddSingleton<TerminalTools>();

            services.AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "terminal_mcp",
                    Version = "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ConsoleApp).Assembly);
        }
    }
}
