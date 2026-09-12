using PanoramicData.SyslogServer.Config;
using PanoramicData.SyslogServer.Models;
using PanoramicData.SyslogServer.Test.Support;
using System.Net;
using Xunit;

namespace PanoramicData.SyslogServer.Test;

public class SyslogServerTcpTests
{
	[Fact]
	public async Task StartAsync_WithTcpEnabled_ReturnsPromptly()
	{
		using var server = SyslogServerFixture.CreateServer(
			new SyslogServerConfiguration { TcpPort = 51500 },
			new RecordingSyslogApplication());

		var start = Task.Run(() => server.StartAsync(CancellationToken.None), TestContext.Current.CancellationToken);
		var finished = await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

		Assert.True(
			ReferenceEquals(finished, start),
			"StartAsync did not return within 5 seconds: the TCP listener loop is running synchronously on the caller's thread.");

		await server.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task Message_IsParsedAndDelivered()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);

		await fixture.SendTcpAsync("<34>Oct 11 22:14:15 su: failed for lonvick");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(Protocol.Tcp, message.Protocol);
		Assert.Equal(34, message.Priority);
		Assert.Equal("Oct 11 22:14:15", message.Header);
		Assert.Equal("su: failed for lonvick", message.Message);
	}

	[Fact]
	public async Task Message_RecordsTheSenderIpAddress()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);

		await fixture.SendTcpAsync("<13>Jan 1 00:00:00 host: hello");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(IPAddress.Loopback, message.SourceIpAddress);
	}

	[Fact]
	public async Task TwoMessagesOnOneConnection_AreBothDelivered()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);
		await using var connection = await fixture.ConnectTcpAsync();

		await connection.SendAsync("<34>Oct 11 22:14:15 su: first");
		var first = await fixture.Application.WaitForNextAsync();

		await connection.SendAsync("<34>Oct 11 22:14:16 su: second");
		var second = await fixture.Application.WaitForNextAsync();

		Assert.Equal("su: first", first.Message);
		Assert.Equal("su: second", second.Message);
	}

	[Fact]
	public async Task TwoConcurrentConnections_AreBothServed()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);
		await using var first = await fixture.ConnectTcpAsync();
		await using var second = await fixture.ConnectTcpAsync();

		await first.SendAsync("<34>Oct 11 22:14:15 su: from-first");
		await second.SendAsync("<34>Oct 11 22:14:15 su: from-second");

		var messages = await fixture.Application.WaitForAsync(2);
		var bodies = messages.Select(message => message.Message).ToList();
		Assert.Contains("su: from-first", bodies);
		Assert.Contains("su: from-second", bodies);
	}

	[Fact]
	public async Task MalformedMessage_IsIgnoredAndTheListenerKeepsServing()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);

		await fixture.SendTcpAsync("this is not a syslog message");
		await fixture.SendTcpAsync("<34>Oct 11 22:14:15 su: still here");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal("su: still here", message.Message);
	}

	[Fact]
	public async Task ClientDisconnecting_DoesNotStopTheListener()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(tcp: true);

		var connection = await fixture.ConnectTcpAsync();
		await connection.SendAsync("<34>Oct 11 22:14:15 su: before disconnect");
		await fixture.Application.WaitForNextAsync();
		await connection.DisposeAsync();

		await fixture.SendTcpAsync("<34>Oct 11 22:14:16 su: after disconnect");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal("su: after disconnect", message.Message);
	}
}
