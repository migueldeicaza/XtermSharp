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

		public void Put (int [] data, int start, int end)
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
			parser.SetExecuteHandler (7, Bell);
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
			parser.SetExecuteHandler (0x84 /* Index */, Index);
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
			parser.SetEscHandler ("D", Index);
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
			throw new NotImplementedException ();
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

		void Index (byte code)
		{
			throw new NotImplementedException ();
		}

		void ShiftIn (byte code)
		{
			throw new NotImplementedException ();
		}

		void ShiftOut (byte code)
		{
			throw new NotImplementedException ();
		}

		void Tab (byte code)
		{
			throw new NotImplementedException ();
		}

		void Backspace (byte code)
		{
			throw new NotImplementedException ();
		}

		void CarriageReturn (byte code)
		{
			throw new NotImplementedException ();
		}

		void LineFeed (byte code)
		{
			throw new NotImplementedException ();
		}

		void Bell (byte code)
		{
			throw new NotImplementedException ();
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

		bool EraseInLine (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool EraseInDisplay (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorForwardTab (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorPosition (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorCharAbsolute (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorPrecedingLine (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorNextLine (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorBackward (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorForward (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorDown (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool CursorUp (int [] pars)
		{
			throw new NotImplementedException ();
		}

		bool InsertChars (int [] pars)
		{
			throw new NotImplementedException ();
		}

		void Print (int [] data, int start, int end)
		{
			throw new NotImplementedException ();
		}
	}
}
