using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PanoramicData.SyslogServer;
using PanoramicData.SyslogServer.Config;
using PanoramicData.SyslogServer.Interfaces;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp;

partial class Program
{
	static async Task Main()
	{
		var cancellationTokenSource = new CancellationTokenSource();
		var host = Host.CreateDefaultBuilder()
			.ConfigureServices((hostBuilderContext, serviceCollection) =>
			{
				serviceCollection
					.AddOptions()
					.Configure<SyslogServerConfiguration>(hostBuilderContext.Configuration.GetSection("SyslogServer"))
					.Configure<ExampleSyslogApplicationConfiguration>(hostBuilderContext.Configuration.GetSection("Application"))

					// Register services
					.AddSingleton<IHostedService, SyslogServer>()
					.AddSingleton<ISyslogApplication, ExampleSyslogApplication>();
			})
			.UseSerilog((context, _, loggerConfiguration)
				=> loggerConfiguration
					.ReadFrom.Configuration(context.Configuration)
					.Enrich.FromLogContext()
			)
			.Build();

		// Start the host
		await host.StartAsync(cancellationTokenSource.Token);
	}
}
