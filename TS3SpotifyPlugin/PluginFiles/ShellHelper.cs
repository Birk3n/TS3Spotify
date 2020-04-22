//Thanks to this guy: https://loune.net/2017/06/running-shell-bash-commands-in-net-core/
using System;
using System.Diagnostics;
public static class ShellHelper
{
	public static string Bash(this string cmd)
	{
		var escapedArgs = cmd.Replace("\"", "\\\"");

		var process = new Process()
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "/bin/bash",
				Arguments = $"-c \"{escapedArgs}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			}
		};
		process.Start();
		string result = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return result;
	}
}
