using System;
using Terminal.Gui;
using XtermSharp;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GuiCsHost {
	public class TerminalView : View, ITerminalDelegate {
		internal XtermSharp.Terminal terminal;

		public TerminalView ()
		{
			terminal = new XtermSharp.Terminal (this, new TerminalOptions () { Cols = 80, Rows = 25 });
		}
		
		public override Rect Frame {
			get => base.Frame;
			set {
				base.Frame = value;
				SetNeedsDisplay ();
			}
		}

		public override bool ProcessKey (KeyEvent keyEvent)
		{
			return base.ProcessKey (keyEvent);
		}

		public override void Redraw (Rect region)
		{
			Driver.SetAttribute (ColorScheme.Normal);
			Clear ();

			var maxCol = Frame.Width;
			var maxRow = Frame.Height;
			var yDisp = terminal.Buffer.YDisp;
			
			for (int row = 0; row < maxRow; row++) {
				Move (Frame.X, Frame.Y + row);
				if (row >= terminal.Rows)
					continue;
				var line = terminal.Buffer.Lines [row];
				for (int col = 0; col < maxCol; col++) {
					var ch = line [col];
					var r = ch.Code == 0 ? ' ' : ch.Rune;
					AddRune (col, row, r);
					
					if (r == ' ')
						continue;
					Debug.Print("" + (char)r);
				}
			}
		}

		public Action<byte []> UserInput;

		public void Send (byte [] data)
		{
			UserInput?.Invoke (data);
		}

		public void SetTerminalTitle (XtermSharp.Terminal source, string title)
		{
			//
		}

		public void ShowCursor (XtermSharp.Terminal source)
		{
			//
		}

		public void SizeChanged (XtermSharp.Terminal source)
		{
			// Triggered by the terminal
		}
	}

	public class SubprocessTerminalView : TerminalView {
		int ptyFd;
		int childPid;

		public SubprocessTerminalView ()
		{
			var size = new MacWinSize () {
				col = (short) terminal.Cols,
				row = (short) terminal.Rows,
			};

			childPid  = Pty.Fork ("/bin/bash", new string [] { "/bin/bash" }, XtermSharp.Terminal.GetEnvironmentVariables (), out ptyFd, size);
			new Thread (IOLoop).Start (SynchronizationContext.Current);
		}

		void IOLoop (object arg)
		{
			var context = arg as SynchronizationContext;
			var buffer = new byte [8192];

			unsafe {
				IntPtr n;
				fixed (byte* p = &buffer[0]) {
					
					while ((n = Mono.Posix.Syscall.read (ptyFd, (void *)((IntPtr)p), (IntPtr) buffer.Length)) != IntPtr.Zero) {
						Debug.Print(System.Text.Encoding.UTF8.GetString (buffer, 0, (int) n));
						context.Send ((x) => {
							terminal.Feed (buffer, (int) n);
						}, null);
					}
				}
			}
		}
	}
}
