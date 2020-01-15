﻿using System;
using System.Collections.Generic;
using System.Text;

namespace XtermSharp {
	public interface ITerminalDelegate {
		void ShowCursor (Terminal source);
		void SetTerminalTitle (Terminal source, string title);
		/// <summary>
		/// This event is triggered from the engine, when the request to resize the window is received from an escape sequence.
		/// </summary>
		/// <param name="source">Source.</param>
		void SizeChanged (Terminal source);

		/// <summary>
		/// Used to respond to the client running on the other end.  This information should be sent to the remote end or subshell.
		/// </summary>
		/// <param name="data"></param>
		void Send (byte [] data);
	}

	//
    	// Simple implementation of ITerminalDelegate, when you do not want to 
	// do a lot of work to use.
    	//
	public class SimpleTerminalDelegate : ITerminalDelegate {
		public void Send (byte [] data)
		{
		}

		public virtual void SetTerminalTitle (Terminal source, string title)
		{
	
		}

		public virtual void ShowCursor (Terminal source)
		{
		}

		public virtual void SizeChanged (Terminal source)
		{
		}
	}

	public class Terminal {
		ITerminalDelegate tdelegate;

		const int MINIMUM_COLS = 2;
		const int MINIMUM_ROWS = 1;

		BufferSet buffers;
		InputHandler input;
		bool applicationKeypad, applicationCursor;
		bool cursorHidden;
		bool originMode;
		bool insertMode;
		bool bracketedPasteMode;
		Dictionary<byte, string> charset;
		int gcharset;

		public Terminal (ITerminalDelegate tdel = null, TerminalOptions options = null)
		{
			if (options == null)
				options = new TerminalOptions ();
			if (tdel == null)
				tdel = new SimpleTerminalDelegate ();

			Options = options;
			Setup ();
			tdelegate = tdel;
		}

		public Terminal ()
		{
		}

		void Setup ()
		{

			Cols = Math.Max (Options.Cols, MINIMUM_COLS);
			Rows = Math.Max (Options.Rows, MINIMUM_ROWS);

			buffers = new BufferSet (this);
			input = new InputHandler (this);
			cursorHidden = false;

			// modes
			applicationKeypad = false;
			applicationCursor = false;
			originMode = false;
			InsertMode = false;
			Wraparound = true;
			bracketedPasteMode = false;

			// charset
			charset = null;
			gcharset = 0;
			gLevel = 0;

			CurAttr = CharData.DefaultAttr;

			// TODO REST
		}

		public void SendResponse (string txt)
		{
			tdelegate.Send (Encoding.UTF8.GetBytes (txt));
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

		public Buffer Buffer => buffers.Active;
		public BufferSet Buffers => buffers;

		public bool ApplicationCursor { get; internal set; }
		public int SavedCols { get; internal set; }
		public bool ApplicationKeypad { get; internal set; }

		internal bool X10Mouse { get; set; }
		internal bool UtfMouse { get; set; }
		internal bool Vt200Mouse { get; set; }

		public bool SendFocus { get; internal set; }
		public bool OriginMode { get; internal set; }


		/// <summary>
		/// If MouseEvents is set, then we are supposed to send some kind of mouse events, which
		/// are determined by the boolean flags below.  Additionally, a "style" is encoded in
		/// SgrMouse, UrxvtMouse which alter the responses.
		/// </summary>
		/// <value><c>true</c> if mouse events; otherwise, <c>false</c>.</value>
		public bool MouseEvents { get; internal set; }

		/// <summary>
		/// If MouseEvents is set, then this value should be probed to determine whether a UI front-end needs to send the MouseRelease event.
		/// </summary>
		/// <value><c>true</c> if the UI is expected to send a mouse release event when the button is released.</value>
		public bool MouseSendsRelease { get; internal set; }

		/// <summary>
		/// If MosueEvents is set, and this is set, then all motion events should be sent, regardless of the state of the mouse buttons. (Xterm flag 1003)
		/// </summary>
		/// <value><c>true</c> if mouse sends all motion; otherwise, <c>false</c>.</value>
		public bool MouseSendsAllMotion { get; internal set; }

		// Should sent motion events when a button is pressed (1002)
		/// <summary>
		/// If MouseEvents is set, then motion events should be sent when the mouse button is held down (Xterm flag 1002)
		/// </summary>
		/// <value><c>true</c> if mouse sends motion when pressed; otherwise, <c>false</c>.</value>
		public bool MouseSendsMotionWhenPressed { get; internal set; }


		/// <summary>
		/// If MouseEvents is set, this determines whether the UI layer should send Wheel events.
		/// </summary>
		/// <value><c>true</c> if mouse sends wheel; otherwise, <c>false</c>.</value>
		public bool MouseSendsWheel { get; internal set; }

		// Whether control/meta/shift modifiers are encoded
		internal bool MouseSendsModifiers = false;
		internal bool SgrMouse;
		internal bool UrxvtMouse;

		public bool CursorHidden { get; internal set; }
		public bool BracketedPasteMode { get; internal set; }

		public TerminalOptions Options { get; private set; }
		public int Cols { get; private set; }
		public int Rows { get; private set; }
		public bool Wraparound;
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
			if (y > refreshEnd && y < Buffer.Lines.Length)
				refreshEnd = y;
			else
				refreshEnd = Buffer.Lines.Length - 1;
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
				buffer.Lines.ShiftElements (topRow + 1, scrollRegionHeight - 1, -1);
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
			if (Scrolled != null)
				Scrolled (this, buffer.YDisp);
		}

		public event Action<Terminal, int> Scrolled;

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
			Cols = cols;
			Rows = rows;
			Buffers.Resize (cols, rows);
			buffers.SetupTabStops (cols);
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
			tdelegate.ShowCursor (this);
		}

		internal void SetX10MouseStyle ()
		{
			X10Mouse = true;
			MouseEvents = true;

			MouseSendsRelease = false;
			MouseSendsAllMotion = false;
			MouseSendsWheel = false;
			MouseSendsModifiers = false;
		}

		internal void SetVT200MouseStyle ()
		{
			Vt200Mouse = true;
			MouseEvents = true;

			MouseSendsRelease = true;
			MouseSendsAllMotion = false;
			MouseSendsWheel = true;
			MouseSendsModifiers = false;
		}

		// Encode button and position to characters
		void Encode (List<byte> data, int ch)
		{
			if (UtfMouse) {
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
			} else {
				if (ch == 255) {
					data.Add (0);
					return;
				}
				if (ch > 127) 
					ch = 127;
				data.Add ((byte) ch);
			}
		}

		/// <summary>
		/// Encodes the button.
		/// </summary>
		/// <returns>The button.</returns>
		/// <param name="button">Button (0, 1, 2 for left, middle, right) and 4 for wheel up, and 5 for wheel down.</param>
		/// <param name="release">If set to <c>true</c> release.</param>
		/// <param name="wheelUp">If set to <c>true</c> wheel up.</param>
		/// <param name="shift">If set to <c>true</c> shift.</param>
		/// <param name="meta">If set to <c>true</c> meta.</param>
		/// <param name="control">If set to <c>true</c> control.</param>
		public int EncodeButton (int button, bool release, bool shift, bool meta, bool control)
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
			if (MouseSendsModifiers) {
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
			// TODO
			// Handle X10 Mouse,
			// Urxvt Mouse
			// SgrMouse
			if (SgrMouse) {
				var bflags = ((buttonFlags & 3) == 3) ? (buttonFlags & ~3) : buttonFlags;
				var sres = "\x1b[<" + bflags + ";" + (x+1) + ";" + (y+1) + (((buttonFlags & 3) == 3) ? 'm' : 'M');
				tdelegate.Send (Encoding.UTF8.GetBytes (sres));
				return;
			}
			if (Vt200Mouse) {

			}
			var res = new List<byte> () { 0x1b, (byte)'[', (byte)'M' };
			Encode (res, buttonFlags+32);
			Encode (res, x+33);
			Encode (res, y+33);
			tdelegate.Send (res.ToArray ());

		}

		public void SendMotion (int buttonFlags, int x, int y)
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
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Implement to change the cursor style, call the base implementation.
		/// </summary>
		/// <param name="style"></param>
		public void SetCursorStyle (CursorStyle style)
		{
		}

		string TerminalTitle { get; set; }
		/// <summary>
		/// Override to set the current terminal text
		/// </summary>
		/// <param name="text"></param>
		internal void SetTitle (string text)
		{
			TerminalTitle = text;
			tdelegate.SetTerminalTitle (this, text);
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
		/// <returns></returns>
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
	}
}
