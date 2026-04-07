using PanoramicData.SyslogServer.Models;

namespace PanoramicData.SyslogServer.Interfaces;

/// <summary>
/// Interface for applications that receive syslog messages.
/// </summary>
public interface ISyslogApplication
{
	/// <summary>
	/// Called when a syslog message is received.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="message">The received syslog message.</param>
	void SyslogMessageReceived(object sender, SyslogMessage message);
}
