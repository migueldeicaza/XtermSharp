using System;
using System.Collections.Generic;

namespace XtermSharp {

	public class Terminal {
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

		public Terminal (TerminalOptions options = null) 
		{
			if (options == null)
				options = new TerminalOptions ();

			Options = options;
			Setup ();
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

		public void Handler (string txt)
		{
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

		void Report (string prefix, string text, object[] args)
		{
			Console.WriteLine ($"{prefix}: {text}");
			for (int i = 0; i < args.Length; i++)
				Console.WriteLine ("    {0}: {1}", i, args [i]);

		}


		public void Feed (byte [] data)
		{
			input.Parse (data);
		}

		public Dictionary<byte, string> Charset { get; set; }

		public Buffer Buffer => buffers.Active;
		public BufferSet Buffers => buffers;

		public bool ApplicationCursor { get; internal set; }
		public int SavedCols { get; internal set; }
		public bool ApplicationKeypad { get; internal set; }
		public object X10Mouse { get; internal set; }
		public bool SendFocus { get; internal set; }
		public bool UtfMouse { get; internal set; }
		public bool OriginMode { get; internal set; }
		public bool Vt200Mouse { get; internal set; }
		public bool NormalMouse { get; internal set; }
		public bool MouseEvents { get; internal set; }
		public bool SgrMouse { get; internal set; }
		public bool UrxvtMouse { get; internal set; }
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
			if (y < refreshStart)
				refreshStart = y;
			if (y > refreshEnd)
				refreshEnd = y;
		}

		internal void EmitChar (char ch)
		{
			// For accessibility purposes 'a11y.char' in the original source.
		}

		internal void Reset ()
		{
			throw new NotImplementedException ();
		}

		// 
		// ESC D Index (Index is 0x84)
		//
		internal void Index ()
		{
			var buffer = Buffer;
			buffer.Y++;
			if (buffer.Y > buffer.ScrollBottom) {
				buffer.Y--;
				Scroll ();
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
			return (CharData.DefaultAttr & 0x1ff) | CurAttr & 0x1ff;
		}

		internal void EmitScroll (int v)
		{
			return;
			throw new NotImplementedException ();
		}

		internal void SetgCharset (int v, Dictionary<byte, string> @default)
		{
			throw new NotImplementedException ();
		}

		internal void Resize (int v, int rows)
		{
			throw new NotImplementedException ();
		}

		internal void SyncScrollArea ()
		{
			// This should call the viewport syncscrollarea
			throw new NotImplementedException ();
		}

		internal void EnableMouseEvents ()
		{
			// TODO:
	    	// DISABLE SELECTION MANAGER.
			throw new NotImplementedException ();
		}

		internal void DisableMouseEvents ()
		{
			// TODO:
	    	// ENABLE SELECTION MANAGER.
			throw new NotImplementedException ();
		}

		internal void Refresh (int v1, int v2)
		{
			throw new NotImplementedException ();
		}

		internal void ShowCursor ()
		{
			throw new NotImplementedException ();
		}

		static Dictionary<int,int> matchColorCache = new Dictionary<int, int> ();

		public int MatchColor (int r1, int g1, int b1)
		{
			throw new NotImplementedException ();
		}

		internal void EmitData (string txt)
		{
			throw new NotImplementedException ();
		}

		internal void SetCursorStyle (CursorStyle style)
		{
			throw new NotImplementedException ();
		}

		internal void SetTitle (string text)
		{
			throw new NotImplementedException ();
		}

		internal void TabSet ()
		{
			throw new NotImplementedException ();
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
	}
}
