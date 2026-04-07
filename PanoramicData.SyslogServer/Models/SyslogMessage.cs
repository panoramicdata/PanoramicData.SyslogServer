using System.Net;

namespace PanoramicData.SyslogServer.Models;

/// <summary>
/// Represents a parsed syslog message.
/// </summary>
public class SyslogMessage
{
	/// <summary>
	/// The transport protocol over which the message was received.
	/// </summary>
	public required Protocol Protocol { get; set; }

	/// <summary>
	/// The IP address of the host that sent the message.
	/// </summary>
	public required IPAddress SourceIpAddress { get; init; }

	/// <summary>
	/// The syslog priority value.
	/// </summary>
	public required int Priority { get; init; }

	/// <summary>
	/// The syslog message header.
	/// </summary>
	public required string Header { get; init; }

	/// <summary>
	/// The syslog message body.
	/// </summary>
	public required string Message { get; init; }
}
