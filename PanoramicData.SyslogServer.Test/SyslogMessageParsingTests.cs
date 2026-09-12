using PanoramicData.SyslogServer.Test.Support;
using Xunit;

namespace PanoramicData.SyslogServer.Test;

/// <summary>
/// Covers how raw text on the wire becomes a <see cref="Models.SyslogMessage"/>.
/// </summary>
/// <remarks>
/// These drive the parser through a real UDP socket rather than calling it directly, because one
/// datagram maps to exactly one parse attempt, which keeps the assertions unambiguous.
/// </remarks>
public class SyslogMessageParsingTests
{
	[Theory]
	[InlineData("<0>Oct 11 22:14:15 su: emergency", 0, "su: emergency")]
	[InlineData("<34>Oct 11 22:14:15 su: failed", 34, "su: failed")]
	[InlineData("<165>Oct 11 22:14:15 su: local use", 165, "su: local use")]
	[InlineData("<191>Oct 11 22:14:15 su: highest", 191, "su: highest")]
	public async Task Priority_IsParsedAcrossTheValidRange(string raw, int expectedPriority, string expectedBody)
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync(raw);

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(expectedPriority, message.Priority);
		Assert.Equal(expectedBody, message.Message);
	}

	[Fact]
	public async Task MessageBody_MayContainSpacesAndPunctuation()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 sshd[1234]: Accepted publickey for root from 10.0.0.1 port 22");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal("sshd[1234]: Accepted publickey for root from 10.0.0.1 port 22", message.Message);
	}

	[Fact]
	public async Task MessageBody_MayContainNonAsciiCharacters()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 app: naïve café — 日本語 ✓");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal("app: naïve café — 日本語 ✓", message.Message);
	}

	[Fact]
	public async Task MessageBody_MayBeEmpty()
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync("<34>Oct 11 22:14:15 ");

		var message = await fixture.Application.WaitForNextAsync();
		Assert.Equal(string.Empty, message.Message);
	}

	[Theory]
	[InlineData("no priority at all")]
	[InlineData("Oct 11 22:14:15 su: missing the priority")]
	[InlineData("<34>tooshort")]
	[InlineData("<>Oct 11 22:14:15 su: empty priority")]
	[InlineData("<abc>Oct 11 22:14:15 su: non-numeric priority")]
	[InlineData("")]
	public async Task UnparseableMessage_IsDroppedRatherThanDelivered(string raw)
	{
		await using var fixture = await SyslogServerFixture.StartAsync(udp: true);

		await fixture.SendUdpAsync(raw);

		await fixture.Application.AssertNoMessageAsync(TimeSpan.FromMilliseconds(500));
	}
}
