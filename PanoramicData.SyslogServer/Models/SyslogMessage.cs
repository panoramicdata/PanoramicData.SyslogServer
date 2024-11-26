using System.Net;

namespace PanoramicData.SyslogServer.Models;

public class SyslogMessage
{
	public required Protocol Protocol { get; set; }

	public required IPAddress SourceIpAddress { get; init; }

	public required int Priority { get; init; }

	public required string Header { get; init; }

	public required string Message { get; init; }

}
