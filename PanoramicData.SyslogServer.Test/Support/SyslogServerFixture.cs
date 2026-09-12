using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PanoramicData.SyslogServer.Config;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PanoramicData.SyslogServer.Test.Support;

/// <summary>
/// Starts a <see cref="SyslogServer"/> on ephemeral loopback ports and provides clients for it.
/// </summary>
/// <remarks>
/// Ports are allocated by binding to port 0 and reading back what the operating system chose,
/// so test classes can run in parallel without colliding on a fixed port.
/// </remarks>
internal sealed class SyslogServerFixture : IAsyncDisposable
{
	private readonly SyslogServer _server;
	private bool _stopped;

	private SyslogServerFixture(SyslogServer server, RecordingSyslogApplication application, int? udpPort, int? tcpPort)
	{
		_server = server;
		Application = application;
		UdpPort = udpPort;
		TcpPort = tcpPort;
	}

	/// <summary>
	/// The application the server delivers parsed messages to.
	/// </summary>
	public RecordingSyslogApplication Application { get; }

	/// <summary>
	/// The server under test.
	/// </summary>
	public SyslogServer Server => _server;

	public int? UdpPort { get; }

	public int? TcpPort { get; }

	/// <summary>
	/// Starts a server listening on the requested protocols.
	/// </summary>
	public static async Task<SyslogServerFixture> StartAsync(bool udp = false, bool tcp = false)
	{
		var udpPort = udp ? GetFreePort(SocketType.Dgram, ProtocolType.Udp) : (int?)null;
		var tcpPort = tcp ? GetFreePort(SocketType.Stream, ProtocolType.Tcp) : (int?)null;

		var application = new RecordingSyslogApplication();
		var server = CreateServer(new SyslogServerConfiguration { UdpPort = udpPort, TcpPort = tcpPort }, application);

		await server.StartAsync(CancellationToken.None);

		return new SyslogServerFixture(server, application, udpPort, tcpPort);
	}

	/// <summary>
	/// Builds a server without starting it, for tests that exercise the lifecycle directly.
	/// </summary>
	public static SyslogServer CreateServer(SyslogServerConfiguration configuration, RecordingSyslogApplication application)
		=> new(Options.Create(configuration), NullLoggerFactory.Instance, application);

	/// <summary>
	/// Sends a single UDP datagram to the server.
	/// </summary>
	public async Task SendUdpAsync(string message)
	{
		var port = UdpPort ?? throw new InvalidOperationException("This fixture has no UDP listener.");
		using var client = new UdpClient();
		var payload = Encoding.UTF8.GetBytes(message);
		await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));
	}

	/// <summary>
	/// Opens a TCP connection to the server and sends a single message over it.
	/// </summary>
	public async Task SendTcpAsync(string message)
	{
		await using var connection = await ConnectTcpAsync();
		await connection.SendAsync(message);
	}

	/// <summary>
	/// Opens a TCP connection that the caller controls, for multi-message or disconnect tests.
	/// </summary>
	public async Task<TcpConnection> ConnectTcpAsync()
	{
		var port = TcpPort ?? throw new InvalidOperationException("This fixture has no TCP listener.");
		var client = new TcpClient();
		await client.ConnectAsync(IPAddress.Loopback, port);
		return new TcpConnection(client);
	}

	/// <summary>
	/// Stops the server, tolerating a server that is already stopped.
	/// </summary>
	public async Task StopAsync()
	{
		if (_stopped)
		{
			return;
		}

		_stopped = true;
		await _server.StopAsync(CancellationToken.None);
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync();
		_server.Dispose();
	}

	private static int GetFreePort(SocketType socketType, ProtocolType protocolType)
	{
		using var socket = new Socket(AddressFamily.InterNetwork, socketType, protocolType);
		socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
		return ((IPEndPoint)socket.LocalEndPoint!).Port;
	}

	/// <summary>
	/// A TCP client connection to the server under test.
	/// </summary>
	internal sealed class TcpConnection(TcpClient client) : IAsyncDisposable
	{
		public async Task SendAsync(string message)
		{
			var payload = Encoding.UTF8.GetBytes(message);
			await client.GetStream().WriteAsync(payload);
			await client.GetStream().FlushAsync();
		}

		public ValueTask DisposeAsync()
		{
			client.Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
