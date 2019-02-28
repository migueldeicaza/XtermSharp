using System;
using System.Runtime.InteropServices;

namespace XtermSharp {
	public class Pty {
		[DllImport ("util")]
		extern static int forkpty (out int master, IntPtr dataReturn, IntPtr termios, IntPtr WinSz);

		[DllImport ("libc")]
		extern static int execv (string process, string [] args);

		[DllImport ("libc")]
		extern static int execve (string process, string [] args, string [] env);

		public static int Fork (string process, string [] args, string [] env, out int master)
		{
			var pid = forkpty (out master, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			if (pid < 0)
				throw new Exception ("Could not create Pty");

			if (pid == 0) {
				execve (process, args, env);
			}
			return pid;
		}

	}
}
