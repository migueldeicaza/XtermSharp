using System;
using System.Collections.Generic;
using NStack;

// 
// Not implemented (either in xterm.js):
// DECUDK (https://vt100.net/docs/vt510-rm/DECUDK.html)
// DCS + q Pt ST (xterm) * Request Terminfo String
//  DCS + p Pt ST (xterm) * set terminfo data

namespace XtermSharp {
	[Flags]
	public enum FLAGS {
		BOLD = 1,
		UNDERLINE = 2,
		BLINK = 4,
		INVERSE = 8,
		INVISIBLE = 16,
		DIM = 32,
		ITALIC = 64
	}

	// DCS Subparser implementations

	// DCS $ q Pt ST
	// DECRQSS (https://vt100.net/docs/vt510-rm/DECRQSS.html)
	//   Request Status String (DECRQSS), VT420 and up.
	// Response: DECRPSS (https://vt100.net/docs/vt510-rm/DECRPSS.html)
	class DECRQSS : IDcsHandler {
		List<byte> data;
		Terminal terminal;

		public DECRQSS (Terminal terminal)
		{
			this.terminal = terminal;
		}

		public void Hook (string collect, int [] parameters, int flag)
		{
			data = new List<byte> ();
		}

		unsafe public void Put (byte* data, int start, int end)
		{
			for (int i = start; i < end; i++)
				this.data.Add (data [i]);
		}

		public void Unhook ()
		{
			var newData = System.Text.Encoding.Default.GetString (data.ToArray ());
			switch (newData) {
			case "\"q": // DECCSA
				terminal.SendResponse ("\x1bP1$r0\"q$\x1b\\");
				return;
			case "\"p": // DECSCL
				terminal.SendResponse ("\x1bP1$r61\"p$\x1b\\");
				return;
			case "r": // DECSTBM
				var pt = "" + (terminal.Buffer.ScrollTop + 1) +
					';' + (terminal.Buffer.ScrollBottom + 1) + 'r';
				terminal.SendResponse ("\x1bP1$r$" + pt + "\x1b\\");
				return;
			case "m": // SGR
				  // TODO: report real settings instead of 0m
				throw new NotImplementedException ();
			default:
				// invalid: DCS 0 $ r Pt ST (xterm)
				terminal.Error ($"Unknown DCS + {newData}");
				terminal.SendResponse ("\x1bP0$r$\x1b");
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
			parser.SetExecuteHandlerFallback ((code) => {
				terminal.Error ("Unknown EXECUTE code", code);
			});
			parser.SetOscHandlerFallback ((int identifier, string data) => {
				terminal.Error ("Unknown OSC code", identifier, data);
			});

			// Print handler
			unsafe { parser.SetPrintHandler (Print); }

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
			parser.SetCsiHandler ('T', (pars, collect) => ScrollDown (pars));
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
			parser.SetExecuteHandler (11, LineFeedBasic);   // VT Vertical Tab - ignores auto-new-line behavior in ConvertEOL
			parser.SetExecuteHandler (12, LineFeedBasic);
			parser.SetExecuteHandler (13, CarriageReturn);
			parser.SetExecuteHandler (8, Backspace);
			parser.SetExecuteHandler (9, Tab);
			parser.SetExecuteHandler (14, ShiftOut);
			parser.SetExecuteHandler (15, ShiftIn);
			// Comment in original FIXME:   What do to with missing? Old code just added those to print.

			// some C1 control codes - FIXME: should those be enabled by default?
			parser.SetExecuteHandler (0x84 /* Index */, () => terminal.Index ());
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
			//   4 - Change Color Number()
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
			parser.SetEscHandler ("D", (c, f) => terminal.Index ());
			parser.SetEscHandler ("E", (c, b) => NextLine ());
			parser.SetEscHandler ("H", (c, f) => TabSet ());
			parser.SetEscHandler ("M", (c, f) => ReverseIndex ());
			parser.SetEscHandler ("=", (c, f) => KeypadApplicationMode ());
			parser.SetEscHandler (">", (c, f) => KeypadNumericMode ());
			parser.SetEscHandler ("c", (c, f) => Reset ());
			parser.SetEscHandler ("n", (c, f) => SetgLevel (2));
			parser.SetEscHandler ("o", (c, f) => SetgLevel (3));
			parser.SetEscHandler ("|", (c, f) => SetgLevel (3));
			parser.SetEscHandler ("}", (c, f) => SetgLevel (2));
			parser.SetEscHandler ("~", (c, f) => SetgLevel (1));
			parser.SetEscHandler ("%@", (c, f) => SelectDefaultCharset ());
			parser.SetEscHandler ("%G", (c, f) => SelectDefaultCharset ());
			parser.SetEscHandler ("#3", (c, f) => SetDoubleHeightTop ());		    // dhtop
			parser.SetEscHandler ("#4", (c, f) => SetDoubleHeightBottom ());            // dhbot
			parser.SetEscHandler ("#5", (c, f) => SingleWidthSingleHeight ());          // swsh
			parser.SetEscHandler ("#6", (c, f) => DoubleWidthSingleHeight ());          // dwsh
			foreach (var bflag in CharSets.All.Keys) {
				char flag = (char)bflag;
				parser.SetEscHandler ("(" + flag, (code, f) => SelectCharset ("(" + flag));
				parser.SetEscHandler (")" + flag, (code, f) => SelectCharset (")" + flag));
				parser.SetEscHandler ("*" + flag, (code, f) => SelectCharset ("*" + flag));
				parser.SetEscHandler ("+" + flag, (code, f) => SelectCharset ("+" + flag));
				parser.SetEscHandler ("-" + flag, (code, f) => SelectCharset ("-" + flag));
				parser.SetEscHandler ("." + flag, (code, f) => SelectCharset ("." + flag));
				parser.SetEscHandler ("/" + flag, (code, f) => SelectCharset ("/" + flag)); // TODO: supported?
			}

			// Error handler
			parser.SetErrorHandler ((state) => {
				terminal.Error ("Parsing error, state: ", state);
				return state;
			});

			// DCS Handler
			parser.SetDcsHandler ("$q", new DECRQSS (terminal));
		}

		public void Parse (byte [] data, int length = -1)
		{
			if (length == -1)
				length = data.Length;

			var buffer = terminal.Buffer;
			var cursorStartX = buffer.X;
			var cursorStartY = buffer.Y;

			unsafe {
				fixed (byte* p = &data [0]) {
					parser.Parse (p, length);
				}
			}

			buffer = terminal.Buffer;
		}

		public void Parse (IntPtr data, int length)
		{
			var buffer = terminal.Buffer;
			var cursorStartX = buffer.X;
			var cursorStartY = buffer.Y;

			unsafe { parser.Parse ((byte*)data, length); }

			buffer = terminal.Buffer;
		}

		// 
		// CSI Ps L
		// Insert Ps Line(s) (default = 1) (IL).
		// 
		private void InsertLines (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;
			var row = buffer.Y + buffer.YBase;

			var scrollBottomRowsOffset = terminal.Rows - 1 - buffer.ScrollBottom;
			var scrollBottomAbsolute = terminal.Rows - 1 + buffer.YBase - scrollBottomRowsOffset + 1;

			var eraseAttr = terminal.EraseAttr ();
			while (p-- != 0) {
				// test: echo -e '\e[44m\e[1L\e[0m'
				// blankLine(true) - xterm/linux behavior
				buffer.Lines.Splice (scrollBottomAbsolute - 1, 1);
				var newLine = buffer.GetBlankLine (eraseAttr);
				buffer.Lines.Splice (row, 0, newLine);
			}

			// this.maxRange();
			terminal.UpdateRange (buffer.Y);
			terminal.UpdateRange (buffer.ScrollBottom);
		}

		// 
		// ESC ( C
		//   Designate G0 Character Set, VT100, ISO 2022.
		// ESC ) C
		//   Designate G1 Character Set (ISO 2022, VT100).
		// ESC * C
		//   Designate G2 Character Set (ISO 2022, VT220).
		// ESC + C
		//   Designate G3 Character Set (ISO 2022, VT220).
		// ESC - C
		//   Designate G1 Character Set (VT300).
		// ESC . C
		//   Designate G2 Character Set (VT300).
		// ESC / C
		//   Designate G3 Character Set (VT300). C = A  -> ISO Latin-1 Supplemental. - Supported?
		// 
		void SelectCharset (string p)
		{
			if (p.Length != 2)
				SelectDefaultCharset ();
			byte ch;

			Dictionary<byte, string> charset;
			if (!CharSets.All.TryGetValue ((byte)p [1], out charset))
				charset = null;

			switch (p [0]) {
			case '(':
				ch = 0;
				break;
			case ')':
			case '-':
				ch = 1;
				break;
			case '*':
			case '.':
				ch = 2;
				break;
			case '+':
				ch = 3;
				break;
			default:
				// includes '/' -> unsupported? (MIGUEL TODO)
				return;
			}
			terminal.SetgCharset (ch, charset);
		}

		//
		// ESC # NUMBER
		//
		void DoubleWidthSingleHeight ()
		{
		}

		//
		// dhtop
		//
		void SetDoubleHeightTop () {}

		// dhbot
		void SetDoubleHeightBottom () { }          // dhbot

		//
		// swsh
		//
		void SingleWidthSingleHeight () { }

		// 
		// ESC % @
		// ESC % G
		//   Select default character set. UTF-8 is not supported (string are unicode anyways)
		//   therefore ESC % G does the same.
		// 
		void SelectDefaultCharset ()
		{
			terminal.SetgLevel (0);
			terminal.SetgCharset (0, CharSets.Default);
		}

		// 
		// ESC n
		// ESC o
		// ESC |
		// ESC }
		// ESC ~
		//   DEC mnemonic: LS (https://vt100.net/docs/vt510-rm/LS.html)
		//   When you use a locking shift, the character set remains in GL or GR until
		//   you use another locking shift. (partly supported)
		// 
		void SetgLevel (int n)
		{
			terminal.SetgLevel (n);
		}

		// 
		// ESC c
		//   DEC mnemonic: RIS (https://vt100.net/docs/vt510-rm/RIS.html)
		//   Reset to initial state.
		// 
		void Reset ()
		{
			parser.Reset ();
			terminal.Reset ();
		}

		// 
		// ESC >
		//   DEC mnemonic: DECKPNM (https://vt100.net/docs/vt510-rm/DECKPNM.html)
		//   Enables the keypad to send numeric characters to the host.
		// 
		void KeypadNumericMode ()
		{
			terminal.ApplicationKeypad = false;
			terminal.SyncScrollArea ();
		}

		// 
		// ESC =
		//   DEC mnemonic: DECKPAM (https://vt100.net/docs/vt510-rm/DECKPAM.html)
		//   Enables the numeric keypad to send application sequences to the host.
		// 
		void KeypadApplicationMode ()
		{
			terminal.ApplicationKeypad = true;
			terminal.SyncScrollArea ();
		}

		// 
		// ESC M
		// C1.RI
		//   DEC mnemonic: HTS
		//   Moves the cursor up one line in the same column. If the cursor is at the top margin,
		//   the page scrolls down.
		// 
		void ReverseIndex ()
		{
			terminal.ReverseIndex ();
		}

		//
		// CSI s
		// ESC 7
		//   Save cursor (ANSI.SYS).
		//
		void RestoreCursor (string collect, int flag)
		{
			var buffer = terminal.Buffer;
			buffer.X = buffer.SavedX;
			buffer.Y = buffer.SavedY;
			terminal.CurAttr = buffer.SavedAttr;
		}

		//
		// CSI s
		// ESC 7
		//   Save cursor (ANSI.SYS).
		//

		void SaveCursor (string collect, int flag)
		{
			var buffer = terminal.Buffer;
			buffer.SavedX = buffer.X;
			buffer.SavedY = buffer.Y;
			buffer.SavedAttr = terminal.CurAttr;
		}

		// 
		// OSC 0; <data> ST (set icon name + window title)
		// OSC 2; <data> ST (set window title)
		//   Proxy to set window title. Icon name is not supported.
		// 
		void SetTitle (string data)
		{
			terminal.SetTitle (data);
		}

		// 
		// ESC H
		// C1.HTS
		//   DEC mnemonic: HTS (https://vt100.net/docs/vt510-rm/HTS.html)
		//   Sets a horizontal tab stop at the column position indicated by
		//   the value of the active column when the terminal receives an HTS.
		// 
		void TabSet ()
		{
			terminal.Buffer.TabSet (terminal.Buffer.X);
		}

		// 
		// ESC E
		// C1.NEL
		//   DEC mnemonic: NEL (https://vt100.net/docs/vt510-rm/NEL)
		//   Moves cursor to first position on next line.
		//   
		void NextLine ()
		{
			terminal.Buffer.X = 0;
			terminal.Index ();
		}

		// SI
		// ShiftIn (Control-O) Switch to standard character set.  This invokes the G0 character set
		void ShiftIn ()
		{
			terminal.SetgLevel (0);
		}

		// SO
		// ShiftOut (Control-N) Switch to alternate character set.  This invokes the G1 character set
		void ShiftOut ()
		{
			terminal.SetgLevel (1);
		}

		//
		// Horizontal tab (Control-I)
		//
		void Tab ()
		{
			var originalX = terminal.Buffer.X;
			terminal.Buffer.X = terminal.Buffer.NextTabStop ();
			if (terminal.Options.ScreenReaderMode)
				terminal.EmitA11yTab (terminal.Buffer.X - originalX);
		}

		// 
		// Backspace handler (Control-h)
		//
		void Backspace ()
		{
			if (terminal.Buffer.X > 0)
				terminal.Buffer.X--;
		}

		void CarriageReturn ()
		{
			terminal.Buffer.X = 0;
		}

		void LineFeed ()
		{
			var buffer = terminal.Buffer;
			if (terminal.Options.ConvertEol)
				buffer.X = 0;
			LineFeedBasic ();
		}

		void LineFeedBasic ()
		{
			var buffer = terminal.Buffer;
			var by = buffer.Y;

			// If we are inside the scroll region, or we hit the last row of the display
			if (by == buffer.ScrollBottom || by == terminal.Rows - 1) {
				terminal.Scroll (isWrapped: false);
			} else
				buffer.Y = by + 1;

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
			var cd = new CharData (terminal.EraseAttr ());
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

		void RestoreCursor (int [] pars)
		{
			var buffer = terminal.Buffer;
			buffer.X = buffer.SavedX;
			buffer.Y = buffer.SavedY;
			terminal.CurAttr = buffer.SavedAttr;
		}

		//
		//  CSI s
		//  ESC 7
		//   Save cursor (ANSI.SYS).
		// 
		void SaveCursor (int [] pars)
		{
			var buffer = terminal.Buffer;
			buffer.SavedX = buffer.X;
			buffer.SavedY = buffer.Y;
			buffer.SavedAttr = terminal.CurAttr;
		}

		// 
		// CSI Ps ; Ps r
		//   Set Scrolling Region [top;bottom] (default = full size of win-
		//   dow) (DECSTBM).
		// CSI ? Pm r
		// 
		void SetScrollRegion (int [] pars, string collect)
		{
			if (collect != "")
				return;
			var buffer = terminal.Buffer;
			buffer.ScrollTop = pars.Length > 0 ? Math.Max (pars [0] - 1, 0) : 0;
			buffer.ScrollBottom = (pars.Length > 1 ? Math.Min (pars [1], terminal.Rows) : terminal.Rows) - 1;
			buffer.X = 0;
			buffer.Y = 0;
		}

		// 
		// CSI Ps SP q  Set cursor style (DECSCUSR, VT520).
		//   Ps = 0  -> blinking block.
		//   Ps = 1  -> blinking block (default).
		//   Ps = 2  -> steady block.
		//   Ps = 3  -> blinking underline.
		//   Ps = 4  -> steady underline.
		//   Ps = 5  -> blinking bar (xterm).
		//   Ps = 6  -> steady bar (xterm).
		// 
		void SetCursorStyle (int [] pars, string collect)
		{
			if (collect != " ")
				return;

			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			switch (p) {
			case 1:
				terminal.SetCursorStyle (CursorStyle.BlinkBlock);
				break;
			case 2:
				terminal.SetCursorStyle (CursorStyle.SteadyBlock);
				break;
			case 3:
				terminal.SetCursorStyle (CursorStyle.BlinkUnderline);
				break;
			case 4:
				terminal.SetCursorStyle (CursorStyle.SteadyUnderline);
				break;
			case 5:
				terminal.SetCursorStyle (CursorStyle.BlinkingBar);
				break;
			case 6:
				terminal.SetCursorStyle (CursorStyle.SteadyBar);
				break;
			}
		}

		// 
		// CSI ! p   Soft terminal reset (DECSTR).
		// http://vt100.net/docs/vt220-rm/table4-10.html
		// 
		void SoftReset (int [] pars, string collect)
		{
			if (collect != "!")
				return;

			terminal.CursorHidden = false;
			terminal.InsertMode = false;
			terminal.OriginMode = false;
			terminal.Wraparound = true;  // defaults: xterm - true, vt100 - false
			terminal.ApplicationKeypad = false; // ?
			terminal.SyncScrollArea ();
			terminal.ApplicationCursor = false;
			terminal.Buffer.ScrollTop = 0;
			terminal.Buffer.ScrollBottom = terminal.Rows - 1;
			terminal.CurAttr = CharData.DefaultAttr;
			terminal.Buffer.X = 0;
			terminal.Buffer.Y = 0;

			terminal.Charset = null;
			terminal.SetgLevel (0);

			// MIGUEL TODO:
			// Should SavedX, SavedY and SavedAttr be reset as well?
		}

		// 
		// CSI Ps n  Device Status Report (DSR).
		//     Ps = 5  -> Status Report.  Result (``OK'') is
		//   CSI 0 n
		//     Ps = 6  -> Report Cursor Position (CPR) [row;column].
		//   Result is
		//   CSI r ; c R
		// CSI ? Ps n
		//   Device Status Report (DSR, DEC-specific).
		//     Ps = 6  -> Report Cursor Position (CPR) [row;column] as CSI
		//     ? r ; c R (assumes page is zero).
		//     Ps = 1 5  -> Report Printer status as CSI ? 1 0  n  (ready).
		//     or CSI ? 1 1  n  (not ready).
		//     Ps = 2 5  -> Report UDK status as CSI ? 2 0  n  (unlocked)
		//     or CSI ? 2 1  n  (locked).
		//     Ps = 2 6  -> Report Keyboard status as
		//   CSI ? 2 7  ;  1  ;  0  ;  0  n  (North American).
		//   The last two parameters apply to VT400 & up, and denote key-
		//   board ready and LK01 respectively.
		//     Ps = 5 3  -> Report Locator status as
		//   CSI ? 5 3  n  Locator available, if compiled-in, or
		//   CSI ? 5 0  n  No Locator, if not.
		// 
		void DeviceStatus (int [] pars, string collect)
		{
			if (collect != "") {
				switch (pars [0]) {
				case 5:
					// status report
					terminal.EmitData ("\x1b[0n");
					break;
				case 6:
					// cursor position
					var y = terminal.Buffer.Y + 1;
					var x = terminal.Buffer.X + 1;
					terminal.EmitData ($"$\x1b[${y};${x}R");
					break;
				}
			} else if (collect == "?") {
				// modern xterm doesnt seem to
				// respond to any of these except ?6, 6, and 5
				switch (pars [0]) {
				case 6:
					// cursor position
					var y = terminal.Buffer.Y + 1;
					var x = terminal.Buffer.X + 1;
					terminal.EmitData ($"$\x1b[?${y};${x}R");
					break;
				case 15:
					// no printer
					// this.handler(C0.ESC + '[?11n');
					break;
				case 25:
					// dont support user defined keys
					// this.handler(C0.ESC + '[?21n');
					break;
				case 26:
					// north american keyboard
					// this.handler(C0.ESC + '[?27;1;0;0n');
					break;
				case 53:
					// no dec locator/mouse
					// this.handler(C0.ESC + '[?50n');
					break;
				}
			}
		}


		// 
		// CSI Pm m  Character Attributes (SGR).
		//     Ps = 0  -> Normal (default).
		//     Ps = 1  -> Bold.
		//     Ps = 2  -> Faint, decreased intensity (ISO 6429).
		//     Ps = 4  -> Underlined.
		//     Ps = 5  -> Blink (appears as Bold).
		//     Ps = 7  -> Inverse.
		//     Ps = 8  -> Invisible, i.e., hidden (VT300).
		//     Ps = 2 2  -> Normal (neither bold nor faint).
		//     Ps = 2 4  -> Not underlined.
		//     Ps = 2 5  -> Steady (not blinking).
		//     Ps = 2 7  -> Positive (not inverse).
		//     Ps = 2 8  -> Visible, i.e., not hidden (VT300).
		//     Ps = 3 0  -> Set foreground color to Black.
		//     Ps = 3 1  -> Set foreground color to Red.
		//     Ps = 3 2  -> Set foreground color to Green.
		//     Ps = 3 3  -> Set foreground color to Yellow.
		//     Ps = 3 4  -> Set foreground color to Blue.
		//     Ps = 3 5  -> Set foreground color to Magenta.
		//     Ps = 3 6  -> Set foreground color to Cyan.
		//     Ps = 3 7  -> Set foreground color to White.
		//     Ps = 3 9  -> Set foreground color to default (original).
		//     Ps = 4 0  -> Set background color to Black.
		//     Ps = 4 1  -> Set background color to Red.
		//     Ps = 4 2  -> Set background color to Green.
		//     Ps = 4 3  -> Set background color to Yellow.
		//     Ps = 4 4  -> Set background color to Blue.
		//     Ps = 4 5  -> Set background color to Magenta.
		//     Ps = 4 6  -> Set background color to Cyan.
		//     Ps = 4 7  -> Set background color to White.
		//     Ps = 4 9  -> Set background color to default (original).
		// 
		//   If 16-color support is compiled, the following apply.  Assume
		//   that xterm's resources are set so that the ISO color codes are
		//   the first 8 of a set of 16.  Then the aixterm colors are the
		//   bright versions of the ISO colors:
		//     Ps = 9 0  -> Set foreground color to Black.
		//     Ps = 9 1  -> Set foreground color to Red.
		//     Ps = 9 2  -> Set foreground color to Green.
		//     Ps = 9 3  -> Set foreground color to Yellow.
		//     Ps = 9 4  -> Set foreground color to Blue.
		//     Ps = 9 5  -> Set foreground color to Magenta.
		//     Ps = 9 6  -> Set foreground color to Cyan.
		//     Ps = 9 7  -> Set foreground color to White.
		//     Ps = 1 0 0  -> Set background color to Black.
		//     Ps = 1 0 1  -> Set background color to Red.
		//     Ps = 1 0 2  -> Set background color to Green.
		//     Ps = 1 0 3  -> Set background color to Yellow.
		//     Ps = 1 0 4  -> Set background color to Blue.
		//     Ps = 1 0 5  -> Set background color to Magenta.
		//     Ps = 1 0 6  -> Set background color to Cyan.
		//     Ps = 1 0 7  -> Set background color to White.
		// 
		//   If xterm is compiled with the 16-color support disabled, it
		//   supports the following, from rxvt:
		//     Ps = 1 0 0  -> Set foreground and background color to
		//     default.
		// 
		//   If 88- or 256-color support is compiled, the following apply.
		//     Ps = 3 8  ; 5  ; Ps -> Set foreground color to the second
		//     Ps.
		//     Ps = 4 8  ; 5  ; Ps -> Set background color to the second
		//     Ps.
		// 
		void CharAttributes (int [] pars)
		{
			// Optimize a single SGR0.
			if (pars.Length == 1 && pars [0] == 0) {
				terminal.CurAttr = CharData.DefaultAttr;
				return;
			}

			var l = pars.Length;
			var flags = (FLAGS)(terminal.CurAttr >> 18);
			var fg = (terminal.CurAttr >> 9) & 0x1ff;
			var bg = terminal.CurAttr & 0x1ff;
			var def = CharData.DefaultAttr;

			for (var i = 0; i < l; i++) {
				int p = pars [i];
				if (p >= 30 && p <= 37) {
					// fg color 8
					fg = p - 30;
				} else if (p >= 40 && p <= 47) {
					// bg color 8
					bg = p - 40;
				} else if (p >= 90 && p <= 97) {
					// fg color 16
					p += 8;
					fg = p - 90;
				} else if (p >= 100 && p <= 107) {
					// bg color 16
					p += 8;
					bg = p - 100;
				} else if (p == 0) {
					// default

					flags = (FLAGS)(def >> 18);
					fg = (def >> 9) & 0x1ff;
					bg = def & 0x1ff;
					// flags = 0;
					// fg = 0x1ff;
					// bg = 0x1ff;
				} else if (p == 1) {
					// bold text
					flags |= FLAGS.BOLD;
				} else if (p == 3) {
					// italic text
					flags |= FLAGS.ITALIC;
				} else if (p == 4) {
					// underlined text
					flags |= FLAGS.UNDERLINE;
				} else if (p == 5) {
					// blink
					flags |= FLAGS.BLINK;
				} else if (p == 7) {
					// inverse and positive
					// test with: echo -e '\e[31m\e[42mhello\e[7mworld\e[27mhi\e[m'
					flags |= FLAGS.INVERSE;
				} else if (p == 8) {
					// invisible
					flags |= FLAGS.INVISIBLE;
				} else if (p == 2) {
					// dimmed text
					flags |= FLAGS.DIM;
				} else if (p == 22) {
					// not bold nor faint
					flags &= ~FLAGS.BOLD;
					flags &= ~FLAGS.DIM;
				} else if (p == 23) {
					// not italic
					flags &= ~FLAGS.ITALIC;
				} else if (p == 24) {
					// not underlined
					flags &= ~FLAGS.UNDERLINE;
				} else if (p == 25) {
					// not blink
					flags &= ~FLAGS.BLINK;
				} else if (p == 27) {
					// not inverse
					flags &= ~FLAGS.INVERSE;
				} else if (p == 28) {
					// not invisible
					flags &= ~FLAGS.INVISIBLE;
				} else if (p == 39) {
					// reset fg
					fg = (CharData.DefaultAttr >> 9) & 0x1ff;
				} else if (p == 49) {
					// reset bg
					bg = CharData.DefaultAttr & 0x1ff;
				} else if (p == 38) {
					// fg color 256
					if (pars [i + 1] == 2) {
						i += 2;
						fg = terminal.MatchColor (
							pars [i] & 0xff,
							pars [i + 1] & 0xff,
							pars [i + 2] & 0xff);
						if (fg == -1)
							fg = 0x1ff;
						i += 2;
					} else if (pars [i + 1] == 5) {
						i += 2;
						p = pars [i] & 0xff;
						fg = p;
					}
				} else if (p == 48) {
					// bg color 256
					if (pars [i + 1] == 2) {
						i += 2;
						bg = terminal.MatchColor (
							pars [i] & 0xff,
							pars [i + 1] & 0xff,
							pars [i + 2] & 0xff);
						if (bg == -1)
							bg = 0x1ff;
						i += 2;
					} else if (pars [i + 1] == 5) {
						i += 2;
						p = pars [i] & 0xff;
						bg = p;
					}
				} else if (p == 100) {
					// reset fg/bg
					fg = (def >> 9) & 0x1ff;
					bg = def & 0x1ff;
				} else {
					terminal.Error ("Unknown SGR attribute: %d.", p);
				}
			}
			terminal.CurAttr = ((int)flags << 18) | (fg << 9) | bg;
		}

		//
		//CSI Pm l  Reset Mode (RM).
		//    Ps = 2  -> Keyboard Action Mode (AM).
		//    Ps = 4  -> Replace Mode (IRM).
		//    Ps = 1 2  -> Send/receive (SRM).
		//    Ps = 2 0  -> Normal Linefeed (LNM).
		//CSI ? Pm l
		//  DEC Private Mode Reset (DECRST).
		//    Ps = 1  -> Normal Cursor Keys (DECCKM).
		//    Ps = 2  -> Designate VT52 mode (DECANM).
		//    Ps = 3  -> 80 Column Mode (DECCOLM).
		//    Ps = 4  -> Jump (Fast) Scroll (DECSCLM).
		//    Ps = 5  -> Normal Video (DECSCNM).
		//    Ps = 6  -> Normal Cursor Mode (DECOM).
		//    Ps = 7  -> No Wraparound Mode (DECAWM).
		//    Ps = 8  -> No Auto-repeat Keys (DECARM).
		//    Ps = 9  -> Don't send Mouse X & Y on button press.
		//    Ps = 1 0  -> Hide toolbar (rxvt).
		//    Ps = 1 2  -> Stop Blinking Cursor (att610).
		//    Ps = 1 8  -> Don't print form feed (DECPFF).
		//    Ps = 1 9  -> Limit print to scrolling region (DECPEX).
		//    Ps = 2 5  -> Hide Cursor (DECTCEM).
		//    Ps = 3 0  -> Don't show scrollbar (rxvt).
		//    Ps = 3 5  -> Disable font-shifting functions (rxvt).
		//    Ps = 4 0  -> Disallow 80 -> 132 Mode.
		//    Ps = 4 1  -> No more(1) fix (see curses resource).
		//    Ps = 4 2  -> Disable Nation Replacement Character sets (DEC-
		//    NRCM).
		//    Ps = 4 4  -> Turn Off Margin Bell.
		//    Ps = 4 5  -> No Reverse-wraparound Mode.
		//    Ps = 4 6  -> Stop Logging.  (This is normally disabled by a
		//    compile-time option).
		//    Ps = 4 7  -> Use Normal Screen Buffer.
		//    Ps = 6 6  -> Numeric keypad (DECNKM).
		//    Ps = 6 7  -> Backarrow key sends delete (DECBKM).
		//    Ps = 1 0 0 0  -> Don't send Mouse X & Y on button press and
		//    release.  See the section Mouse Tracking.
		//    Ps = 1 0 0 1  -> Don't use Hilite Mouse Tracking.
		//    Ps = 1 0 0 2  -> Don't use Cell Motion Mouse Tracking.
		//    Ps = 1 0 0 3  -> Don't use All Motion Mouse Tracking.
		//    Ps = 1 0 0 4  -> Don't send FocusIn/FocusOut events.
		//    Ps = 1 0 0 5  -> Disable Extended Mouse Mode.
		//    Ps = 1 0 1 0  -> Don't scroll to bottom on tty output
		//    (rxvt).
		//    Ps = 1 0 1 1  -> Don't scroll to bottom on key press (rxvt).
		//    Ps = 1 0 3 4  -> Don't interpret "meta" key.  (This disables
		//    the eightBitInput resource).
		//    Ps = 1 0 3 5  -> Disable special modifiers for Alt and Num-
		//    Lock keys.  (This disables the numLock resource).
		//    Ps = 1 0 3 6  -> Don't send ESC  when Meta modifies a key.
		//    (This disables the metaSendsEscape resource).
		//    Ps = 1 0 3 7  -> Send VT220 Remove from the editing-keypad
		//    Delete key.
		//    Ps = 1 0 3 9  -> Don't send ESC  when Alt modifies a key.
		//    (This disables the altSendsEscape resource).
		//    Ps = 1 0 4 0  -> Do not keep selection when not highlighted.
		//    (This disables the keepSelection resource).
		//    Ps = 1 0 4 1  -> Use the PRIMARY selection.  (This disables
		//    the selectToClipboard resource).
		//    Ps = 1 0 4 2  -> Disable Urgency window manager hint when
		//    Control-G is received.  (This disables the bellIsUrgent
		//    resource).
		//    Ps = 1 0 4 3  -> Disable raising of the window when Control-
		//    G is received.  (This disables the popOnBell resource).
		//    Ps = 1 0 4 7  -> Use Normal Screen Buffer, clearing screen
		//    first if in the Alternate Screen.  (This may be disabled by
		//    the titeInhibit resource).
		//    Ps = 1 0 4 8  -> Restore cursor as in DECRC.  (This may be
		//    disabled by the titeInhibit resource).
		//    Ps = 1 0 4 9  -> Use Normal Screen Buffer and restore cursor
		//    as in DECRC.  (This may be disabled by the titeInhibit
		//    resource).  This combines the effects of the 1 0 4 7  and 1 0
		//    4 8  modes.  Use this with terminfo-based applications rather
		//    than the 4 7  mode.
		//    Ps = 1 0 5 0  -> Reset terminfo/termcap function-key mode.
		//    Ps = 1 0 5 1  -> Reset Sun function-key mode.
		//    Ps = 1 0 5 2  -> Reset HP function-key mode.
		//    Ps = 1 0 5 3  -> Reset SCO function-key mode.
		//    Ps = 1 0 6 0  -> Reset legacy keyboard emulation (X11R6).
		//    Ps = 1 0 6 1  -> Reset keyboard emulation to Sun/PC style.
		//    Ps = 2 0 0 4  -> Reset bracketed paste mode.
		//
		void ResetMode (int [] pars, string collect)
		{
			if (pars.Length == 0)
				return;

			if (pars.Length > 1) {
				for (var i = 0; i < pars.Length; i++)
					ResetMode (pars [i], "");


				return;
			}
			ResetMode (pars [0], collect);
		}

		void ResetMode (int par, string collect)
		{
			if (collect == "") {
				switch (par) {
				case 4:
					terminal.InsertMode = false;
					break;
				case 20:
					// this._t.convertEol = false;
					break;
				}
			} else if (collect == "?") {
				switch (par) {
				case 1:
					terminal.ApplicationCursor = false;
					break;
				case 3:
					if (terminal.Cols == 132 && terminal.SavedCols != 0)
						terminal.Resize (terminal.SavedCols, terminal.Rows);

					terminal.SavedCols = 0;
					break;
				case 5:
					// Reset default color
					terminal.CurAttr = CharData.DefaultAttr;
					break;
				case 6:
					terminal.OriginMode = false;
					break;
				case 7:
					terminal.Wraparound = false;
					break;
				case 12:
					// this.cursorBlink = false;
					break;
				case 66:
					terminal.Log ("Switching back to normal keypad.");
					terminal.ApplicationKeypad = false;
					terminal.SyncScrollArea ();
					break;
				case 9: // X10 Mouse
					terminal.MouseEvents = false;
					break;
				case 1000: // vt200 mouse
					terminal.MouseEvents = false;
					break;
				case 1002: // button event mouse
					terminal.MouseSendsMotionWhenPressed = false;
					break;
				case 1003: // any event mouse
					terminal.MouseSendsAllMotion = false;
					break;
				case 1004: // send focusin/focusout events
					terminal.SendFocus = false;
					break;
				case 1005: // utf8 ext mode mouse
					terminal.UtfMouse = false;
					break;
				case 1006: // sgr ext mode mouse
					terminal.SgrMouse = false;
					break;
				case 1015: // urxvt ext mode mouse
					terminal.UrxvtMouse = false;
					break;
				case 25: // hide cursor
					terminal.CursorHidden = true;
					break;
				case 1048: // alt screen cursor
					this.RestoreCursor (Array.Empty<int> ());
					break;
				case 1049: // alt screen buffer cursor
					   // FALL-THROUGH
					goto case 47;
				case 47: // normal screen buffer
				case 1047: // normal screen buffer - clearing it first
					   // Ensure the selection manager has the correct buffer
					terminal.Buffers.ActivateNormalBuffer ();
					if (par == 1049)
						this.RestoreCursor (Array.Empty<int> ());
					terminal.Refresh (0, terminal.Rows - 1);
					terminal.SyncScrollArea ();
					terminal.ShowCursor ();
					break;
				case 2004: // bracketed paste mode (https://cirw.in/blog/bracketed-paste)
					terminal.BracketedPasteMode = false;
					break;
				}
			}
		}

		// 
		// CSI Pm h  Set Mode (SM).
		//     Ps = 2  -> Keyboard Action Mode (AM).
		//     Ps = 4  -> Insert Mode (IRM).
		//     Ps = 1 2  -> Send/receive (SRM).
		//     Ps = 2 0  -> Automatic Newline (LNM).
		// CSI ? Pm h
		//   DEC Private Mode Set (DECSET).
		//     Ps = 1  -> Application Cursor Keys (DECCKM).
		//     Ps = 2  -> Designate USASCII for character sets G0-G3
		//     (DECANM), and set VT100 mode.
		//     Ps = 3  -> 132 Column Mode (DECCOLM).
		//     Ps = 4  -> Smooth (Slow) Scroll (DECSCLM).
		//     Ps = 5  -> Reverse Video (DECSCNM).
		//     Ps = 6  -> Origin Mode (DECOM).
		//     Ps = 7  -> Wraparound Mode (DECAWM).
		//     Ps = 8  -> Auto-repeat Keys (DECARM).
		//     Ps = 9  -> Send Mouse X & Y on button press.  See the sec-
		//     tion Mouse Tracking.
		//     Ps = 1 0  -> Show toolbar (rxvt).
		//     Ps = 1 2  -> Start Blinking Cursor (att610).
		//     Ps = 1 8  -> Print form feed (DECPFF).
		//     Ps = 1 9  -> Set print extent to full screen (DECPEX).
		//     Ps = 2 5  -> Show Cursor (DECTCEM).
		//     Ps = 3 0  -> Show scrollbar (rxvt).
		//     Ps = 3 5  -> Enable font-shifting functions (rxvt).
		//     Ps = 3 8  -> Enter Tektronix Mode (DECTEK).
		//     Ps = 4 0  -> Allow 80 -> 132 Mode.
		//     Ps = 4 1  -> more(1) fix (see curses resource).
		//     Ps = 4 2  -> Enable Nation Replacement Character sets (DECN-
		//     RCM).
		//     Ps = 4 4  -> Turn On Margin Bell.
		//     Ps = 4 5  -> Reverse-wraparound Mode.
		//     Ps = 4 6  -> Start Logging.  This is normally disabled by a
		//     compile-time option.
		//     Ps = 4 7  -> Use Alternate Screen Buffer.  (This may be dis-
		//     abled by the titeInhibit resource).
		//     Ps = 6 6  -> Application keypad (DECNKM).
		//     Ps = 6 7  -> Backarrow key sends backspace (DECBKM).
		//     Ps = 1 0 0 0  -> Send Mouse X & Y on button press and
		//     release.  See the section Mouse Tracking.
		//     Ps = 1 0 0 1  -> Use Hilite Mouse Tracking.
		//     Ps = 1 0 0 2  -> Use Cell Motion Mouse Tracking.
		//     Ps = 1 0 0 3  -> Use All Motion Mouse Tracking.
		//     Ps = 1 0 0 4  -> Send FocusIn/FocusOut events.
		//     Ps = 1 0 0 5  -> Enable Extended Mouse Mode.
		//     Ps = 1 0 1 0  -> Scroll to bottom on tty output (rxvt).
		//     Ps = 1 0 1 1  -> Scroll to bottom on key press (rxvt).
		//     Ps = 1 0 3 4  -> Interpret "meta" key, sets eighth bit.
		//     (enables the eightBitInput resource).
		//     Ps = 1 0 3 5  -> Enable special modifiers for Alt and Num-
		//     Lock keys.  (This enables the numLock resource).
		//     Ps = 1 0 3 6  -> Send ESC   when Meta modifies a key.  (This
		//     enables the metaSendsEscape resource).
		//     Ps = 1 0 3 7  -> Send DEL from the editing-keypad Delete
		//     key.
		//     Ps = 1 0 3 9  -> Send ESC  when Alt modifies a key.  (This
		//     enables the altSendsEscape resource).
		//     Ps = 1 0 4 0  -> Keep selection even if not highlighted.
		//     (This enables the keepSelection resource).
		//     Ps = 1 0 4 1  -> Use the CLIPBOARD selection.  (This enables
		//     the selectToClipboard resource).
		//     Ps = 1 0 4 2  -> Enable Urgency window manager hint when
		//     Control-G is received.  (This enables the bellIsUrgent
		//     resource).
		//     Ps = 1 0 4 3  -> Enable raising of the window when Control-G
		//     is received.  (enables the popOnBell resource).
		//     Ps = 1 0 4 7  -> Use Alternate Screen Buffer.  (This may be
		//     disabled by the titeInhibit resource).
		//     Ps = 1 0 4 8  -> Save cursor as in DECSC.  (This may be dis-
		//     abled by the titeInhibit resource).
		//     Ps = 1 0 4 9  -> Save cursor as in DECSC and use Alternate
		//     Screen Buffer, clearing it first.  (This may be disabled by
		//     the titeInhibit resource).  This combines the effects of the 1
		//     0 4 7  and 1 0 4 8  modes.  Use this with terminfo-based
		//     applications rather than the 4 7  mode.
		//     Ps = 1 0 5 0  -> Set terminfo/termcap function-key mode.
		//     Ps = 1 0 5 1  -> Set Sun function-key mode.
		//     Ps = 1 0 5 2  -> Set HP function-key mode.
		//     Ps = 1 0 5 3  -> Set SCO function-key mode.
		//     Ps = 1 0 6 0  -> Set legacy keyboard emulation (X11R6).
		//     Ps = 1 0 6 1  -> Set VT220 keyboard emulation.
		//     Ps = 2 0 0 4  -> Set bracketed paste mode.
		// Modes:
		//   http: *vt100.net/docs/vt220-rm/chapter4.html
		// 
		void SetMode (int [] pars, string collect)
		{
			if (pars.Length == 0)
				return;

			if (pars.Length > 1) {
				for (var i = 0; i < pars.Length; i++)
					SetMode (pars [i], "");


				return;
			}
			SetMode (pars [0], collect);
		}

		void SetMode (int par, string collect)
		{
			if (collect == "") {
				switch (par) {
				case 4:
					//Console.WriteLine ("This needs to handle the replace mode as well");
					// https://vt100.net/docs/vt510-rm/IRM.html
					terminal.InsertMode = true;
					break;
				case 20:

					// Automatic New Line (LNM)
					// this._t.convertEol = true;
					break;
				}
			} else if (collect == "?") {
				switch (par) {
				case 1:
					terminal.ApplicationCursor = true;
					break;
				case 2:
					terminal.SetgCharset (0, CharSets.Default);
					terminal.SetgCharset (1, CharSets.Default);
					terminal.SetgCharset (2, CharSets.Default);
					terminal.SetgCharset (3, CharSets.Default);
					// set VT100 mode here
					break;
				case 3: // 132 col mode
					terminal.SavedCols = terminal.Cols;
					terminal.Resize (132, terminal.Rows);
					break;
				case 5:
					// Inverted colors
					terminal.CurAttr = CharData.InvertedAttr;  
					break;
				case 6:
					terminal.OriginMode = true;
					break;
				case 7:
					terminal.Wraparound = true;
					break;
				case 12:
					// this.cursorBlink = true;
					break;
				case 66:
					terminal.Log ("Serial port requested application keypad.");
					terminal.ApplicationKeypad = true;
					terminal.SyncScrollArea ();
					break;
				case 9: // X10 Mouse
					// no release, no motion, no wheel, no modifiers.
					terminal.SetX10MouseStyle ();
					break;
				case 1000: // vt200 mouse
					   // no motion.
					   // no modifiers, except control on the wheel.
					terminal.SetVT200MouseStyle ();
					break;
				case 1002:
					// SET_BTN_EVENT_MOUSE
					terminal.MouseSendsMotionWhenPressed = true;
					break;

				case 1003:
					// SET_ANY_EVENT_MOUSE 
					terminal.MouseSendsAllMotion = true;
					break;

				case 1004: // send focusin/focusout events
					   // focusin: ^[[I
					   // focusout: ^[[O
					terminal.SendFocus = true;
					break;
				case 1005: // utf8 ext mode mouse
					   // for wide terminals
					   // simply encodes large values as utf8 characters
					terminal.UtfMouse = true;
					break;
				case 1006: // sgr ext mode mouse
					terminal.SetVT200MouseStyle ();
					terminal.SgrMouse = true;
					// for wide terminals
					// does not add 32 to fields
					// press: ^[[<b;x;yM
					// release: ^[[<b;x;ym
					break;
				case 1015: // urxvt ext mode mouse
					terminal.SetVT200MouseStyle ();

					terminal.UrxvtMouse = true;
					// for wide terminals
					// numbers for fields
					// press: ^[[b;x;yM
					// motion: ^[[b;x;yT
					break;
				case 25: // show cursor
					terminal.CursorHidden = false;
					break;
				case 1048: // alt screen cursor

					SaveCursor (Array.Empty<int> ());
					break;
				case 1049: // alt screen buffer cursor
					SaveCursor (Array.Empty<int> ());
					// FALL-THROUGH
					goto case 47;
				case 47: // alt screen buffer
				case 1047: // alt screen buffer
					terminal.Buffers.ActivateAltBuffer (terminal.EraseAttr ());
					terminal.Refresh (0, terminal.Rows - 1);
					terminal.SyncScrollArea ();
					terminal.ShowCursor ();
					break;
				case 2004: // bracketed paste mode (https://cirw.in/blog/bracketed-paste)
					terminal.BracketedPasteMode = true;
					break;
				}
			}
		}


		// 
		// CSI Ps g  Tab Clear (TBC).
		//     Ps = 0  -> Clear Current Column (default).
		//     Ps = 3  -> Clear All.
		// Potentially:
		//   Ps = 2  -> Clear Stops on Line.
		//   http://vt100.net/annarbor/aaa-ug/section6.html
		// 
		void TabClear (int [] pars)
		{
			var p = pars.Length == 0 ? 0 : pars [0];
			var buffer = terminal.Buffer;
			if (p == 0)
				buffer.ClearStop (buffer.X);
			else if (p == 3)
				buffer.ClearTabStops ();
		}

		//
		// CSI Ps ; Ps f
		//   Horizontal and Vertical Position [row;column] (default =
		//   [1,1]) (HVP).
		//
		void HVPosition (int [] pars)
		{
			int p = 1;
			int q = 1;
			if (pars.Length > 0) {
				p = Math.Max (pars [0], 1);
				if (pars.Length > 1)
					q = Math.Max (pars [0], 1);
			}
			var buffer = terminal.Buffer;
			buffer.Y = p - 1;
			if (buffer.Y >= terminal.Rows)
				buffer.Y = terminal.Rows - 1;

			buffer.X = q - 1;
			if (buffer.X >= terminal.Cols)
				buffer.X = terminal.Cols - 1;
		}

		// 
		// CSI Pm e  Vertical Position Relative (VPR)
		//   [rows] (default = [row+1,column])
		// reuse CSI Ps B ?
		// 
		void VPositionRelative (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			var newY = buffer.Y + p;

			if (newY >= terminal.Rows) {
				buffer.Y = terminal.Rows - 1;
			} else
				buffer.Y = newY;

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols)
				buffer.X--;
		}

		// 
		// CSI Pm d  Vertical Position Absolute (VPA)
		//   [row] (default = [1,column])
		// 
		void LinePosAbsolute (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			if (p - 1 >= terminal.Rows)
				buffer.Y = terminal.Rows - 1;
			else
				buffer.Y = p - 1;
		}

		// 
		// CSI Ps c  Send Device Attributes (Primary DA).
		//     Ps = 0  or omitted -> request attributes from terminal.  The
		//     response depends on the decTerminalID resource setting.
		//     -> CSI ? 1 ; 2 c  (``VT100 with Advanced Video Option'')
		//     -> CSI ? 1 ; 0 c  (``VT101 with No Options'')
		//     -> CSI ? 6 c  (``VT102'')
		//     -> CSI ? 6 0 ; 1 ; 2 ; 6 ; 8 ; 9 ; 1 5 ; c  (``VT220'')
		//   The VT100-style response parameters do not mean anything by
		//   themselves.  VT220 parameters do, telling the host what fea-
		//   tures the terminal supports:
		//     Ps = 1  -> 132-columns.
		//     Ps = 2  -> Printer.
		//     Ps = 6  -> Selective erase.
		//     Ps = 8  -> User-defined keys.
		//     Ps = 9  -> National replacement character sets.
		//     Ps = 1 5  -> Technical characters.
		//     Ps = 2 2  -> ANSI color, e.g., VT525.
		//     Ps = 2 9  -> ANSI text locator (i.e., DEC Locator mode).
		// CSI > Ps c
		//   Send Device Attributes (Secondary DA).
		//     Ps = 0  or omitted -> request the terminal's identification
		//     code.  The response depends on the decTerminalID resource set-
		//     ting.  It should apply only to VT220 and up, but xterm extends
		//     this to VT100.
		//     -> CSI  > Pp ; Pv ; Pc c
		//   where Pp denotes the terminal type
		//     Pp = 0  -> ``VT100''.
		//     Pp = 1  -> ``VT220''.
		//   and Pv is the firmware version (for xterm, this was originally
		//   the XFree86 patch number, starting with 95).  In a DEC termi-
		//   nal, Pc indicates the ROM cartridge registration number and is
		//   always zero.
		// More information:
		//   xterm/charproc.c - line 2012, for more information.
		//   vim responds with ^[[?0c or ^[[?1c after the terminal's response (?)
		// 
		void SendDeviceAttributes (int [] pars, string collect)
		{
			if (pars.Length > 0 && pars [0] > 0)
				return;

			var name = terminal.Options.TermName;
			if (collect == "") {
				if (name.StartsWith ("xterm", StringComparison.Ordinal) || name.StartsWith ("rxvt-unicode", StringComparison.Ordinal) || name.StartsWith ("screen", StringComparison.Ordinal)) {
					terminal.SendResponse ("\x1b[?1;2c");
				} else if (name.StartsWith ("linux", StringComparison.Ordinal)) {
					terminal.SendResponse ("\x1b[?6c");
				}
			} else if (collect == ">") {
				// xterm and urxvt
				// seem to spit this
				// out around ~370 times (?).
				if (name.StartsWith ("xterm", StringComparison.Ordinal)) {
					terminal.SendResponse ("\x1b[>0;276;0c");
				} else if (name.StartsWith ("rxvt-unicode", StringComparison.Ordinal)) {
					terminal.SendResponse ("\x1b[>85;95;0c");
				} else if (name.StartsWith ("linux", StringComparison.Ordinal)) {
					// not supported by linux console.
					// linux console echoes parameters.
					terminal.SendResponse ("" + pars [0] + 'c');
				} else if (name.StartsWith ("screen", StringComparison.Ordinal)) {
					terminal.SendResponse ("\x1b[>83;40003;0c");
				}
			}
		}

		// 
		// CSI Ps b  Repeat the preceding graphic character Ps times (REP).
		//
		void RepeatPrecedingCharacter (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);

			var buffer = terminal.Buffer;
			var line = buffer.Lines [buffer.YBase + buffer.Y];
			CharData cd = buffer.X - 1 < 0 ? new CharData (CharData.DefaultAttr) : line [buffer.X - 1];
			line.ReplaceCells (buffer.X,
				  buffer.X + p,
				      cd);
			// FIXME: no UpdateRange here?
		}

		//
		//CSI Pm a  Character Position Relative
		//  [columns] (default = [row,col+1]) (HPR)
		//reuse CSI Ps C ?
		//
		void HPositionRelative (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			buffer.X += p;
			if (buffer.X >= terminal.Cols)
				buffer.X = terminal.Cols - 1;
		}

		// 
		// CSI Pm `  Character Position Absolute
		//   [column] (default = [row,1]) (HPA).
		// 
		void CharPosAbsolute (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			buffer.X = p - 1;
			if (buffer.X >= terminal.Cols)
				buffer.X = terminal.Cols - 1;
		}

		//
		//CSI Ps Z  Cursor Backward Tabulation Ps tab stops (default = 1) (CBT).
		//
		void CursorBackwardTab (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);

			var buffer = terminal.Buffer;
			while (p-- != 0) {
				buffer.X = buffer.PreviousTabStop ();
			}
		}

		// 
		// CSI Ps X
		// Erase Ps Character(s) (default = 1) (ECH).
		// 
		void EraseChars (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);

			var buffer = terminal.Buffer;
			buffer.Lines [buffer.Y + buffer.YBase].ReplaceCells (
				  buffer.X,
				  buffer.X + p,
				new CharData (terminal.EraseAttr ()));
		}

		// 
		// CSI Ps T  Scroll down Ps lines (default = 1) (SD).
		// 
		void ScrollDown (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			while (p-- != 0) {
				buffer.Lines.Splice (buffer.YBase + buffer.ScrollBottom, 1);
				buffer.Lines.Splice (buffer.YBase + buffer.ScrollBottom, 0, buffer.GetBlankLine (CharData.DefaultAttr));
			}
			// this.maxRange();
			terminal.UpdateRange (buffer.ScrollTop);
			terminal.UpdateRange (buffer.ScrollBottom);

		}

		// 
		// CSI Ps S  Scroll up Ps lines (default = 1) (SU).
		// 
		void ScrollUp (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			while (p-- != 0) {
				buffer.Lines.Splice (buffer.YBase + buffer.ScrollTop, 1);
				buffer.Lines.Splice (buffer.YBase + buffer.ScrollBottom, 0, buffer.GetBlankLine (CharData.DefaultAttr));
			}
			// this.maxRange();
			terminal.UpdateRange (buffer.ScrollTop);
			terminal.UpdateRange (buffer.ScrollBottom);
		}

		// 
		// CSI Ps P
		// Delete Ps Character(s) (default = 1) (DCH).
		// 
		void DeleteChars (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;

			buffer.Lines [buffer.Y + buffer.YBase].DeleteCells (
			  buffer.X, p, new CharData (terminal.EraseAttr ()));

			terminal.UpdateRange (buffer.Y);
		}

		// 
		// CSI Ps M
		// Delete Ps Line(s) (default = 1) (DL).
		// 
		void DeleteLines (int [] pars)
		{
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);
			var buffer = terminal.Buffer;
			var row = buffer.Y + buffer.YBase;
			int j;
			j = terminal.Rows - 1 - buffer.ScrollBottom;
			j = terminal.Rows - 1 + buffer.YBase - j;
			var eraseAttr = terminal.EraseAttr ();
			while (p-- != 0) {
				// test: echo -e '\e[44m\e[1M\e[0m'
				// blankLine(true) - xterm/linux behavior
				buffer.Lines.Splice (row, 1);
				buffer.Lines.Splice (j, 0, buffer.GetBlankLine (eraseAttr));
			}

			// this.maxRange();
			terminal.UpdateRange (buffer.Y);
			terminal.UpdateRange (buffer.ScrollBottom);

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
			switch (p) {
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
			terminal.UpdateRange (buffer.Y);
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
				terminal.UpdateRange (j - 1);
				break;
			case 1:
				j = buffer.Y;
				terminal.UpdateRange (j);
				// Deleted front part of line and everything before. This line will no longer be wrapped.
				EraseInBufferLine (j, 0, buffer.X + 1, true);
				if (buffer.X + 1 >= terminal.Cols) {
					// Deleted entire previous line. This next line can no longer be wrapped.
					buffer.Lines [j + 1].IsWrapped = false;
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
			var newY = buffer.Y - param;
			if (newY < 0)
				buffer.Y = 0;
			else
				buffer.Y = newY;
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

			var newY = buffer.Y + param;

			if (newY >= terminal.Rows)
				buffer.Y = terminal.Rows - 1;
			else
				buffer.Y = newY;

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

			var newY = buffer.Y + param;

			// review
			//if (buffer.Y > buffer.ScrollBottom)
			//	buffer.Y = buffer.ScrollBottom - 1;
			if (newY >= terminal.Rows)
				buffer.Y = terminal.Rows - 1;
			else
				buffer.Y = newY;

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols)
				buffer.X--;
		}

		void SetCursor (int x, int y)
		{
			if (terminal.OriginMode) {
				terminal.Buffer.X = x;
				terminal.Buffer.Y = terminal.Buffer.ScrollTop + y;
			} else {
				terminal.Buffer.X = x;
				terminal.Buffer.Y = y;
			}
		}

		// 
		// CSI Ps A
		// Cursor Up Ps Times (default = 1) (CUU).
		// 
		void CursorUp (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			if (buffer.Y - param < 0)
				buffer.Y = 0;
			else
				buffer.Y -= param;
		}

		//
		// CSI Ps @
		// Insert Ps (Blank) Character(s) (default = 1) (ICH).
		//
		void InsertChars (int [] pars)
		{
			var buffer = terminal.Buffer;
			var cd = new CharData (terminal.EraseAttr ());

			buffer.Lines [buffer.Y + buffer.YBase].InsertCells (
				  buffer.X,
				  pars.Length > 0 ? pars [0] : 1,
				    cd);

			terminal.UpdateRange (buffer.Y);
		}

		unsafe void Print (byte* data, int start, int end)
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
				int code;
				var n = RuneExt.ExpectedSizeFromFirstByte (data [pos]);
				if (n == -1) {
					// Invalid UTF-8 sequence, client sent us some junk, happens if we run with the wrong locale set
					// for example if LANG=en
					code = (int)((uint)data [pos]);
				} else if (n == 1)
					code = data [pos];
				else if (pos + n <= end) {
					var x = new byte [n];
					for (int j = 0; j < n; j++)
						x [j] = data [pos++];
					(var r, var size) = Rune.DecodeRune (x);
					code = (int)(uint)r;
					pos--;
				} else {
					// Alternative: keep a buffer here that can be cleared on Reset(), and use that to process the data on partial inputs
					Console.WriteLine ("Partial data, need to tell the caller that a partial UTF-8 string was received and process later");
					return;
				}

				// MIGUEL-TODO: I suspect this needs to be a stirng in C# to cope with Grapheme clusters
				var ch = code;

				// calculate print space
				// expensive call, therefore we save width in line buffer

				// TODO: This is wrong, we only have one byte at this point, we do not have a full rune.
				// The correct fix includes the upper parser tracking the "pending" data across invocations
				// until a valid UTF-8 string comes in, and *then* we can call this method
				// var chWidth = Rune.ColumnWidth ((Rune)code);

				// 1 until we get a fixed NStack
				var chWidth = 1;

				// get charset replacement character
				// charset are only defined for ASCII, therefore we only
				// search for an replacement char if code < 127
				if (code < 127 && charset != null) {

					// MIGUEL-FIXME - this is broken for dutch cahrset that returns two letters "ij", need to figure out what to do
					if (charset.TryGetValue ((byte)code, out var str)) {
						ch = str [0];
						code = ch;
					}
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
							if (buffer.X >= 2) {
								var chMinusTwo = bufferRow [buffer.X - 2];

								chMinusTwo.Code += ch;
								chMinusTwo.Rune = (Rune)code;
								bufferRow [buffer.X - 2] = chMinusTwo; // must be set explicitly now
							}
						} else {
							chMinusOne.Code += ch;
							chMinusOne.Rune = (Rune)code;
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

						if (buffer.Y >= buffer.ScrollBottom) {
							terminal.Scroll (isWrapped: true);
						} else {
							// The line already exists (eg. the initial viewport), mark it as a
							// wrapped line
							buffer.Lines [++buffer.Y].IsWrapped = true;
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
						buffer.X = cols - 1;
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
					if (lastCell.Width == 2)
						bufferRow [cols - 1] = empty;

				}

				// write current char to buffer and advance cursor
				var charData = new CharData (curAttr, (Rune)code, chWidth, ch);
				bufferRow [buffer.X++] = charData;

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
