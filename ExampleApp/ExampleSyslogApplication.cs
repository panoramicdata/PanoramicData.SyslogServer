using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.SyslogServer.Interfaces;
using PanoramicData.SyslogServer.Models;

namespace ExampleApp;

internal class ExampleSyslogApplication(
	IOptions<ExampleSyslogApplicationConfiguration> options,
	ILogger<Program> logger) : ISyslogApplication
{
	private readonly ExampleSyslogApplicationConfiguration _config = options.Value;

	public void SyslogMessageReceived(object sender, SyslogMessage message)
		=> logger.LogInformation(
			"{ServerName} Protocol: {Protocol} IP Address: {IpAddress} Priority: {Priority}, Header: {Header}, Message: {Message}",
			_config.ServerName,
			message.Protocol,
			message.SourceIpAddress,
			message.Priority,
			message.Header,
			message.Message);
}