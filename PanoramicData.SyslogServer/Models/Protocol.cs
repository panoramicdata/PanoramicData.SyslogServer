namespace PanoramicData.SyslogServer.Models;

/// <summary>
/// The transport protocol used for a syslog message.
/// </summary>
public enum Protocol
{
	/// <summary>
	/// User Datagram Protocol.
	/// </summary>
	Udp,

	/// <summary>
	/// Transmission Control Protocol.
	/// </summary>
	Tcp
}