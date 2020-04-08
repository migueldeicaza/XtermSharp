using System;
using System.Collections.Generic;
using System.Text;

namespace XtermSharp {

	public class Terminal {
		readonly ITerminalDelegate terminalDelegate;
		readonly ControlCodes controlCodes;
		readonly List<string> titleStack;
		readonly List<string> iconTitleStack;
		readonly BufferSet buffers;
		readonly InputHandler input;

		const int MINIMUM_COLS = 2;
		const int MINIMUM_ROWS = 1;

		// modes
		bool insertMode;
		bool bracketedPasteMode;

		bool applicationKeypad, applicationCursor;
		bool cursorHidden;
		Dictionary<byte, string> charset;
		int gcharset;


		public Terminal (ITerminalDelegate terminalDelegate = null, TerminalOptions options = null)
		{
			this.terminalDelegate = terminalDelegate ?? new SimpleTerminalDelegate ();
			controlCodes = new ControlCodes () { Send8bit = false };
			titleStack = new List<string> ();
			iconTitleStack = new List<string> ();
			input = new InputHandler (this);

			Options = options ?? new TerminalOptions ();
			Cols = Math.Max (Options.Cols, MINIMUM_COLS);
			Rows = Math.Max (Options.Rows, MINIMUM_ROWS);

			buffers = new BufferSet (this);

			Setup ();
		}

		/// <summary>
		/// Gets the delegate for the terminal
		/// </summary>
		public ITerminalDelegate Delegate => terminalDelegate;

		/// <summary>
		/// Gets the control codes for the terminal
		/// </summary>
		public ControlCodes ControlCodes => controlCodes;

		/// <summary>
		/// Gets the current title of the terminal
		/// </summary>
		public string Title { get; private set; }

		/// <summary>
		/// Gets the current icon title of the terminal
		/// </summary>
		public string IconTitle { get; private set; }

		/// <summary>
		/// Gets the currently active buffer
		/// </summary>
		public Buffer Buffer => buffers.Active;

		/// <summary>
		/// Gets the BufferSet for the terminal
		/// </summary>
		public BufferSet Buffers => buffers;

		/// <summary>
		/// Gets or sets the margin mode of the terminal
		/// </summary>
		public bool MarginMode { get; internal set; }

		/// <summary>
		/// Gets or sets the origin mode of the terminal
		/// </summary>
		public bool OriginMode { get; internal set; }

		/// <summary>
		/// Gets or sets the origin mode of the terminal
		/// </summary>
		public bool Wraparound { get; internal set; }

		/// <summary>
		/// Gets or sets the origin mode of the terminal
		/// </summary>
		public bool ReverseWraparound { get; internal set; }

		/// <summary>
		/// Gets or sets the current mouse mode
		/// </summary>
		public MouseMode MouseMode { get; internal set; }

		/// <summary>
		/// Gets or sets the current mouse protocol
		/// </summary>
		public MouseProtocolEncoding MouseProtocol { get; internal set; }

		/// <summary>
		/// Called by input handlers to set the title
		/// </summary>
		internal void SetTitle (string text)
		{
			Title = text;
			terminalDelegate.SetTerminalTitle (this, text);
		}

		/// <summary>
		/// Called by input handlers to push the current title onto the stack
		/// </summary>
		internal void PushTitle ()
		{
			titleStack.Insert (0, Title);
		}

		/// <summary>
		/// Called by input handlers to pop and set the title to the last one on the stack
		/// </summary>
		internal void PopTitle ()
		{
			if (titleStack.Count > 0) {
				Title = titleStack[0];
				titleStack.RemoveAt (0);
			}

			terminalDelegate.SetTerminalTitle (this, Title);
		}

		/// <summary>
		/// Called by input handlers to set the icon title
		/// </summary>
		internal void SetIconTitle (string text)
		{
			IconTitle = text;
			terminalDelegate.SetTerminalIconTitle (this, text);
		}

		/// <summary>
		/// Called by input handlers to push the current icon title onto the stack
		/// </summary>
		internal void PushIconTitle ()
		{
			iconTitleStack.Insert (0, IconTitle);
		}

		/// <summary>
		/// Called by input handlers to pop and set the icon title to the last one on the stack
		/// </summary>
		internal void PopIconTitle ()
		{
			if (iconTitleStack.Count > 0) {
				IconTitle = iconTitleStack [0];
				iconTitleStack.RemoveAt (0);
			}

			terminalDelegate.SetTerminalIconTitle (this, IconTitle);
		}

		/// <summary>
		/// Sends a response to a command
		/// </summary>
		public void SendResponse (string txt)
		{
			terminalDelegate.Send (Encoding.UTF8.GetBytes (txt));
		}

		/// <summary>
		/// Sends a response to a command
		/// </summary>
		public void SendResponse (params object[] args)
		{
			if (args == null) {
				return;
			}

			int len = args.Length;
			for (int i = 0; i < args.Length; i++) {
				if (args [i] is string s) {
					len += s != null ? s.Length - 1: 0;
				} else if (args [i] is byte [] ba) {
					len += ba.Length - 1;
				}
			}

			var buffer = new byte [len];

			int bufferIndex = 0;
			for (int i = 0; i < args.Length; i++) {
				if (args[i] == null) {
					buffer [bufferIndex] = 0;
				} else if (args[i] is byte b) {
					buffer [bufferIndex] = b;
				} else if (args[i] is string s) {
					if (s == null) {
						buffer [bufferIndex] = 0;
					} else {
						foreach (var sb in Encoding.UTF8.GetBytes (s)) {
							buffer [bufferIndex++] = sb;
						}
					}
					bufferIndex--;
				} else if (args[i] is byte[] ba) {
					foreach (var bab in ba) {
						buffer [bufferIndex++] = bab;
					}
					bufferIndex--;
				} else {
					Error ("Unsupported type in SendResponse", args[i].GetType());
				}

				bufferIndex++;
			}

			terminalDelegate.Send (buffer);
		}

		public void Error (string txt, params object [] args)
		{
			Report ("ERROR", txt, args);
		}

		public bool Debug { get; set; }
		public void Log (string text, params object [] args)
		{
			Report ("LOG", text, args);
		}

		void Report (string prefix, string text, object [] args)
		{
			Console.WriteLine ($"{prefix}: {text}");
			for (int i = 0; i < args.Length; i++)
				Console.WriteLine ("    {0}: {1}", i, args [i]);

		}

		public void Feed (byte [] data, int len = -1)
		{
			input.Parse (data, len);
		}

		public void Feed (IntPtr data, int len = -1)
		{
			input.Parse (data, len);
		}

		public void Feed (string text)
		{
			var bytes = Encoding.UTF8.GetBytes (text);
			Feed (bytes, bytes.Length);
		}

		public Dictionary<byte, string> Charset {
			get => charset;
			set {
				charset = value;
			}
		}

		public bool ApplicationCursor { get; internal set; }
		public int SavedCols { get; internal set; }
		public bool ApplicationKeypad { get; internal set; }

		public bool SendFocus { get; internal set; }

		public bool CursorHidden { get; internal set; }
		public bool BracketedPasteMode { get; internal set; }

		public TerminalOptions Options { get; private set; }
		public int Cols { get; private set; }
		public int Rows { get; private set; }
		public bool InsertMode;
		public int CurAttr;
		int gLevel;
		int refreshStart = Int32.MaxValue;
		int refreshEnd = -1;
		bool userScrolling;

		internal void UpdateRange (int y)
		{
			if (y < 0)
				throw new ArgumentException ();

			if (y < refreshStart)
				refreshStart = y;
			if (y > refreshEnd)
				refreshEnd = y;
		}

		public void GetUpdateRange (out int startY, out int endY)
		{
			startY = refreshStart;
			endY = refreshEnd;
		}

		public void ClearUpdateRange ()
		{
			refreshStart = Int32.MaxValue;
			refreshEnd = -1;
		}

		internal void EmitChar (int ch)
		{
			// For accessibility purposes 'a11y.char' in the original source.
		}

		//
		// ESC c Full Reset (RIS)
		//
		internal void Reset ()
		{
			Options.Rows = Rows;
			Options.Cols = Cols;

			var savedCursorHidden = cursorHidden;
			Setup ();
			cursorHidden = savedCursorHidden;
			Refresh (0, Rows - 1);
			SyncScrollArea ();
		}

		// 
		// ESC D Index (Index is 0x84)
		//
		internal void Index ()
		{
			var buffer = Buffer;
			var newY = buffer.Y + 1;
			if (newY > buffer.ScrollBottom) {
				Scroll ();
			} else {
				buffer.Y = newY;
			}
			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X > Cols)
				buffer.X--;
		}

		BufferLine blankLine;
		internal void Scroll (bool isWrapped = false)
		{
			var buffer = Buffer;
			BufferLine newLine = blankLine;
			if (newLine == null || newLine.Length != Cols || newLine [0].Attribute != EraseAttr ()) {
				newLine = buffer.GetBlankLine (EraseAttr (), isWrapped);
				blankLine = newLine;
			}
			newLine.IsWrapped = isWrapped;

			var topRow = buffer.YBase + buffer.ScrollTop;
			var bottomRow = buffer.YBase + buffer.ScrollBottom;

			if (buffer.ScrollTop == 0) {
				// Determine whether the buffer is going to be trimmed after insertion.
				var willBufferBeTrimmed = buffer.Lines.IsFull;

				// Insert the line using the fastest method
				if (bottomRow == buffer.Lines.Length - 1) {
					if (willBufferBeTrimmed) {
						buffer.Lines.Recycle ().CopyFrom (newLine);
					} else {
						buffer.Lines.Push (new BufferLine (newLine));
					}
				} else {
					buffer.Lines.Splice (bottomRow + 1, 0, new BufferLine (newLine));
				}

				// Only adjust ybase and ydisp when the buffer is not trimmed
				if (!willBufferBeTrimmed) {
					buffer.YBase++;
					// Only scroll the ydisp with ybase if the user has not scrolled up
					if (!userScrolling) {
						buffer.YDisp++;
					}
				} else {
					// When the buffer is full and the user has scrolled up, keep the text
					// stable unless ydisp is right at the top
					if (userScrolling) {
						buffer.YDisp = Math.Max (buffer.YDisp - 1, 0);
					}
				}
			} else {
				// scrollTop is non-zero which means no line will be going to the
				// scrollback, instead we can just shift them in-place.
				var scrollRegionHeight = bottomRow - topRow + 1/*as it's zero-based*/;

				if (scrollRegionHeight > 1) {
					buffer.Lines.ShiftElements (topRow + 1, scrollRegionHeight - 1, -1);
				}

				buffer.Lines [bottomRow] = new BufferLine (newLine);
			}

			// Move the viewport to the bottom of the buffer unless the user is
			// scrolling.
			if (!userScrolling) {
				buffer.YDisp = buffer.YBase;
			}

			// Flag rows that need updating
			UpdateRange (buffer.ScrollTop);
			UpdateRange (buffer.ScrollBottom);

			/**
			 * This event is emitted whenever the terminal is scrolled.
			 * The one parameter passed is the new y display position.
			 *
			 * @event scroll
			 */
			Scrolled?.Invoke (this, buffer.YDisp);
		}

		/// <summary>
		/// Scroll the display of the terminal
		/// </summary>
		/// <param name="disp">The number of lines to scroll down (negative scroll up)</param>
		/// <param name="suppressScrollEvent">Don't emit the scroll event as scrollLines. This is use to avoid unwanted
		/// events being handled by the viewport when the event was triggered from the viewport originally.</param>
		public void ScrollLines(int disp, bool suppressScrollEvent = false)
		{
			if (disp < 0) {
				if (Buffer.YDisp == 0) {
					return;
				}

				this.userScrolling = true;
			} else if (disp + Buffer.YDisp >= Buffer.YBase) {
				this.userScrolling = false;
			}

			int oldYdisp = Buffer.YDisp;
			Buffer.YDisp = Math.Max (Math.Min (Buffer.YDisp + disp, Buffer.YBase), 0);

			// No change occurred, don't trigger scroll/refresh
			if (oldYdisp == Buffer.YDisp) {
				return;
			}

			if (!suppressScrollEvent) {
				Scrolled?.Invoke (this, Buffer.YDisp);
			}

			Refresh (0, this.Rows - 1);
		}

		public event Action<Terminal, int> Scrolled;

		public event Action<Terminal, string> DataEmitted;

		internal void Bell ()
		{
			//Console.WriteLine ("beep");
		}

		public void EmitLineFeed ()
		{
			if (LineFeedEvent != null)
				LineFeedEvent (this);
		}

		public event Action<Terminal> LineFeedEvent;

		internal void EmitA11yTab (object p)
		{
			throw new NotImplementedException ();
		}

		internal void SetgLevel (int v)
		{
			gLevel = v;
			if (CharSets.All.TryGetValue ((byte)v, out var cs))
				Charset = cs;
			else
				Charset = null;
		}

		internal int EraseAttr ()
		{
			return (CharData.DefaultAttr & ~0x1ff) | CurAttr & 0x1ff;
		}

		internal void EmitScroll (int v)
		{
			return;
			throw new NotImplementedException ();
		}

		internal void SetgCharset (byte v, Dictionary<byte, string> charset)
		{
			CharSets.All [v] = charset;
			if (gLevel == v)
				this.charset = charset;
		}

		public void Resize (int cols, int rows)
		{
			if (cols < MINIMUM_COLS)
				cols = MINIMUM_COLS;
			if (rows < MINIMUM_ROWS)
				rows = MINIMUM_ROWS;
			if (cols == Cols && rows == Rows)
				return;

			var oldCols = Cols;
			Cols = cols;
			Rows = rows;
			Buffers.Resize (cols, rows);
			buffers.SetupTabStops (oldCols);
			Refresh (0, Rows - 1);
		}

		internal void SyncScrollArea ()
		{
			// This should call the viewport syncscrollarea
			//throw new NotImplementedException ();
		}

		/// <summary>
		/// Implemented by subclasses - must refresh the display from the starting to the end row.
		/// </summary>
		/// <param name="startRow">Initial row to update, offset starts at zero.</param>
		/// <param name="endRow">Last row to update.</param>
		public void Refresh (int startRow, int endRow)
		{
			// TO BE HONEST - This probably should not be called directly,
			// instead the view shoudl after feeding data, determine if there is a need
			// to refresh based on the parameters provided for refresh ranges, and then
			// update, to avoid the backend rtiggering this multiple times.

			UpdateRange (startRow);
			UpdateRange (endRow);
		}

		public void ShowCursor ()
		{
			if (cursorHidden == false)
				return;
			cursorHidden = false;
			Refresh (Buffer.Y, Buffer.Y);
			terminalDelegate.ShowCursor (this);
		}

		/// <summary>
		/// Encodes button and position to characters
		/// </summary>
		void EncodeMouseUtf (List<byte> data, int ch)
		{
			if (ch == 2047) {
				data.Add (0);
				return;
			}
			if (ch < 127) {
				data.Add ((byte)ch);
			} else {
				if (ch > 2047) 
					ch = 2047;
				data.Add ((byte)(0xC0 | (ch >> 6)));
				data.Add ((byte)(0x80 | (ch & 0x3F)));
			}
		}

		/// <summary>
		/// Encodes the mouse button.
		/// </summary>
		/// <returns>The mouse button.</returns>
		/// <param name="button">Button (0, 1, 2 for left, middle, right) and 4 for wheel up, and 5 for wheel down.</param>
		/// <param name="release">If set to <c>true</c> release.</param>
		/// <param name="wheelUp">If set to <c>true</c> wheel up.</param>
		/// <param name="shift">If set to <c>true</c> shift.</param>
		/// <param name="meta">If set to <c>true</c> meta.</param>
		/// <param name="control">If set to <c>true</c> control.</param>
		public int EncodeMouseButton (int button, bool release, bool shift, bool meta, bool control)
		{
			int value;

			if (release)
				value = 3;
			else {
				switch (button) {
				case 0:
					value = 0;
					break;
				case 1:
					value = 1;
					break;
				case 2:
					value = 2;
					break;
				case 4:
					value = 64;
					break;
				case 5:
					value = 65;
					break;
				default:
					value = 0;
					break;
				}
			}

			if (MouseMode.SendsModifiers()) {
				if (shift)
					value |= 4;
				if (meta)
					value |= 8;
				if (control)
					value |= 16;
			}
			return value;
		}

		/// <summary>
		/// Sends a mouse event for a specific button at the specific location
		/// </summary>
		/// <param name="buttonFlags">Button flags encoded in Cb mode.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public void SendEvent (int buttonFlags, int x, int y)
		{
			switch (MouseProtocol) {
			case MouseProtocolEncoding.X10:
				SendResponse (ControlCodes.CSI, "M", (byte)(buttonFlags + 32), (byte)Math.Min (255, (32 + x + 1)), (byte)Math.Min (255, (32 + y + 1)));
				break;
			case MouseProtocolEncoding.SGR:
				var bflags = ((buttonFlags & 3) == 3) ? (buttonFlags & ~3) : buttonFlags;
				var m = ((buttonFlags & 3) == 3) ? "m" : "M";
				SendResponse (ControlCodes.CSI, $"<{bflags};{x+1};{y+1}{m}");
				break;
			case MouseProtocolEncoding.URXVT:
				SendResponse (ControlCodes.CSI, $"{buttonFlags+32};{x+1};{y+1}M");
				break;
			case MouseProtocolEncoding.UTF8:
				var utf8 = new List<byte> () { 0x4d /* M */ };
				EncodeMouseUtf (utf8, ch: buttonFlags+32);
				EncodeMouseUtf (utf8, ch: x+33);
				EncodeMouseUtf (utf8, ch: y+33);
				SendResponse (ControlCodes.CSI, utf8.ToArray());
				break;
			}
		}

		public void SendMouseMotion (int buttonFlags, int x, int y)
		{
			SendEvent (buttonFlags + 32, x, y);

		}

		static Dictionary<int, int> matchColorCache = new Dictionary<int, int> ();

		public int MatchColor (int r1, int g1, int b1)
		{
			throw new NotImplementedException ();
		}

		internal void EmitData (string txt)
		{
			DataEmitted?.Invoke (this, txt);
		}

		/// <summary>
		/// Implement to change the cursor style, call the base implementation.
		/// </summary>
		/// <param name="style"></param>
		public void SetCursorStyle (CursorStyle style)
		{
		}

		internal void ReverseIndex ()
		{
			var buffer = Buffer;

			if (buffer.Y == buffer.ScrollTop) {
				// possibly move the code below to term.reverseScroll();
				// test: echo -ne '\e[1;1H\e[44m\eM\e[0m'
				// blankLine(true) is xterm/linux behavior
				var scrollRegionHeight = buffer.ScrollBottom - buffer.ScrollTop;
				buffer.Lines.ShiftElements (buffer.Y + buffer.YBase, scrollRegionHeight, 1);
				buffer.Lines [buffer.Y + buffer.YBase] = buffer.GetBlankLine (EraseAttr ());
				UpdateRange (buffer.ScrollTop);
				UpdateRange (buffer.ScrollBottom);
			} else {
				buffer.Y--;
			}
		}


		/// <summary>
		/// Provides a baseline set of environment variables that would be useful to run the terminal,
		/// you can customzie these accordingly.
		/// </summary>
		public static string [] GetEnvironmentVariables (string termName = null)
		{
			var l = new List<string> ();
			if (termName == null)
				termName = "xterm-256color";

			l.Add ("TERM=" + termName);

			// Without this, tools like "vi" produce sequences that are not UTF-8 friendly
			l.Add ("LANG=en_US.UTF-8");
			var env = Environment.GetEnvironmentVariables ();
			foreach (var x in new [] { "LOGNAME", "USER", "DISPLAY", "LC_TYPE", "USER", "HOME", "PATH" })
				if (env.Contains (x))
					l.Add ($"{x}={env [x]}");
			return l.ToArray ();
		}

		/// <summary>
		/// Sets up the terminals initial state
		/// </summary>
		void Setup ()
		{
			cursorHidden = false;

			// modes
			applicationKeypad = false;
			applicationCursor = false;
			OriginMode = false;
			MarginMode = false;
			InsertMode = false;
			Wraparound = true;
			bracketedPasteMode = false;

			// charset
			charset = null;
			gcharset = 0;
			gLevel = 0;

			CurAttr = CharData.DefaultAttr;

			MouseMode = MouseMode.Off;
			MouseProtocol = MouseProtocolEncoding.X10;

			// TODO REST
		}
	}
}
