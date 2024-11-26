using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.SyslogServer.Config;
using PanoramicData.SyslogServer.Interfaces;
using PanoramicData.SyslogServer.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicData.SyslogServer;

public partial class SyslogServer(
	IOptions<SyslogServerConfiguration> options,
	ILoggerFactory loggerFactory,
	ISyslogApplication syslogApplication) : IHostedService, IDisposable
{
	private readonly Lock _lock = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly ILogger _logger = loggerFactory.CreateLogger<SyslogServer>();
	private bool _started;
	private bool _disposedValue;
	private Task? _udpListenerTask;
	private Task? _tcpListenerTask;

	public Guid Id { get; } = Guid.NewGuid();

	private readonly SyslogServerConfiguration _config = (options ?? throw new ArgumentNullException(nameof(options))).Value;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_started)
		{
			throw new InvalidOperationException("The server is already started.");
		}

		if (!_config.UdpPort.HasValue && !_config.TcpPort.HasValue)
		{
			throw new InvalidOperationException("At least one of UDP or TCP must be enabled.");
		}

		if (_config.UdpPort.HasValue)
		{
			_logger.LogInformation("Starting UDP listener on port {UdpPort}...", _config.UdpPort);
			try
			{
				_udpListenerTask = UdpListenerLoopAsync(_config.UdpPort.Value, _cancellationTokenSource.Token);
				_logger.LogInformation("Starting UDP listener on port {UdpPort} complete.", _config.UdpPort);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Error starting UDP listener on port {UdpPort}: {Message}",
					_config.UdpPort,
					ex.Message);
			}
		}

		if (_config.TcpPort.HasValue)
		{
			_logger.LogInformation("Starting TCP listener on port {TcpPort}...", _config.TcpPort);
			try
			{
				_tcpListenerTask = TcpListenerLoopAsync(_config.TcpPort.Value, _cancellationTokenSource.Token);
				_logger.LogInformation("Starting TCP listener on port {TcpPort} complete.", _config.TcpPort);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Error starting TCP listener on port {TcpPort}: {Message}",
					_config.TcpPort,
					ex.Message);
			}
		}

		_started = true;

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			if (!_started)
			{
				return Task.CompletedTask;
			}

			_cancellationTokenSource.Cancel();

			_udpListenerTask?.Wait(cancellationToken);
			_tcpListenerTask?.Wait(cancellationToken);

			_started = false;

		}

		return Task.CompletedTask;
	}

	private async Task UdpListenerLoopAsync(int udpServerPort, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Creating UDP Client...");
		using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, udpServerPort));

		_logger.LogDebug("Creating remote endpoint definition...");
		var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_logger.LogDebug("Waiting for UDP packet...");
				var receiveResult = await udpClient.ReceiveAsync(cancellationToken);

				_logger.LogDebug("Received UDP packet from {RemoteEndPoint}", remoteEndpoint);
				var message = Encoding.UTF8.GetString(receiveResult.Buffer);
				await ProcessSyslogMessageAsync(Protocol.Udp, receiveResult.RemoteEndPoint.Address, message);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogError(ex, "Error in UDP listener");
			}
		}
	}

	private Task TcpListenerLoopAsync(int tcpServerPort, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Creating TCP Client...");
		var tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, tcpServerPort));
		tcpListener.Start();

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				if (tcpListener.Pending())
				{
					_logger.LogDebug("New socket creating...");
					var client = tcpListener.AcceptTcpClient();
					_ = Task.Run(() => HandleTcpClientAsync(client, cancellationToken), cancellationToken);
				}

				Thread.Sleep(10); // Prevent CPU overuse
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogError(ex, "Error in TCP listener");
			}
		}

		tcpListener.Stop();

		return Task.CompletedTask;
	}

	private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
	{
		using (client)
		{
			var buffer = new byte[1024];
			var stream = client.GetStream();

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					if (stream.DataAvailable)
					{
						var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
						if (bytesRead == 0) break;

						var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
						await ProcessSyslogMessageAsync(
							Protocol.Tcp,
							(client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? throw new InvalidCastException("Could not case RemoteEndPoint as an IP endpoint"),
							message
						);
					}

					await Task.Delay(10, cancellationToken); // Prevent CPU overuse
				}
				catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
				{
					_logger.LogError(ex, "Error handling TCP client");
					break;
				}
			}
		}
	}

	private Task ProcessSyslogMessageAsync(
		Protocol protocol,
		IPAddress remoteIpAddress,
		string rawMessage)
	{
		try
		{
			// Parse PRI, HEADER, and MSG using a regex
			var syslogRegex = SyslogMessagePattern();
			var match = syslogRegex.Match(rawMessage);

			if (match.Success)
			{
				var syslogMessage = new SyslogMessage
				{
					Protocol = protocol,
					SourceIpAddress = remoteIpAddress,
					Priority = int.Parse(match.Groups["pri"].Value),
					Header = match.Groups["header"].Value,
					Message = match.Groups["msg"].Value
				};

				syslogApplication.SyslogMessageReceived(this, syslogMessage);
			}
			else
			{
				_logger.LogWarning("{Protocol} Unable to parse message: {Message}", protocol, rawMessage);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing syslog message");
		}

		return Task.CompletedTask;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_cancellationTokenSource.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	[GeneratedRegex(@"^<(?<pri>\d+)>(?<header>[^ ]+ [^ ]+ [^ ]+) (?<msg>.*)$")]
	private static partial Regex SyslogMessagePattern();
}
