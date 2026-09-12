using System.Text.RegularExpressions;

namespace PanoramicData.SyslogServer;

/// <summary>
/// Source-generated regular expressions used to parse syslog messages.
/// </summary>
/// <remarks>
/// The pattern lives on this type rather than on <see cref="SyslogServer"/> so that the server
/// itself does not have to be declared partial purely to host a source-generated member.
/// </remarks>
internal static partial class SyslogMessagePatterns
{
	/// <summary>
	/// Matches a syslog message: a priority, a three-token header, and the message body.
	/// </summary>
	[GeneratedRegex(@"^<(?<pri>\d+)>(?<header>[^ ]+ [^ ]+ [^ ]+) (?<msg>.*)$")]
	internal static partial Regex SyslogMessage();
}
