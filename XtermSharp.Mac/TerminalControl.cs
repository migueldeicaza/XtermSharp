using System;
using System.Runtime.InteropServices;
using AppKit;
using CoreFoundation;
using CoreGraphics;

namespace XtermSharp.Mac {
	/// <summary>
	/// A view that contains a TerminalView and Scroller that handles user input
	/// </summary>
	public class TerminalControl : NSView {
		TerminalView terminalView;

		int shellPid;
		int shellFileDescriptor;
		bool running;
		readonly byte [] readBuffer = new byte [4 * 1024];

#if DEBUG
		static int x;
#endif
		public TerminalControl (CGRect rect) : base (rect)
		{
			Build (rect);
		}

		public string WelcomeText { get; set; } = "Welcome to XtermSharp!";

		public string ExitText { get; set; } = string.Empty;

		public int ShellProcessId {
			get	{
				return shellPid;
			}
		}

		/// <summary>
		/// Raised when the title of the terminal has changed
		/// </summary>
		public event Action<string> TitleChanged;

		/// <summary>
		/// Raised when the title of the shell process exits
		/// </summary>
		public event Action ShellExited;

		/// <summary>
		/// Launches the shell
		/// </summary>
		public void StartShell(string shellPath = "/bin/bash", string [] args = null, string [] env = null)
		{
			// TODO: throw error if already started
			terminalView.Feed (WelcomeText + "\n");

			var size = new UnixWindowSize ();
			GetUnixWindowSize (terminalView.Frame, terminalView.Terminal.Rows, terminalView.Terminal.Cols, ref size);

			var shellArgs = args == null ? new string [1] : new string [args.Length + 1];
			shellArgs [0] = shellPath;
			args?.CopyTo (shellArgs, 1);

			shellPid = Pty.ForkAndExec (shellPath, shellArgs, env ?? Terminal.GetEnvironmentVariables (), out shellFileDescriptor, size);
			running = true;
			DispatchIO.Read (shellFileDescriptor, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		public override void Layout ()
		{
			terminalView.Frame = new CGRect (0, 0, Frame.Width, Frame.Height);
		}

		static void GetUnixWindowSize (CGRect frame, int rows, int cols, ref UnixWindowSize size)
		{
			size = new UnixWindowSize () {
				col = (short)cols,
				row = (short)rows,
				xpixel = (short)frame.Width,
				ypixel = (short)frame.Height
			};
		}

		/// <summary>
		/// Sets up the view and sets event handlers
		/// </summary>
		void Build (CGRect rect)
		{
			var terminalFrame = new CGRect (0, 0, rect.Width, rect.Height);

			terminalView = new TerminalView (terminalFrame);
			var t = terminalView.Terminal;

			terminalView.UserInput = HandleUserInput;
			terminalView.SizeChanged += HandleSizeChanged;
			terminalView.TitleChanged += (TerminalView sender, string title) => {
				TitleChanged?.Invoke (title);
			};

			AddSubview (terminalView);

			t.DataEmitted += HandleTerminalDataEmitted;
		}

		void HandleUserInput (byte [] data)
		{
			if (!running)
				return;

			DispatchIO.Write (shellFileDescriptor, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
		}

		void HandleSizeChanged (int newCols, int newRows)
		{
			UnixWindowSize newSize = new UnixWindowSize ();
			GetUnixWindowSize (terminalView.Frame, terminalView.Terminal.Rows, terminalView.Terminal.Cols, ref newSize);
			var res = Pty.SetWinSize (shellFileDescriptor, ref newSize);
			// TODO: log result of SetWinSize if != 0
		}

		void HandleTerminalDataEmitted (Terminal terminal, string txt)
		{
			var data = System.Text.Encoding.UTF8.GetBytes (txt);
			DispatchIO.Write (shellFileDescriptor, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
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
					running = false;
					if (!string.IsNullOrEmpty(ExitText))
						terminalView.Terminal.Feed (ExitText);

					ShellExited?.Invoke ();
					return;
				}
				byte [] copy = new byte [(int)size];
				Marshal.Copy (buffer, copy, 0, (int)size);

#if DEBUG
				System.IO.File.WriteAllBytes ("/tmp/log-" + (x++), copy);
#endif
				terminalView.Feed (copy);
			}

			DispatchIO.Read (shellFileDescriptor, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		void ChildProcessWrite (DispatchData left, int error)
		{
			if (error != 0) {
				// TODO: log
				throw new Exception ("Error writing data to child");
			}
		}
	}
}
