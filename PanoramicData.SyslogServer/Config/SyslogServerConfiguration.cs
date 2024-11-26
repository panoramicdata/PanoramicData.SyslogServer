namespace PanoramicData.SyslogServer.Config;

public class SyslogServerConfiguration
{
	/// <summary>
	/// The address on which the SSH server should listen.
	/// Use:
	/// - an IP address
	/// - "IPv6Any" to listen dual stack
	/// - "Any" to listen on all network interfaces.
	/// </summary>
	public string LocalAddress { get; set; } = string.Empty;

	/// <summary>
	/// Whether to permit UDP and on which port, or null to disable UDP.
	/// </summary>
	public int? UdpPort { get; set; }

	/// <summary>
	/// Whether to permit TCP and on which port, or null to disable TCP.
	/// </summary>
	public int? TcpPort { get; set; }
}
