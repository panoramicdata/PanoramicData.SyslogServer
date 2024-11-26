using PanoramicData.SyslogServer.Models;

namespace PanoramicData.SyslogServer.Interfaces;

public interface ISyslogApplication
{
	void SyslogMessageReceived(object sender, SyslogMessage message);
}
