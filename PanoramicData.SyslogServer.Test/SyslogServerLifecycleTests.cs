using PanoramicData.SyslogServer.Config;
using PanoramicData.SyslogServer.Test.Support;
using Xunit;

namespace PanoramicData.SyslogServer.Test;

public class SyslogServerLifecycleTests
{
	[Fact]
	public async Task StartAsync_WithNeitherProtocolEnabled_Throws()
	{
		using var server = SyslogServerFixture.CreateServer(new SyslogServerConfiguration(), new RecordingSyslogApplication());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => server.StartAsync(TestContext.Current.CancellationToken));

		Assert.Equal("At least one of UDP or TCP must be enabled.", exception.Message);
	}

	[Fact]
	public async Task StartAsync_WhenAlreadyStarted_Throws()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => fixture.Server.StartAsync(TestContext.Current.CancellationToken));

		Assert.Equal("The server is already started.", exception.Message);
	}

	[Fact]
	public async Task StopAsync_BeforeStart_DoesNothing()
	{
		using var server = SyslogServerFixture.CreateServer(
			new SyslogServerConfiguration { UdpPort = 51400 },
			new RecordingSyslogApplication());

		await server.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task StopAsync_AfterStartingUdp_CompletesWithoutThrowing()
	{
		var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.StopAsync();

		fixture.Server.Dispose();
	}

	[Fact]
	public async Task StopAsync_CalledTwice_DoesNothingTheSecondTime()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.Server.StopAsync(TestContext.Current.CancellationToken);
		await fixture.Server.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public void Id_IsUniquePerInstance()
	{
		var application = new RecordingSyslogApplication();
		using var first = SyslogServerFixture.CreateServer(new SyslogServerConfiguration { UdpPort = 51401 }, application);
		using var second = SyslogServerFixture.CreateServer(new SyslogServerConfiguration { UdpPort = 51402 }, application);

		Assert.NotEqual(Guid.Empty, first.Id);
		Assert.NotEqual(first.Id, second.Id);
	}
}
