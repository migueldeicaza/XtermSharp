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
		NSScroller scroller;

		int shellPid;
		int shellFileDescriptor;
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
		public void StartShell(string shellPath = "/bin/bash")
		{
			// TODO: throw error if already started
			terminalView.Feed (WelcomeText + "\n");

			var size = new UnixWindowSize ();
			GetUnixWindowSize (terminalView.Frame, terminalView.Terminal.Rows, terminalView.Terminal.Cols, ref size);

			shellPid = Pty.ForkAndExec (shellPath, new string [] { shellPath }, Terminal.GetEnvironmentVariables (), out shellFileDescriptor, size);
			DispatchIO.Read (shellFileDescriptor, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		public override void Layout ()
		{
			var viewFrame = Frame;

			var scrollWidth = NSScroller.ScrollerWidthForControlSize (NSControlSize.Regular);
			var scrollFrame = new CGRect (viewFrame.Right - scrollWidth, viewFrame.Y, scrollWidth, viewFrame.Height);
			viewFrame = new CGRect (viewFrame.X, viewFrame.Y, viewFrame.Width - scrollWidth, viewFrame.Height);

			scroller.Frame = scrollFrame;
			terminalView.Frame = viewFrame;
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
			var scrollWidth = NSScroller.ScrollerWidthForControlSize (NSControlSize.Regular);
			var scrollFrame = new CGRect (rect.Right - scrollWidth, rect.Y, scrollWidth, rect.Height);
			scroller = new NSScroller (scrollFrame);
			scroller.ScrollerStyle = NSScrollerStyle.Legacy;
			scroller.DoubleValue = 0.0;
			scroller.KnobProportion = 0.1f;
			scroller.Enabled = false;
			AddSubview (scroller);

			scroller.Activated += ScrollerActivated;

			var terminalFrame = new CGRect (rect.X, rect.Y, rect.Width - scrollWidth, rect.Height);

			terminalView = new TerminalView (terminalFrame);
			var t = terminalView.Terminal;

			terminalView.UserInput = HandleUserInput;
			terminalView.SizeChanged += HandleSizeChanged;
			terminalView.TerminalScrolled += HandleTerminalScrolled;
			terminalView.CanScrollChanged += HandleTerminalCanScrollChanged;
			terminalView.TitleChanged += (TerminalView sender, string title) => {
				TitleChanged?.Invoke (title);
			};

			AddSubview (terminalView);
		}

		void HandleTerminalScrolled (double scrollPosition)
		{
			UpdateScroller ();
		}

		private void HandleTerminalCanScrollChanged (bool obj)
		{
			UpdateScroller ();
		}

		void ScrollerActivated (object sender, EventArgs e)
		{
			switch (scroller.HitPart) {
			case NSScrollerPart.DecrementPage:
				terminalView.PageUp ();
				scroller.DoubleValue = terminalView.ScrollPosition;
				break;
			case NSScrollerPart.IncrementPage:
				terminalView.PageDown ();
				scroller.DoubleValue = terminalView.ScrollPosition;
				break;
			case NSScrollerPart.Knob:
				terminalView.ScrollToPosition (scroller.DoubleValue);
				break;
			}
		}

		void HandleUserInput (byte [] data)
		{
			DispatchIO.Write (shellFileDescriptor, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
		}

		void HandleSizeChanged (int newCols, int newRows)
		{
			UnixWindowSize newSize = new UnixWindowSize ();
			GetUnixWindowSize (terminalView.Frame, terminalView.Terminal.Rows, terminalView.Terminal.Cols, ref newSize);
			var res = Pty.SetWinSize (shellFileDescriptor, ref newSize);
			// TODO: log result of SetWinSize if != 0

			UpdateScroller ();
		}

		void UpdateScroller()
		{
			var shouldBeEnabled = !terminalView.Terminal.Buffers.IsAlternateBuffer;
			shouldBeEnabled = shouldBeEnabled && terminalView.Terminal.Buffer.HasScrollback;
			shouldBeEnabled = shouldBeEnabled && terminalView.Terminal.Buffer.Lines.Length > terminalView.Terminal.Rows;
			scroller.Enabled = shouldBeEnabled;

			scroller.DoubleValue = terminalView.ScrollPosition;
			scroller.KnobProportion = terminalView.ScrollThumbsize;
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
