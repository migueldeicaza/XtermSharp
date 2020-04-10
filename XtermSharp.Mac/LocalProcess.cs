using System;
using System.Runtime.InteropServices;
using CoreFoundation;

namespace XtermSharp.Mac {
	/// <summary>
	/// A Process class that communicates with a local process, typically a shell like `bash`
	/// </summary>
	public class LocalProcess : Process {
		readonly byte [] readBuffer = new byte [4 * 1024];
		UnixWindowSize initialSize;
		int shellFileDescriptor;

#if DEBUG
		static int debugFileIndex;
#endif

		/// <summary>
		/// Gets the id of the process
		/// </summary>
		public int ProcessId { get; private set; }

		/// <summary>
		/// This event is raised when there is an unexpected write failure to the process
		/// </summary>
		public event Action<int> OnProcessWriteFailure;

		/// <summary>
		/// Launches the shell
		/// </summary>
		public virtual void Start (string shellPath = "/bin/bash", string [] args = null, string [] env = null)
		{
			OnStart ();

			var shellArgs = args == null ? new string [1] : new string [args.Length + 1];
			shellArgs [0] = shellPath;
			args?.CopyTo (shellArgs, 1);

			ProcessId = Pty.ForkAndExec (shellPath, shellArgs, env ?? Terminal.GetEnvironmentVariables (), out shellFileDescriptor, initialSize);
			DispatchIO.Read (shellFileDescriptor, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		/// <summary>
		/// Notifies the subshell that the terminal emited some data, eg a response to device status
		/// </summary>
		public override void NotifyDataEmitted (string txt)
		{
			var data = System.Text.Encoding.UTF8.GetBytes (txt);
			DispatchIO.Write (shellFileDescriptor, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
		}

		/// <summary>
		/// Notifies the subshell that the size has changed
		/// </summary>
		public override void NotifySizeChanged (int newCols, int newRows, nfloat width, nfloat height)
		{
			UnixWindowSize newSize = new UnixWindowSize ();
			GetUnixWindowSize (newCols, newRows, width, height, ref newSize);

			if (IsRunning) {
				var res = Pty.SetWinSize (shellFileDescriptor, ref newSize);
				// TODO: log result of SetWinSize if != 0
			} else {
				initialSize = newSize;
			}
		}

		/// <summary>
		/// Notfies the subshell that the user entered some data
		/// </summary>
		public override void NotifyUserInput (byte [] data)
		{
			if (!IsRunning)
				return;

			DispatchIO.Write (shellFileDescriptor, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
		}

		/// <summary>
		/// Ges the unix window size from the given dimensions
		/// </summary>
		protected static void GetUnixWindowSize (int cols, int rows, nfloat width, nfloat height, ref UnixWindowSize size)
		{
			size = new UnixWindowSize () {
				col = (short)cols,
				row = (short)rows,
				xpixel = (short)width,
				ypixel = (short)height
			};
		}

		/// <summary>
		/// Reads data from the child process
		/// </summary>
		void ChildProcessRead (DispatchData data, int error)
		{
			using (var map = data.CreateMap (out var buffer, out var size)) {
				// Faster, but harder to debug:
				// terminalView.Feed (buffer, (int) size);
				if (size == 0) {
					OnStop ();
					return;
				}

				byte [] copy = new byte [(int)size];
				Marshal.Copy (buffer, copy, 0, (int)size);

#if DEBUG_LOG_FILE
				System.IO.File.WriteAllBytes ("/tmp/log-" + (debugFileIndex++), copy);
#endif
				SendOnData (copy);
			}

			DispatchIO.Read (shellFileDescriptor, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		void ChildProcessWrite (DispatchData left, int error)
		{
			if (error != 0) {
				OnProcessWriteFailure?.Invoke (error);
				// TODO: log
				//throw new Exception ("Error writing data to child");
			}
		}

	}
}
