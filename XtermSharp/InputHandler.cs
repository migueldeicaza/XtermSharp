using System;
using System.Collections.Generic;
using NStack;

// 
// Not implemented (either in xterm.js):
// DECUDK (https://vt100.net/docs/vt510-rm/DECUDK.html)
// DCS + q Pt ST (xterm) * Request Terminfo String
//  DCS + p Pt ST (xterm) * set terminfo data

namespace XtermSharp {
	// DCS Subparser implementations

 	// DCS $ q Pt ST
 	// DECRQSS (https://vt100.net/docs/vt510-rm/DECRQSS.html)
 	//   Request Status String (DECRQSS), VT420 and up.
 	// Response: DECRPSS (https://vt100.net/docs/vt510-rm/DECRPSS.html)
	class DECRQSS : IDcsHandler {
		List<Rune> data;
		Terminal terminal;

		public DECRQSS (Terminal terminal)
		{
			this.terminal = terminal;
		}

		public void Hook (string collect, int [] parameters, int flag)
		{
			data = new List<Rune> ();
		}

		public void Put (uint [] data, int start, int end)
		{
			for (int i = start; i < end; i++)
				this.data.Add ((Rune) data [i]);
		}

		public void Unhook ()
		{
			var newData = ustring.Make (this.data).ToString ();
			switch (newData) {
			case "\"q": // DECCSA
				terminal.Handler ("\x1bP1$r0\"q$\x1b\\");
				return;
			case "\"p": // DECSCL
				terminal.Handler ("\x1bP1$r61\"p$\x1b\\");
				return;
			case "r": // DECSTBM
				var pt = "" + (terminal.Buffer.ScrollTop + 1) +
					';' + (terminal.Buffer.ScrollBottom + 1) + 'r';
				terminal.Handler ("\x1bP1$r$" + pt + "\x1b\\");
				return;
			case "m": // SGR
				  // TODO: report real settings instead of 0m
				throw new NotImplementedException ();
			default:
				// invalid: DCS 0 $ r Pt ST (xterm)
				terminal.Error ($"Unknown DCS + {newData}");
				terminal.Handler ("\x1bP0$r$\x1b");
				break;
			}
		}
	}

	// 
	// The terminal's standard implementation of IInputHandler, this handles all
	// input from the Parser.
	// 
	// Refer to http://invisible-island.net/xterm/ctlseqs/ctlseqs.html to understand
	// each function's header comment.
	// 
	class InputHandler {
		Terminal terminal;
		EscapeSequenceParser parser;

		public EscapeSequenceParser.ExecuteHandler NextLine { get; }

		public InputHandler (Terminal terminal)
		{
			this.terminal = terminal;
			parser = new EscapeSequenceParser ();
			parser.SetCsiHandlerFallback ((string collect, int [] pars, int flag) => {
				terminal.Error ("Unknown CSI code", collect, pars, flag);
			});
			parser.SetEscHandlerFallback ((string collect, int flag) => {
				terminal.Error ("Unknown ESC code", collect, flag);
			});
			parser.SetExecuteHandlerFallback ((byte  code) => {
				terminal.Error ("Unknown EXECUTE code", code);
			});
			parser.SetOscHandlerFallback ((int identifier, string data) => {
				terminal.Error ("Unknown OSC code", identifier, data);
			});

			// Print handler
			parser.SetPrintHandler (Print);

			// CSI handler
			parser.SetCsiHandler ('@', (pars, collect) => InsertChars (pars));
			parser.SetCsiHandler ('A', (pars, collect) => CursorUp (pars));
			parser.SetCsiHandler ('B', (pars, collect) => CursorDown (pars));
			parser.SetCsiHandler ('C', (pars, collect) => CursorForward (pars));
			parser.SetCsiHandler ('D', (pars, collect) => CursorBackward (pars));
			parser.SetCsiHandler ('E', (pars, collect) => CursorNextLine (pars));
			parser.SetCsiHandler ('F', (pars, collect) => CursorPrecedingLine (pars));
			parser.SetCsiHandler ('G', (pars, collect) => CursorCharAbsolute (pars));
			parser.SetCsiHandler ('H', (pars, collect) => CursorPosition (pars));
			parser.SetCsiHandler ('I', (pars, collect) => CursorForwardTab (pars));
			parser.SetCsiHandler ('J', (pars, collect) => EraseInDisplay (pars));
			parser.SetCsiHandler ('K', (pars, collect) => EraseInLine (pars));
			parser.SetCsiHandler ('L', (pars, collect) => InsertLines (pars));
			parser.SetCsiHandler ('M', (pars, collect) => DeleteLines (pars));
			parser.SetCsiHandler ('P', (pars, collect) => DeleteChars (pars));
			parser.SetCsiHandler ('S', (pars, collect) => ScrollUp (pars));
			parser.SetCsiHandler ('T', (pars, collect) => ScrollDown (pars, collect));
			parser.SetCsiHandler ('X', (pars, collect) => EraseChars (pars));
			parser.SetCsiHandler ('Z', (pars, collect) => CursorBackwardTab (pars));
			parser.SetCsiHandler ('`', (pars, collect) => CharPosAbsolute (pars));
			parser.SetCsiHandler ('a', (pars, collect) => HPositionRelative (pars));
			parser.SetCsiHandler ('b', (pars, collect) => RepeatPrecedingCharacter (pars));
			parser.SetCsiHandler ('c', (pars, collect) => SendDeviceAttributes (pars, collect));
			parser.SetCsiHandler ('d', (pars, collect) => LinePosAbsolute (pars));
			parser.SetCsiHandler ('e', (pars, collect) => VPositionRelative (pars));
			parser.SetCsiHandler ('f', (pars, collect) => HVPosition (pars));
			parser.SetCsiHandler ('g', (pars, collect) => TabClear (pars));
			parser.SetCsiHandler ('h', (pars, collect) => SetMode (pars, collect));
			parser.SetCsiHandler ('l', (pars, collect) => ResetMode (pars, collect));
			parser.SetCsiHandler ('m', (pars, collect) => CharAttributes (pars));
			parser.SetCsiHandler ('n', (pars, collect) => DeviceStatus (pars, collect));
			parser.SetCsiHandler ('p', (pars, collect) => SoftReset (pars, collect));
			parser.SetCsiHandler ('q', (pars, collect) => SetCursorStyle (pars, collect));
			parser.SetCsiHandler ('r', (pars, collect) => SetScrollRegion (pars, collect));
			parser.SetCsiHandler ('s', (pars, collect) => SaveCursor (pars));
			parser.SetCsiHandler ('u', (pars, collect) => RestoreCursor (pars));

			// Execute Handler
			parser.SetExecuteHandler (7, terminal.Bell);
			parser.SetExecuteHandler (10, LineFeed);
			parser.SetExecuteHandler (11, LineFeed);
			parser.SetExecuteHandler (12, LineFeed);
			parser.SetExecuteHandler (13, CarriageReturn);
			parser.SetExecuteHandler (8,  Backspace);
			parser.SetExecuteHandler (9, Tab);
			parser.SetExecuteHandler (14, ShiftOut);
			parser.SetExecuteHandler (15, ShiftIn);
			// Comment in original FIXME:   What do to with missing? Old code just added those to print.

			// some C1 control codes - FIXME: should those be enabled by default?
			parser.SetExecuteHandler (0x84 /* Index */, (x) => terminal.Index ());
			parser.SetExecuteHandler (0x85 /* Next Line */, NextLine);
			parser.SetExecuteHandler (0x88 /* Horizontal Tabulation Set */, TabSet);

			// 
			// OSC handler
			// 
			//   0 - icon name + title
			parser.SetOscHandler (0, SetTitle);
			//   1 - icon name
			//   2 - title
			parser.SetOscHandler (2, SetTitle);
			//   3 - set property X in the form "prop=value"
			//   4 - Change Color Number
			//   5 - Change Special Color Number
			//   6 - Enable/disable Special Color Number c
			//   7 - current directory? (not in xterm spec, see https://gitlab.com/gnachman/iterm2/issues/3939)
			//  10 - Change VT100 text foreground color to Pt.
			//  11 - Change VT100 text background color to Pt.
			//  12 - Change text cursor color to Pt.
			//  13 - Change mouse foreground color to Pt.
			//  14 - Change mouse background color to Pt.
			//  15 - Change Tektronix foreground color to Pt.
			//  16 - Change Tektronix background color to Pt.
			//  17 - Change highlight background color to Pt.
			//  18 - Change Tektronix cursor color to Pt.
			//  19 - Change highlight foreground color to Pt.
			//  46 - Change Log File to Pt.
			//  50 - Set Font to Pt.
			//  51 - reserved for Emacs shell.
			//  52 - Manipulate Selection Data.
			// 104 ; c - Reset Color Number c.
			// 105 ; c - Reset Special Color Number c.
			// 106 ; c; f - Enable/disable Special Color Number c.
			// 110 - Reset VT100 text foreground color.
			// 111 - Reset VT100 text background color.
			// 112 - Reset text cursor color.
			// 113 - Reset mouse foreground color.
			// 114 - Reset mouse background color.
			// 115 - Reset Tektronix foreground color.
			// 116 - Reset Tektronix background color.

			// 
			// ESC handlers
			// 
			parser.SetEscHandler ("7", SaveCursor);
			parser.SetEscHandler ("8", RestoreCursor);
			parser.SetEscHandler ("D", (c) => terminal.Index ());
			parser.SetEscHandler ("E", NextLine);
			parser.SetEscHandler ("H", TabSet);
			parser.SetEscHandler ("M", ReverseIndex);
			parser.SetEscHandler ("=", KeypadApplicationMode);
			parser.SetEscHandler (">", KeypadNumericMode);
			parser.SetEscHandler ("c", Reset);
			parser.SetEscHandler ("n", (c) => SetgLevel (2));
			parser.SetEscHandler ("o", (c) => SetgLevel (3));
			parser.SetEscHandler ("|", (c) => SetgLevel (3));
			parser.SetEscHandler ("}", (c) => SetgLevel (2));
			parser.SetEscHandler ("~", (c) => SetgLevel (1));
			parser.SetEscHandler ("%@", SelectDefaultCharset);
			parser.SetEscHandler ("%G", SelectDefaultCharset);
			foreach (var flag in CharSets.All.Keys) {
				parser.SetEscHandler ("(" + flag, (code) => SelectCharset ("(" + flag));
				parser.SetEscHandler (")" + flag, (code) => SelectCharset (")" + flag));
				parser.SetEscHandler ("*" + flag, (code) => SelectCharset ("*" + flag));
				parser.SetEscHandler ("+" + flag, (code) => SelectCharset ("+" + flag));
				parser.SetEscHandler ("-" + flag, (code) => SelectCharset ("-" + flag));
				parser.SetEscHandler ("." + flag, (code) => SelectCharset ("." + flag));
				parser.SetEscHandler ("/" + flag, (code) => SelectCharset ("/" + flag)); // TODO: supported?
			}

			// Error handler
			parser.SetErrorHandler ((state) => {
				terminal.Error ("Parsing error, state: ", state);
				return state;
			});

			// DCS Handler
			parser.SetDcsHandler ("$q", new DECRQSS (terminal));
		}

		public event Action<InputHandler> CursorMoved;

		public void Parse (byte [] data)
		{
			var buffer = terminal.Buffer;
			var cursorStartX = buffer.X;
			var cursorStartY = buffer.Y;

			if (terminal.Debug)
				terminal.Log ("data: " + data);
			var ustr = ustring.Make (data);
			var runes = ustr.ToRunes ();
			parser.Parse (runes, runes.Length);

			buffer = terminal.Buffer;
			if (buffer.X != cursorStartX || buffer.Y != cursorStartY)
				CursorMoved (this);
		}

		private bool InsertLines (int [] pars)
		{
			throw new NotImplementedException ();
		}

		void SelectCharset (string p)
		{
			throw new NotImplementedException ();
		}

		void SelectDefaultCharset (byte code)
		{
			throw new NotImplementedException ();
		}

		void SetgLevel (int n)
		{
			throw new NotImplementedException ();
		}

		void Reset (byte code)
		{
			parser.Reset ();
			terminal.Reset ();
		}

		void KeypadNumericMode (byte code)
		{
			throw new NotImplementedException ();
		}

		void KeypadApplicationMode (byte code)
		{
			throw new NotImplementedException ();
		}

		void ReverseIndex (byte code)
		{
			throw new NotImplementedException ();
		}

		void RestoreCursor (string collect, int flag)
		{
			throw new NotImplementedException ();
		}

		void SaveCursor (string collect, int flag)
		{
			throw new NotImplementedException ();
		}

		bool SetTitle (string data)
		{
			throw new NotImplementedException ();
		}

		void TabSet (byte code)
		{
			throw new NotImplementedException ();
		}

		// SI
		// ShiftIn (Control-O) Switch to standard character set.  This invokes the G0 character set
		void ShiftIn (byte code)
		{
			terminal.SetgLevel (0);
		}

		// SO
		// ShiftOut (Control-N) Switch to alternate character set.  This invokes the G1 character set
		void ShiftOut (byte code)
		{
			terminal.SetgLevel (1);
		}

		//
		// Horizontal tab (Control-I)
		//
		void Tab (byte code)
		{
			var originalX = terminal.Buffer.X;
			terminal.Buffer.X = terminal.Buffer.NextTabStop ();
			if (terminal.Options.ScreenReaderMode)
				terminal.EmitA11yTab (terminal.Buffer.X - originalX);
		}

		// 
		// Backspace handler (Control-h)
		//
		void Backspace (byte code)
		{
			if (terminal.Buffer.X > 0)
				terminal.Buffer.X--;
		}

		void CarriageReturn (byte code)
		{
			terminal.Buffer.X = 0;
		}

		void LineFeed (byte code)
		{
			var buffer = terminal.Buffer;
			if (terminal.Options.ConvertEol)
				buffer.X = 0;
			buffer.Y++;
			if (buffer.Y > buffer.ScrollBottom) {
				buffer.Y--;
				terminal.Scroll (isWrapped: false);
			}
			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols) 
				buffer.X--;

			// This event is emitted whenever the terminal outputs a LF or NL.
			terminal.EmitLineFeed ();
		}

		// 
		// Helper method to erase cells in a terminal row.
		// The cell gets replaced with the eraseChar of the terminal.
		// @param y row index
		// @param start first cell index to be erased
		// @param end   end - 1 is last erased cell
		// 
		void EraseInBufferLine (int y, int start, int end, bool clearWrap = false)
		{
			var line = terminal.Buffer.Lines [terminal.Buffer.YBase + y];
			var cd = CharData.Null;
			cd.Attribute = terminal.EraseAttr ();
			line.ReplaceCells (start, end, cd);
			if (clearWrap)
				line.IsWrapped = false;
		}

		// 
		// Helper method to reset cells in a terminal row.
		// The cell gets replaced with the eraseChar of the terminal and the isWrapped property is set to false.
		// @param y row index
		// 
		void ResetBufferLine (int y)
		{
			EraseInBufferLine (y, 0, terminal.Cols, true);
		}

		bool RestoreCursor (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool SaveCursor (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool SetScrollRegion (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool SetCursorStyle (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool SoftReset (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool DeviceStatus (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool CharAttributes (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool ResetMode (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool SetMode (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool TabClear (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool HVPosition (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool VPositionRelative (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool LinePosAbsolute (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool SendDeviceAttributes (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool RepeatPrecedingCharacter (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool HPositionRelative (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CharPosAbsolute (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorBackwardTab (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool EraseChars (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool ScrollDown (int [] pars, string collect)
		{
			throw new NotImplementedException ();
		}

		bool ScrollUp (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool DeleteChars (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool DeleteLines (int [] pars)
		{
			throw new NotImplementedException ();
		}

		// 
		// CSI Ps K  Erase in Line (EL).
		//     Ps = 0  -> Erase to Right (default).
		//     Ps = 1  -> Erase to Left.
		//     Ps = 2  -> Erase All.
		// CSI ? Ps K
		//   Erase in Line (DECSEL).
		//     Ps = 0  -> Selective Erase to Right (default).
		//     Ps = 1  -> Selective Erase to Left.
		//     Ps = 2  -> Selective Erase All.
		// 
		void EraseInLine (int [] pars)
		{
			var p = pars.Length == 0 ? 0 : pars [0];
			var buffer = terminal.Buffer;
			switch (p){
			case 0:
        			EraseInBufferLine (buffer.Y, buffer.X, terminal.Cols);
				break;
		      	case 1:
		        	EraseInBufferLine (buffer.Y, 0, buffer.X + 1);
				break;
		      	case 2:
		        	EraseInBufferLine (buffer.Y, 0, terminal.Cols);
				break;
			}
			terminal.UpdateRange(buffer.Y);
		}

		// 
		// CSI Ps J  Erase in Display (ED).
		//     Ps = 0  -> Erase Below (default).
		//     Ps = 1  -> Erase Above.
		//     Ps = 2  -> Erase All.
		//     Ps = 3  -> Erase Saved Lines (xterm).
		// CSI ? Ps J
		//   Erase in Display (DECSED).
		//     Ps = 0  -> Selective Erase Below (default).
		//     Ps = 1  -> Selective Erase Above.
		//     Ps = 2  -> Selective Erase All.
		// 
		void EraseInDisplay (int [] pars)
		{
			var p = pars.Length == 0 ? 0 : pars [0];
			var buffer = terminal.Buffer;
			int j;
			switch (p) {
			case 0:
				j = buffer.Y;
				terminal.UpdateRange (j);
				EraseInBufferLine (j++, buffer.X, terminal.Cols, buffer.X == 0);
				for (; j < terminal.Rows; j++) {
					ResetBufferLine (j);
				}
				terminal.UpdateRange (j);
				break;
			case 1:
				j = buffer.Y;
				terminal.UpdateRange (j);
				// Deleted front part of line and everything before. This line will no longer be wrapped.
				EraseInBufferLine (j, 0, buffer.X + 1, true);
				if (buffer.X + 1 >= terminal.Cols) {
					// Deleted entire previous line. This next line can no longer be wrapped.
					buffer.Lines[j + 1].IsWrapped = false;
				}
				while (j-- != 0) {
					ResetBufferLine (j);
				}
				terminal.UpdateRange (0);
				break;
			case 2:
				j = terminal.Rows;
				terminal.UpdateRange (j - 1);
				while (j-- != 0) {
					ResetBufferLine (j);
				}
				terminal.UpdateRange (0);
				break;
			case 3:
				// Clear scrollback (everything not in viewport)
				var scrollBackSize = buffer.Lines.Length - terminal.Rows;
				if (scrollBackSize > 0) {
					buffer.Lines.TrimStart (scrollBackSize);
					buffer.YBase = Math.Max (buffer.YBase - scrollBackSize, 0);
					buffer.YDisp = Math.Max (buffer.YDisp - scrollBackSize, 0);
					// Force a scroll event to refresh viewport
					terminal.EmitScroll (0);
				}
				break;

			}
		}

		// 
		// CSI Ps I
		//   Cursor Forward Tabulation Ps tab stops (default = 1) (CHT).
		// 
		void CursorForwardTab (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			while (param-- != 0)
				buffer.X = buffer.NextTabStop ();
		}

		// 
		// CSI Ps ; Ps H
		// Cursor Position [row;column] (default = [1,1]) (CUP).
		// 
		void CursorPosition (int [] pars)
		{
			int col, row;
			switch (pars.Length) {
			case 1:
				row = pars [0] - 1;
				col = 0;
				break;
			case 2:
				row = pars [0] - 1;
				col = pars [1] - 1;
				break;
			default:
				col = 0;
				row = 0;
				break;
			}
			col = Math.Min (Math.Max (col, 0), terminal.Cols - 1);
			row = Math.Min (Math.Max (row, 0), terminal.Rows - 1);

			var buffer = terminal.Buffer;
			buffer.X = col;
			buffer.Y = row;
		}

		// 
		// CSI Ps G
		// Cursor Character Absolute  [column] (default = [row,1]) (CHA).
		// 
		void CursorCharAbsolute (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.X = param - 1;
		}

		// 
		// CSI Ps F
		// Cursor Preceding Line Ps Times (default = 1) (CNL).
		// reuse CSI Ps A ?
		// 
		void CursorPrecedingLine (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.Y -= param;
			if (buffer.Y < 0)
				buffer.Y = 0;
			buffer.X = 0;
		}

		// 
		// CSI Ps E
		// Cursor Next Line Ps Times (default = 1) (CNL).
		// same as CSI Ps B?
		// 
		void CursorNextLine (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.Y += param;
			if (buffer.Y >= terminal.Rows)
				buffer.Y = terminal.Rows - 1;

			buffer.X = 0;
		}

		// 
		// CSI Ps D
		// Cursor Backward Ps Times (default = 1) (CUB).
		// 
		void CursorBackward (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols) {
				buffer.X--;
			}
			buffer.X -= param;
			if (buffer.X < 0)
				buffer.X = 0;
		}

		// 
		// CSI Ps B
		// Cursor Forward Ps Times (default = 1) (CUF).
		// 
		void CursorForward (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.X += param;
			if (buffer.X > terminal.Cols)
				buffer.X = terminal.Cols - 1;
		}

		// 
		// CSI Ps B
		// Cursor Down Ps Times (default = 1) (CUD).
		// 
		void CursorDown (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.Y += param;
			if (buffer.Y > terminal.Rows)
				buffer.Y = terminal.Rows - 1;

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols) 
				buffer.X--;
		}

		// 
		// CSI Ps A
		// Cursor Up Ps Times (default = 1) (CUU).
		// 
		void CursorUp (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			buffer.Y -= param;
			if (buffer.Y < 0)
				buffer.Y = 0;
		}

		//
		// CSI Ps @
		// Insert Ps (Blank) Character(s) (default = 1) (ICH).
		//
		void InsertChars (int [] pars)
		{
			var buffer = terminal.Buffer;
			var cd = CharData.Null;
			cd.Attribute = terminal.EraseAttr ();

			buffer.Lines [buffer.Y + buffer.YBase].InsertCells (
			  	buffer.X,
				  pars.Length > 0 ? pars [0] : 1,
		    		cd);

			terminal.UpdateRange (buffer.Y);
		}

		void Print (uint [] data, int start, int end)
		{
			var buffer = terminal.Buffer;
			var charset = terminal.Charset;
			var screenReaderMode = terminal.Options.ScreenReaderMode;
			var cols = terminal.Cols;
			var wrapAroundMode = terminal.Wraparound;
			var insertMode = terminal.InsertMode;
			var curAttr = terminal.CurAttr;
			var bufferRow = buffer.Lines [buffer.Y + buffer.YBase];

			terminal.UpdateRange (buffer.Y);
			for (var pos = start; pos < end; ++pos) {
				var code = data [pos];

				// MIGUEL-TODO: I suspect this needs to be a stirng in C# to cope with Grapheme clusters
				var ch = (char)code;

				// calculate print space
				// expensive call, therefore we save width in line buffer
				var chWidth = Rune.ColumnWidth ((Rune)code);

				// get charset replacement character
				// charset are only defined for ASCII, therefore we only
				// search for an replacement char if code < 127
				if (code < 127 && charset != null) {

					// MIGUEL-FIXME - this is broken for dutch cahrset that returns two letters "ij", need to figure out what to do
					charset.TryGetValue ((byte)code, out var str);
					ch = str [0];
					code = (uint)ch;
				}
				if (screenReaderMode)
					terminal.EmitChar (ch);

				// insert combining char at last cursor position
				// FIXME: needs handling after cursor jumps
				// buffer.x should never be 0 for a combining char
				// since they always follow a cell consuming char
				// therefore we can test for buffer.x to avoid overflow left
				if (chWidth == 0 && buffer.X > 0) {
					// MIGUEL TODO: in the original code the getter might return a null value
					// does this mean that JS returns null for out of bounsd?
					if (buffer.X >= 1 && buffer.X < bufferRow.Length) {
						var chMinusOne = bufferRow [buffer.X - 1];
						if (chMinusOne.Width == 0) {
							// found empty cell after fullwidth, need to go 2 cells back
							// it is save to step 2 cells back here
							// since an empty cell is only set by fullwidth chars
							if (buffer.X >= 2){
								var chMinusTwo = bufferRow [buffer.X - 2];

								chMinusTwo.Code += ch;
								chMinusTwo.Rune = code;
								bufferRow [buffer.X - 2] = chMinusTwo; // must be set explicitly now
							}
						} else {
							chMinusOne.Code += ch;
							chMinusOne.Rune = code;
							bufferRow [buffer.X - 1] = chMinusOne; // must be set explicitly now
						}
					}
					continue;
				}

				// goto next line if ch would overflow
				// TODO: needs a global min terminal width of 2
				// FIXME: additionally ensure chWidth fits into a line
				//   -->  maybe forbid cols<xy at higher level as it would
				//        introduce a bad runtime penalty here
				if (buffer.X + chWidth - 1 >= cols) {
					// autowrap - DECAWM
					// automatically wraps to the beginning of the next line
					if (wrapAroundMode) {
						buffer.X = 0;
						buffer.Y++;
						if (buffer.Y > buffer.ScrollBottom) {
							buffer.Y--;
							terminal.Scroll (isWrapped: true);
						} else {
							// The line already exists (eg. the initial viewport), mark it as a
							// wrapped line
							buffer.Lines [buffer.Y].IsWrapped = true;
						}
						// row changed, get it again
						bufferRow = buffer.Lines [buffer.Y + buffer.YBase];
					} else {
						if (chWidth == 2) {
							// FIXME: check for xterm behavior
							// What to do here? We got a wide char that does not fit into last cell
							continue;
						}
						// FIXME: Do we have to set buffer.x to cols - 1, if not wrapping?
					}
				}

				var empty = CharData.Null;
				empty.Attribute = curAttr;
				// insert mode: move characters to right
				if (insertMode) {
					// right shift cells according to the width
					bufferRow.InsertCells (buffer.X, chWidth, empty);
					// test last cell - since the last cell has only room for
					// a halfwidth char any fullwidth shifted there is lost
					// and will be set to eraseChar
					var lastCell = bufferRow [cols - 1];
					if (lastCell.Width== 2) 
						bufferRow [cols - 1] = empty;
				
				}

				// write current char to buffer and advance cursor
				var x = new CharData (curAttr, code, chWidth, ch);
				bufferRow [buffer.X++] = x;

				// fullwidth char - also set next cell to placeholder stub and advance cursor
				// for graphemes bigger than fullwidth we can simply loop to zero
				// we already made sure above, that buffer.x + chWidth will not overflow right
				if (chWidth > 0) {
					while (--chWidth != 0) {
						bufferRow [buffer.X++] = empty;
					}
				}
			}
			terminal.UpdateRange (buffer.Y);
		}
	}
}
