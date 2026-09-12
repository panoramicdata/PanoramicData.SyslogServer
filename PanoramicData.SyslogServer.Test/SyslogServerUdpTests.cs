using PanoramicData.SyslogServer.Models;
using PanoramicData.SyslogServer.Test.Support;
using System.Net;
using Xunit;

namespace PanoramicData.SyslogServer.Test;

public class SyslogServerUdpTests
{
	[Fact]
	public async Task Datagram_IsParsedAndDelivered()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 su: failed for lonvick");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(Protocol.Udp, message.Protocol);
		Assert.Equal(34, message.Priority);
		Assert.Equal("Oct 11 22:14:15", message.Header);
		Assert.Equal("su: failed for lonvick", message.Message);
	}

	[Fact]
	public async Task Datagram_RecordsTheSenderIpAddress()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<13>Jan 1 00:00:00 host: hello");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(IPAddress.Loopback, message.SourceIpAddress);
	}

	[Fact]
	public async Task Datagram_PassesTheServerAsTheEventSender()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 su: hello");
		await fixture.Application.WaitForNextAsync();

		Assert.Same(fixture.Server, Assert.Single(fixture.Application.Senders));
	}

	[Fact]
	public async Task ThreeDatagrams_AreAllDelivered()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 su: one");
		await fixture.SendUdpAsync("<34>Oct 11 22:14:16 su: two");
		await fixture.SendUdpAsync("<34>Oct 11 22:14:17 su: three");

		var messages = await fixture.Application.WaitForAsync(3);
		var bodies = messages.Select(message => message.Message).ToList();
		Assert.Contains("su: one", bodies);
		Assert.Contains("su: two", bodies);
		Assert.Contains("su: three", bodies);
	}

	[Fact]
	public async Task MalformedDatagram_IsIgnoredAndTheListenerKeepsServing()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("this is not a syslog message");
		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 su: still here");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal("su: still here", message.Message);
	}

	[Fact]
	public async Task BothProtocolsEnabled_EachDeliversWithItsOwnProtocol()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true, tcp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 su: over-udp");
		await fixture.SendTcpAsync("<34>Oct 11 22:14:15 su: over-tcp");

		var messages = await fixture.Application.WaitForAsync(2);
		var byBody = messages.ToDictionary(message => message.Message, message => message.Protocol);
		Assert.Equal(Protocol.Udp, byBody["su: over-udp"]);
		Assert.Equal(Protocol.Tcp, byBody["su: over-tcp"]);
	}
}
