using System;
using System.Runtime.InteropServices;

namespace XtermSharp {
	[StructLayout(LayoutKind.Sequential)]
	public struct MacWinSize {
		public short row, col, xpixel, ypixel;
	}

	public class Pty {
		[DllImport ("util")]
		extern static int forkpty (out int master, IntPtr dataReturn, IntPtr termios, ref MacWinSize WinSz);

		[DllImport ("libc")]
		extern static int execv (string process, string [] args);

		[DllImport ("libc")]
		extern static int execve (string process, string [] args, string [] env);

		public static int Fork (string process, string [] args, string [] env, out int master, MacWinSize winSize)
		{
			var pid = forkpty (out master, IntPtr.Zero, IntPtr.Zero, ref winSize);
			if (pid < 0)
				throw new Exception ("Could not create Pty");

			if (pid == 0) {
				execve (process, args, env);
			}
			return pid;
		}

	}
}
