using System;
using AppKit;
using CoreGraphics;

namespace XtermSharp.Mac {
	/// <summary>
	/// A view that contains a TerminalView and connects to a local process
	/// </summary>
	public class LocalProcessTerminalView : NSView {
		TerminalView terminalView;
		LocalProcess process;

		public LocalProcessTerminalView (CGRect rect) : base (rect)
		{
			Build ();
		}

		public string WelcomeText { get; set; } = "Welcome to XtermSharp!";

		public string ExitText { get; set; } = string.Empty;

		/// <summary>
		/// Gets the process that is connected to this view
		/// </summary>
		public LocalProcess Process {
			get {
				return process;
			}

			set {
				if (value == null)
					throw new ArgumentNullException (nameof (Process));

				if (process == value) {
					return;
				}

				if (process != null) {
					if (process.IsRunning)
						throw new InvalidOperationException ("cannot change process while it is running");

					process.OnStarted -= ProcessOnStarted;
					process.OnExited -= ProcessOnExited;
					process.OnData -= ProcessOnData;
				}

				process = value;
				process.OnStarted += ProcessOnStarted;
				process.OnExited += ProcessOnExited;
				process.OnData += ProcessOnData;
				process.NotifySizeChanged (terminalView.Terminal.Cols, terminalView.Terminal.Rows, terminalView.Frame.Width, terminalView.Frame.Height);
			}
		}

		/// <summary>
		/// Raised when the title of the terminal has changed
		/// </summary>
		public event Action<string> TitleChanged;

		public override void Layout ()
		{
			terminalView.Frame = Bounds;
		}

		/// <summary>
		/// Sets up the view and sets event handlers
		/// </summary>
		void Build ()
		{
			var terminalFrame = Bounds;

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

		void HandleTerminalDataEmitted (Terminal terminal, string txt)
		{
			process?.NotifyDataEmitted (txt);
		}

		void HandleSizeChanged (int cols, int rows, nfloat width, nfloat height)
		{
			process?.NotifySizeChanged (cols, rows, width, height);
		}

		void HandleUserInput (byte [] data)
		{
			process?.NotifyUserInput (data);
		}

		void ProcessOnStarted ()
		{
			if (!string.IsNullOrEmpty (WelcomeText))
				terminalView.Feed (WelcomeText + "\n");
		}

		void ProcessOnExited ()
		{
			if (!string.IsNullOrEmpty (ExitText))
				terminalView.Terminal.Feed (ExitText);
		}

		void ProcessOnData (byte [] data)
		{
			terminalView.Feed (data);
		}
	}
}
