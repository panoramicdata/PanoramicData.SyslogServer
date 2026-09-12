using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PanoramicData.SyslogServer;
using PanoramicData.SyslogServer.Config;
using PanoramicData.SyslogServer.Interfaces;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp;

internal sealed class Program
{
	// Never instantiated; it exists only as the entry point and as the ILogger<> category type.
	private Program()
	{
	}

	static async Task Main()
	{
		using var cancellationTokenSource = new CancellationTokenSource();
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
