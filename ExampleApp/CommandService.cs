using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExampleApp;

public class CommandService
{
    private Process? _process;
	private readonly ProcessStartInfo _startInfo;

	public CommandService(string command, string args)
	{
		_startInfo = new ProcessStartInfo(command, args)
		{
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};
	}

   public event EventHandler<byte[]>? DataReceived;
	public event EventHandler? EofReceived;
	public event EventHandler<uint>? CloseReceived;

	public void Start()
	{
     _process = Process.Start(_startInfo) ?? throw new InvalidOperationException("Failed to start process.");
		Task.Run(() => MessageLoop());
	}

	public void OnData(byte[] data)
	{
       var process = _process ?? throw new InvalidOperationException("The process has not been started.");
		process.StandardInput.BaseStream.Write(data, 0, data.Length);
		process.StandardInput.BaseStream.Flush();
	}

  public void OnClose()
		=> (_process ?? throw new InvalidOperationException("The process has not been started.")).StandardInput.BaseStream.Close();

	private void MessageLoop()
	{
     var process = _process ?? throw new InvalidOperationException("The process has not been started.");
		var bytes = new byte[1024 * 64];
		while (true)
		{
         var len = process.StandardOutput.BaseStream.Read(bytes, 0, bytes.Length);
			if (len <= 0)
				break;

			var data = bytes.Length != len
				? bytes.Take(len).ToArray()
				: bytes;
			DataReceived?.Invoke(this, data);
		}

		EofReceived?.Invoke(this, EventArgs.Empty);
     CloseReceived?.Invoke(this, (uint)process.ExitCode);
	}
}
