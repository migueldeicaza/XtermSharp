using System;
using System.Runtime.InteropServices;
using System.IO;

namespace XtermSharp {
	[StructLayout(LayoutKind.Sequential)]
	public struct UnixWindowSize {
		public short row, col, xpixel, ypixel;
	}

	public class Pty {
		[DllImport ("util")]
		extern static int forkpty (out int master, IntPtr dataReturn, IntPtr termios, ref UnixWindowSize WinSz);

		[DllImport ("libc")]
		extern static int execv (string process, string [] args);

		[DllImport ("libc")]
		extern static int execve (string process, string [] args, string [] env);

		[DllImport ("libpty.dylib", EntryPoint="fork_and_exec")]
		extern static int HeavyFork (string process, string [] args, string [] env, out int master, UnixWindowSize winSize);

		static bool HeavyDuty = true;
		/// <summary>
		/// Forks a process and returns a file handle that is connected to the standard output of the child process
		/// </summary>
		/// <param name="programName">Name of the program to run</param>
		/// <param name="args">Argument to pass to the program</param>
		/// <param name="env">Desired environment variables for the program</param>
		/// <param name="master">The file descriptor connected to the input and output of the child process</param>
		/// <param name="winSize">Desired window size</param>
		/// <returns></returns>
		public static int ForkAndExec (string programName, string [] args, string [] env, out int master, UnixWindowSize winSize)
		{
			if (HeavyDuty) {
				return HeavyFork (programName, args, env, out  master, winSize);
			} else {
				var pid = forkpty (out master, IntPtr.Zero, IntPtr.Zero, ref winSize);
				if (pid < 0)
					throw new Exception ("Could not create Pty");

				if (pid == 0) {
					execve (programName, args, env);
				}
				return pid;
			}
		}

		[DllImport ("libc", SetLastError = true)]
		extern static int ioctl (int fd, long cmd, ref UnixWindowSize WinSz);

		/// <summary>
		/// Sends a request to the pseudo terminal to set the size to the specified one
		/// </summary>
		/// <param name="fd">File descriptor returned by ForkPty</param>
		/// <param name="winSize">The desired window size</param>
		/// <returns></returns>
		public static int SetWinSize (int fd, ref UnixWindowSize winSize)
		{
			const long MAC_TIOCSWINSZ = 0x80087467;
			var r = ioctl (fd, MAC_TIOCSWINSZ, ref winSize);
			if (r == -1) {
				var lastErr = Marshal.GetLastWin32Error ();
				Console.WriteLine (lastErr);
			}
			return r;
		}

		[DllImport ("libc", SetLastError = true)]
		extern static int ioctl (int fd, long cmd, ref long size);

		/// <summary>
		/// Returns the number of bytes available for reading on a file descriptor
		/// </summary>
		/// <param name="fd"></param>
		/// <param name="size"></param>
		/// <returns></returns>
		public static int AvailableBytes (int fd, ref long size)
		{
			const long MAC_FIONREAD = 0x4004667f;
			var r = ioctl (fd, MAC_FIONREAD, ref size);
			if (r == -1) {
				var lastErr = Marshal.GetLastWin32Error ();
				Console.WriteLine (lastErr);
			}
			return r;
		}
	}
}
