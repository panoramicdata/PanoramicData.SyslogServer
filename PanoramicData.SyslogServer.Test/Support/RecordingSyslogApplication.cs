using PanoramicData.SyslogServer.Interfaces;
using PanoramicData.SyslogServer.Models;
using System.Threading.Channels;

namespace PanoramicData.SyslogServer.Test.Support;

/// <summary>
/// An <see cref="ISyslogApplication"/> that records what the server delivers to it.
/// </summary>
/// <remarks>
/// Messages are queued in a channel so that tests can await the next delivery rather than
/// sleeping for an arbitrary period, which is what keeps the socket tests from being flaky.
/// </remarks>
internal sealed class RecordingSyslogApplication : ISyslogApplication
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	private readonly Channel<SyslogMessage> _messages = Channel.CreateUnbounded<SyslogMessage>();

	/// <summary>
	/// The senders the server passed alongside each message.
	/// </summary>
	public List<object> Senders { get; } = [];

	public void SyslogMessageReceived(object sender, SyslogMessage message)
	{
		lock (Senders)
		{
			Senders.Add(sender);
		}

		_messages.Writer.TryWrite(message);
	}

	/// <summary>
	/// Waits for the next message the server delivers.
	/// </summary>
	/// <exception cref="TimeoutException">No message arrived within the timeout.</exception>
	public async Task<SyslogMessage> WaitForNextAsync(TimeSpan? timeout = null)
	{
		using var cancellationTokenSource = new CancellationTokenSource(timeout ?? DefaultTimeout);
		try
		{
			return await _messages.Reader.ReadAsync(cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException(
				$"Expected a syslog message within {(timeout ?? DefaultTimeout).TotalSeconds:0.#}s, but none was delivered.");
		}
	}

	/// <summary>
	/// Waits for the next <paramref name="count"/> messages, in delivery order.
	/// </summary>
	public async Task<IReadOnlyList<SyslogMessage>> WaitForAsync(int count, TimeSpan? timeout = null)
	{
		var received = new List<SyslogMessage>(count);
		for (var i = 0; i < count; i++)
		{
			received.Add(await WaitForNextAsync(timeout));
		}

		return received;
	}

	/// <summary>
	/// Asserts that nothing is delivered within the given window.
	/// </summary>
	public async Task AssertNoMessageAsync(TimeSpan within)
	{
		using var cancellationTokenSource = new CancellationTokenSource(within);
		try
		{
			var unexpected = await _messages.Reader.ReadAsync(cancellationTokenSource.Token);
			throw new InvalidOperationException(
				$"Expected no syslog message, but received one with priority {unexpected.Priority} and body '{unexpected.Message}'.");
		}
		catch (OperationCanceledException)
		{
			// Nothing arrived, which is what we wanted.
		}
	}
}
