using System;
using Terminal.Gui;
using XtermSharp;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GuiCsHost {
	public class TerminalView : View, ITerminalDelegate {
		internal XtermSharp.Terminal terminal;
		bool cursesDriver = Application.Driver.GetType ().Name.IndexOf ("CursesDriver") != -1;
		bool terminalSupportsUtf8;

		public TerminalView ()
		{
			terminal = new XtermSharp.Terminal (this, new TerminalOptions () { Cols = 80, Rows = 25 });
			CanFocus = true;

			if (cursesDriver)
				terminalSupportsUtf8 = Environment.GetEnvironmentVariable ("LANG").IndexOf ("UTF-8", StringComparison.OrdinalIgnoreCase) != -1;
			else
				terminalSupportsUtf8 = true;
		}
		
		public override Rect Frame {
			get => base.Frame;
			set {
				base.Frame = value;
				SetNeedsDisplay ();

				if (value.Width != terminal.Cols || value.Height != terminal.Rows) {
					terminal.Resize (value.Width, value.Height);
				}

				TerminalSizeChanged?.Invoke (value.Width, value.Height);
			}
		}

		/// <summary>
		///  This event is raised when the terminal size has change, due to a Gui.CS frame changed.
		/// </summary>
		public event Action<int, int> TerminalSizeChanged;

		public override bool ProcessKey (KeyEvent keyEvent)
		{
			switch (keyEvent.Key) {
			case Key.Esc:
				Send (0x1b);
				break;
			case Key.Space:
				Send (0x20);
				break;
			case Key.DeleteChar:
				Send (EscapeSequences.CmdDelKey);
				break;
			case Key.Backspace:
				Send (0x7f);
				break;
			case Key.CursorUp:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveUpApp : EscapeSequences.MoveUpNormal);
				break;
			case Key.CursorDown:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveDownApp : EscapeSequences.MoveDownNormal);
				break;
			case Key.CursorLeft:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveLeftApp : EscapeSequences.MoveLeftNormal);
				break;
			case Key.CursorRight:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveRightApp : EscapeSequences.MoveRightNormal);
				break;
			case Key.PageUp:
				if (terminal.ApplicationCursor)
					Send (EscapeSequences.CmdPageUp);
				else {
					// TODO: view should scroll one page up.
				}
				break;
			case Key.PageDown:
				if (terminal.ApplicationCursor)
					Send (EscapeSequences.CmdPageDown);
				else {
					// TODO: view should scroll one page down
				}
				break;
			case Key.Home:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveHomeApp : EscapeSequences.MoveHomeNormal);
				break;
			case Key.End:
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveEndApp : EscapeSequences.MoveEndNormal);
				break;
			case Key.InsertChar:
				break;
			case Key.F1:
				Send (EscapeSequences.CmdF [0]);
				break;
			case Key.F2:
				Send (EscapeSequences.CmdF [1]);
				break;
			case Key.F3:
				Send (EscapeSequences.CmdF [2]);
				break;
			case Key.F4:
				Send (EscapeSequences.CmdF [3]);
				break;
			case Key.F5:
				Send (EscapeSequences.CmdF [4]);
				break;
			case Key.F6:
				Send (EscapeSequences.CmdF [5]);
				break;
			case Key.F7:
				Send (EscapeSequences.CmdF [6]);
				break;
			case Key.F8:
				Send (EscapeSequences.CmdF [7]);
				break;
			case Key.F9:
				Send (EscapeSequences.CmdF [8]);
				break;
			case Key.F10:
				Send (EscapeSequences.CmdF [9]);
				break;
			case Key.BackTab:
				Send (EscapeSequences.CmdBackTab);
				break;
			default:
				if (keyEvent.Key >= Key.ControlA && keyEvent.Key <= Key.ControlZ) {
					Send ((byte)keyEvent.Key);
					break;
				}
				if (keyEvent.IsAlt) {
					Send (0x1b);
				}
				var rune = (Rune)(uint)keyEvent.Key;
				var len = Rune.RuneLen (rune);
				if (len > 0) {
					var buff = new byte [len];
					var n = Rune.EncodeRune (rune, buff);
					Send (buff);
				} else {
					Send ((byte)keyEvent.Key);
				}
				break;
			}
			return true;
		}

		void SetAttribute (int attribute)
		{
			int bg = attribute & 0x1ff;
			int fg = (attribute >> 9) & 0x1ff;
			var flags = (FLAGS)(attribute >> 18);

			if (cursesDriver) {
				var cattr = 0;
				if (fg == Renderer.DefaultColor && bg == Renderer.DefaultColor)
					cattr = 0;
				else {
					if (fg == Renderer.DefaultColor)
						fg = (short)ConsoleColor.Gray;
					if (bg == Renderer.DefaultColor)
						bg = (short)ConsoleColor.Black;

					Driver.SetColors ((ConsoleColor)fg, (ConsoleColor)bg);
					return;
				}

				if (flags.HasFlag (FLAGS.BOLD))
					cattr |= 0x200000; // A_BOLD
				if (flags.HasFlag (FLAGS.INVERSE))
					cattr |= 0x40000; // A_REVERSE
				if (flags.HasFlag (FLAGS.BLINK))
					cattr |= 0x80000; // A_BLINK
				if (flags.HasFlag (FLAGS.DIM))
					cattr |= 0x100000; // A_DIM
				if (flags.HasFlag (FLAGS.UNDERLINE))
					cattr |= 0x20000; // A_UNDERLINE
				if (flags.HasFlag (FLAGS.ITALIC))
					cattr |= 0x10000; // A_STANDOUT
				Driver.SetAttribute (new Terminal.Gui.Attribute (cattr));
			} else {
				Driver.SetAttribute (ColorScheme.Normal);
			}
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
				var line = terminal.Buffer.Lines [row+yDisp];
				for (int col = 0; col < maxCol; col++) {
					var ch = line [col];
					SetAttribute (ch.Attribute);
					Rune r;

					if (ch.Code == 0)
						r = ' ';
					else {
						if (terminalSupportsUtf8)
							r = ch.Rune;
						else {
							switch (ch.Code) {
							case 0:
								r = ' ';
								break;
							case 0x2518: // '┘'
								r = 0x40006a; // ACS_LRCORNER;
								break;
							case 0x2510: // '┐'
								r = 0x40006b; // ACS_URCORNER;
								break;
							case 0x250c: // '┌'
								r = 0x40006c; // ACS_ULCORNER;
								break;
							case 0x2514: // '└'
								r = 0x40006d; // ACS_LLCORNER;
								break;
							case 0x253c: // '┼'
								r = 0x40006e; // ACS_PLUS;
								break;
							case 0x23ba: // '⎺'
							case 0x23bb: // '⎻'
							case 0x2500: // '─'
							case 0x23bc: // '⎼'
							case 0x23bd: // '⎽'
								r = 0x400071; // ACS_VLINE
								break;
							case 0x251c: // '├'
								r = 0x400074; // ACS_LTEE
								break;
							case 0x2524: // '┤'
								r = 0x400075; // ACS_RTEE
								break;
							case 0x2534: // '┴'
								r = 0x400076; // ACS_BTEE
								break;
							case 0x252c: // '┬'
								r = 0x400077; // ACS_TTEE
								break;
							case 0x2502: // '│'
								r = 0x400078; // ACS_VLINE
								break;
							default:
								r = ch.Rune;
								break;
							}
						}
					}
					AddRune (col, row, r);
				}
			}
			PositionCursor ();
		}

		public override bool MouseEvent (MouseEvent mouseEvent)
		{
			if (terminal.MouseEvents) {
				var f = mouseEvent.Flags;
				int button = -1;
				if (f.HasFlag (MouseFlags.Button1Clicked))
					button = 0;
				if (f.HasFlag (MouseFlags.Button2Clicked))
					button = 1;
				if (f.HasFlag (MouseFlags.Button3Clicked))
					button = 2;

				if  (button != -1){
					var e = terminal.EncodeButton (button, release: false, shift: false, meta: false, control: false);
					terminal.SendEvent (e, mouseEvent.X, mouseEvent.Y);
					if (terminal.MouseSendsRelease) {
						e = terminal.EncodeButton (button, release: true, shift: false, meta: false, control: false);
						terminal.SendEvent (e, mouseEvent.X, mouseEvent.Y);
					}
					return true;
				}
			} else {
				// Not currently handled

			}
			return false;
		}

		public Action<byte []> UserInput;

		byte [] miniBuf = new byte [1];

		void Send (byte b)
		{
			miniBuf [0] = b;
			Send (miniBuf);
		}

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

		public override void PositionCursor ()
		{
			Move (terminal.Buffer.X, terminal.Buffer.Y);
		}

		bool UpdateDisplay (Mono.Terminal.MainLoop mainloop)
		{
			terminal.GetUpdateRange (out var rowStart, out var rowEnd);
			terminal.ClearUpdateRange ();
			var cols = terminal.Cols;
			var tb = terminal.Buffer;
			SetNeedsDisplay (new Rect (0, rowStart, Frame.Width, rowEnd+1));
			//SetNeedsDisplay ();
			pendingDisplay = false;
			return false;
		}

		bool pendingDisplay;
		void QueuePendingDisplay ()
		{
			// throttle
			if (!pendingDisplay) {
				pendingDisplay = true;
				Application.MainLoop.AddTimeout (TimeSpan.FromMilliseconds (1), UpdateDisplay);
			}
		}

		public void Feed (byte [] buffer, int n)
		{
			terminal.Feed (buffer, n);
			QueuePendingDisplay ();
		}
	}

	public class SubprocessTerminalView : TerminalView {
		int ptyFd;
		int childPid;
		
		void SendDataToChild (byte [] data)
		{
			unsafe {
				fixed (byte* p = &data [0]) {
					var n = Mono.Unix.Native.Syscall.write (ptyFd, (void*)((IntPtr)p), (ulong)data.Length);
				}
			}
		}

		void NotifyPtySizeChanged (int cols, int rows)
		{
			UnixWindowSize nz = new UnixWindowSize ();
			nz.col = (short) Frame.Width;
			nz.row = (short) Frame.Height;
			var res = Pty.SetWinSize (ptyFd, ref nz);
		}

		public SubprocessTerminalView ()
		{
			var size = new UnixWindowSize () {
				col = (short) terminal.Cols,
				row = (short) terminal.Rows,
			};

			childPid  = Pty.ForkAndExec ("/bin/bash", new string [] { "/bin/bash" }, XtermSharp.Terminal.GetEnvironmentVariables ("xterm"), out ptyFd, size);
			var unixMainLoop = Application.MainLoop.Driver as Mono.Terminal.UnixMainLoop;
			unixMainLoop.AddWatch (ptyFd, Mono.Terminal.UnixMainLoop.Condition.PollIn, PtyReady);
	
			this.UserInput = SendDataToChild;
			this.TerminalSizeChanged += NotifyPtySizeChanged;
		}

		byte [] buffer = new byte [8192];
		bool PtyReady (Mono.Terminal.MainLoop mainloop)
		{
			unsafe {
				long n;
				fixed (byte* p = &buffer[0]) {

					n = Mono.Unix.Native.Syscall.read (ptyFd, (void*)((IntPtr)p), (ulong)buffer.Length);
					Debug.Print(System.Text.Encoding.UTF8.GetString (buffer, 0, (int) n));
					Feed (buffer, (int)n);
				}
			}
			return true;
		}
	}
}
