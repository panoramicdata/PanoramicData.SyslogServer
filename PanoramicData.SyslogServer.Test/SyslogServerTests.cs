using Xunit;

namespace PanoramicData.SyslogServer.Test;

public class SyslogServerTests
{
	[Fact]
	public void SyslogServer_Namespace_Exists()
	{
		var type = typeof(SyslogServer);
		Assert.NotNull(type);
	}
}
