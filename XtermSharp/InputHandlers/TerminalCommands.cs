using System;
using System.Collections.Generic;
using System.Text;

namespace XtermSharp {
	/// <summary>
	/// Used by input handlers to perform commands on the terminal and the active buffer
	/// </summary>
	// TODO: unit tests, mock Terminal
	internal class TerminalCommands {
		readonly Terminal terminal;
		bool savedMarginMode;
		bool savedOriginMode;
		bool savedWraparound;
		bool savedReverseWraparound;

		public TerminalCommands (Terminal terminal)
		{
			this.terminal = terminal;
		}

		/// <summary>
		// CSI Ps A
		// Cursor Up Ps Times (default = 1) (CUU).
		/// </summary>
		public void CursorUp (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			var top = buffer.ScrollTop;

			if (buffer.Y < top) {
				top = 0;
			}

			if (buffer.Y - param < top)
				buffer.Y = top;
			else
				buffer.Y -= param;
		}

		/// <summary>
		// CSI Ps B
		// Cursor Down Ps Times (default = 1) (CUD).
		/// </summary>
		public void CursorDown (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			var bottom = buffer.ScrollBottom;

			// When the cursor starts below the scroll region, CUD moves it down to the
			// bottom of the screen.
			if (buffer.Y > bottom) {
				bottom = buffer.Rows - 1;
			}

			var newY = buffer.Y + param;

			// review
			//if (buffer.Y > buffer.ScrollBottom)
			//	buffer.Y = buffer.ScrollBottom - 1;
			if (newY >= bottom)
				buffer.Y = bottom;
			else
				buffer.Y = newY;

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= terminal.Cols)
				buffer.X--;
		}


		/// <summary>
		// CSI Ps C
		// Cursor Forward Ps Times (default = 1) (CUF).
		/// </summary>
		public void CursorForward (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;
			var right = terminal.MarginMode ? buffer.MarginRight : buffer.Cols - 1;

			if (buffer.X > right) {
				right = buffer.Cols - 1;
			}

			buffer.X += param;
			if (buffer.X > right) {
				buffer.X = right;
			}
		}

		/// <summary>
		/// CSI Ps D
		/// Cursor Backward Ps Times (default = 1) (CUB).
		/// </summary>
		public void CursorBackward (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			// What is our left margin - depending on the settings.
			var left = terminal.MarginMode ? buffer.MarginLeft : 0;

			// If the cursor is positioned before the margin, we can go backwards to the first column
			if (buffer.X < left) {
				left = 0;
			}
			buffer.X -= param;

			if (buffer.X < left) {
				buffer.X = left;
			}
		}

		/// <summary>
		/// ESC E
		/// C1.NEL
		///   DEC mnemonic: NEL (https://vt100.net/docs/vt510-rm/NEL)
		///   Moves cursor to first position on next line.
		/// </summary>
		public void NextLine ()
		{
			terminal.Buffer.X = IsUsingMargins () ? terminal.Buffer.MarginLeft : 0;
			terminal.Index ();
		}

		/// <summary>
		/// CSI Ps G
		/// Cursor Character Absolute  [column] (default = [row,1]) (CHA).
		/// </summary>
		public void CursorCharAbsolute (int [] pars)
		{
			int param = Math.Max (pars.Length > 0 ? pars [0] : 1, 1);
			var buffer = terminal.Buffer;

			buffer.X = (IsUsingMargins () ? buffer.MarginLeft : 0) + Math.Min (param - 1, buffer.Cols - 1);
		}

		/// <summary>
		/// CSI Ps P
		/// Delete Ps Character(s) (default = 1) (DCH).
		/// </summary>
		public void DeleteChars (int [] pars)
		{
			var buffer = terminal.Buffer;
			var p = Math.Max (pars.Length == 0 ? 1 : pars [0], 1);

			if (terminal.MarginMode) {
				if (buffer.X + p > buffer.MarginRight) {
					p = buffer.MarginRight - buffer.X;
				}
			}

			buffer.Lines [buffer.Y + buffer.YBase].DeleteCells (buffer.X, p, new CharData (terminal.EraseAttr ()));

			terminal.UpdateRange (buffer.Y);
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
		public void SendDeviceAttributes (int [] pars, string collect)
		{
			if (pars.Length > 0 && pars [0] > 0)
				return;


			if (collect == ">" || collect == ">0") {
				// DA2 Secondary Device Attributes
				if (pars.Length == 0 || pars [0] == 0) {
					var vt510 = 61; // we identified as a vt510
					var kbd = 1; // PC-style keyboard
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}>{vt510};20;{kbd}c");
					return;
				}

				return;
			}

			var name = terminal.Options.TermName;
			if (collect == "") {
				if (name.StartsWith ("xterm", StringComparison.Ordinal) || name.StartsWith ("rxvt-unicode", StringComparison.Ordinal) || name.StartsWith ("screen", StringComparison.Ordinal)) {
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?1;2c");
				} else if (name.StartsWith ("linux", StringComparison.Ordinal)) {
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?6c");
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

		/// <summary>
		/// CSI Ps n  Device Status Report (DSR).
		///     Ps = 5  -> Status Report.  Result (``OK'') is
		///   CSI 0 n
		///     Ps = 6  -> Report Cursor Position (CPR) [row;column].
		///   Result is
		///   CSI r ; c R
		/// CSI ? Ps n
		///   Device Status Report (DSR, DEC-specific).
		///     Ps = 6  -> Report Cursor Position (CPR) [row;column] as CSI
		///     ? r ; c R (assumes page is zero).
		///     Ps = 1 5  -> Report Printer status as CSI ? 1 0  n  (ready).
		///     or CSI ? 1 1  n  (not ready).
		///     Ps = 2 5  -> Report UDK status as CSI ? 2 0  n  (unlocked)
		///     or CSI ? 2 1  n  (locked).
		///     Ps = 2 6  -> Report Keyboard status as
		///   CSI ? 2 7  ;  1  ;  0  ;  0  n  (North American).
		///   The last two parameters apply to VT400 & up, and denote key-
		///   board ready and LK01 respectively.
		///     Ps = 5 3  -> Report Locator status as
		///   CSI ? 5 3  n  Locator available, if compiled-in, or
		///   CSI ? 5 0  n  No Locator, if not.
		/// </summary>
		public void DeviceStatus (int [] pars, string collect)
		{
			var buffer = terminal.Buffer;

			if (collect == "") {
				switch (pars [0]) {
				case 5:
					// status report
					terminal.EmitData ("\x1b[0n");
					break;
				case 6:
					// cursor position
					var y = Math.Max (1, buffer.Y + 1 - (terminal.OriginMode ? buffer.ScrollTop : 0));
					// Need the max, because the cursor could be before the leftMargin
					var x = Math.Max (1, buffer.X + 1 - (terminal.OriginMode ? buffer.MarginLeft : 0));
					terminal.EmitData ($"\x1b[{y};{x}R");
					break;
				}
			} else if (collect == "?") {
				// modern xterm doesnt seem to
				// respond to any of these except ?6, 6, and 5
				switch (pars [0]) {
				case 6:
					// cursor position
					var y = buffer.Y + 1 - (terminal.OriginMode ? buffer.ScrollTop : 0);
					// Need the max, because the cursor could be before the leftMargin
					var x = Math.Max (1, buffer.X + 1 - (IsUsingMargins () ? buffer.MarginLeft : 0));
					terminal.EmitData ($"\x1b[?{y};{x}1R");
					break;
				case 15:
					// Request printer status report, we respond "We are ready"
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?10n");
					break;
				case 25:
					// We respond "User defined keys are locked"
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?21n");
					break;
				case 26:
					// Requests keyboard type
					// We respond "American keyboard", TODO: worth plugging something else?  Mac perhaps?
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?27;1;0;0n");
					break;
				case 53:
					// no dec locator/mouse
					// this.handler(C0.ESC + '[?50n');
					break;
				case 55:
					// Request locator status
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?53n");
					break;
				case 56:
					// What kind of locator we have, we reply mouse, but perhaps on iOS we should respond something else
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?57;1n");
					break;
				case 62:
					// Macro space report
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}0*{'{'}");
					break;
				case 63:
					// Requests checksum of macros, we return 0
					var id = pars.Length > 1 ? pars [1] : 0;
					terminal.SendResponse ($"{terminal.ControlCodes.DCS}{id}!~0000{terminal.ControlCodes.ST}");
					break;
				case 75:
					// Data integrity report, no issues:
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?70n");
					break;
				case 85:
					// Multiple session status, we reply single session
					terminal.SendResponse ($"{terminal.ControlCodes.CSI}?83n");
					break;
				}
			}
		}

		/// <summary>
		/// http://vt100.net/docs/vt220-rm/table4-10.html
		///
		/// ! - CSI ! p   Soft terminal reset (DECSTR). */
		/// </summary>
		public void SoftReset ()
		{
			var buffer = terminal.Buffer;

			terminal.CursorHidden = false;
			terminal.InsertMode = false;
			terminal.OriginMode = false;

			terminal.Wraparound = true;  // defaults: xterm - true, vt100 - false
			terminal.ReverseWraparound = false;
			terminal.ApplicationKeypad = false;
			terminal.SyncScrollArea ();
			terminal.ApplicationCursor = false;
			terminal.CurAttr = CharData.DefaultAttr;

			terminal.Charset = null;
			terminal.SetgLevel (0);

			savedOriginMode = false;
			savedMarginMode = false;
			savedWraparound = false;
			savedReverseWraparound = false;

			//syncScrollArea ()
			//applicationCursor = false
			buffer.ScrollTop = 0;
			buffer.ScrollBottom = buffer.Rows - 1;
			buffer.SavedAttr = CharData.DefaultAttr;
			buffer.SavedY = 0;
			buffer.SavedX = 0;
			buffer.MarginRight = buffer.Cols;
			buffer.MarginLeft = 0;
			terminal.Charset = null;
			//conformance = .vt500
		}

		/// <summary>
		/// CSI Ps ; Ps r
		///   Set Scrolling Region [top;bottom] (default = full size of win-
		///   dow) (DECSTBM).
		// CSI ? Pm r
		/// </summary>
		public void SetScrollRegion (int [] pars, string collect)
		{
			if (collect != "")
				return;
			var buffer = terminal.Buffer;
			var top = pars.Length > 0 ? Math.Max (pars [0] - 1, 0) : 0;
			var bottom = buffer.Rows;
			if (pars.Length > 1) {
				// bottom = 0 means "bottom of the screen"
				var p = pars [1];
				if (p != 0) {
					bottom = Math.Min (pars [1], buffer.Rows);
				}
			}

			// normalize
			bottom--;
        
			// only set the scroll region if top < bottom
			if (top < bottom) {
				buffer.ScrollBottom = bottom;
				buffer.ScrollTop = top;
			}

			SetCursor (0, 0);
		}

		public void SetMargins (int [] pars)
		{
			var buffer = terminal.Buffer;
			var left = (pars.Length > 0 ? pars [0] : 1) - 1;
			var right = (pars.Length > 1 ? pars [1] : buffer.Cols) - 1;

			left = Math.Min (left, right);
			buffer.MarginLeft = left;
			buffer.MarginRight = right;
		}

		/// <summary>
		/// Sets the location of the cursor 
		/// </summary>
		public void SetCursor (int col, int row)
		{
			var buffer = terminal.Buffer;
			if (terminal.OriginMode) {
				buffer.X = col + (IsUsingMargins () ? buffer.MarginLeft : 0);
				buffer.Y = buffer.ScrollTop + row;
			} else {
				buffer.X = col;
				buffer.Y = row;
			}
		}

		/// <summary>
		/// CSI s
		/// ESC 7
		/// Save cursor (ANSI.SYS).
		/// </summary>
		public void SaveCursor ()
		{
			var buffer = terminal.Buffer;
			buffer.SavedX = buffer.X;
			buffer.SavedY = buffer.Y;
			buffer.SavedAttr = terminal.CurAttr;
			savedWraparound = terminal.Wraparound;
			savedReverseWraparound = terminal.ReverseWraparound;
			savedMarginMode = terminal.MarginMode;
			savedOriginMode = terminal.OriginMode;
		}

		public void RestoreCursor ()
		{
			var buffer = terminal.Buffer;
			buffer.X = buffer.SavedX;
			buffer.Y = buffer.SavedY;
			terminal.CurAttr = buffer.SavedAttr;
			terminal.MarginMode = savedMarginMode;
			terminal.OriginMode = savedOriginMode;
			terminal.Wraparound = savedWraparound;
			terminal.ReverseWraparound = savedReverseWraparound;
		}

		/// <summary>
		/// CSI Ps ; Ps ; Ps t - Various window manipulations and reports (xterm)
		/// See https://invisible-island.net/xterm/ctlseqs/ctlseqs.html for a full
		/// list of commans for this escape sequence
		/// </summary>
		public void SetWindowOptions (int [] pars)
		{
			if (pars == null || pars.Length == 0)
				return;

			if (pars.Length == 3 && pars [0] == 3) {
				terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.MoveWindowTo, pars [1], pars [2]);
				return;
			}
			if (pars.Length == 3 && pars [0] == 4) {
				terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.MoveWindowTo, pars [1], pars [2]);
				return;
			}

			if (pars.Length == 3 && pars [0] == 8) {
				terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ResizeTo, pars [1], pars [2]);
				return;
			}

			if (pars.Length == 2 && pars [0] == 9) {
				switch (pars [1]) {
				case 0:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.RestoreMaximizedWindow);
					return;
				case 1:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.MaximizeWindow);
					return;
				case 2:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.MaximizeWindowVertically);
					return;
				case 3:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.MaximizeWindowHorizontally);
					return;
				default:
					return;
				}
			}

			if (pars.Length == 2 && pars [0] == 10) {
				switch (pars [1]) {
				case 0:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.UndoFullScreen);
					return;
				case 1:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.SwitchToFullScreen);
					return;
				case 2:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ToggleFullScreen);
					return;
				default:
					return;
				}
			}

			if (pars.Length == 2 && pars [0] == 22) {
				switch (pars [1]) {
				case 0:
					terminal.PushTitle ();
					terminal.PushIconTitle ();
					return;
				case 1:
					terminal.PushIconTitle ();
					return;
				case 2:
					terminal.PushTitle ();
					return;
				default:
					return;
				}
			}

			if (pars.Length == 2 && pars [0] == 23) {
				switch (pars [1]) {
				case 0:
					terminal.PopTitle ();
					terminal.PopIconTitle ();
					return;
				case 1:
					terminal.PopTitle ();
					return;
				case 2:
					terminal.PopIconTitle ();
					return;
				default:
					return;
				}
			}

			if (pars.Length == 1) {
				string response = null;
				switch (pars [0]) {
				case 0:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.DeiconifyWindow);
					return;
				case 1:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.IconifyWindow);
					return;
				case 2:
					return;
				case 3:
					return;
				case 4:
					return;
				case 5:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.BringToFront);
					return;
				case 6:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.SendToBack);
					return;
				case 7:
					terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.RefreshWindow);
					return;
				case 15:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportSizeOfScreenInPixels);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}5;768;1024t";
					}

					terminal.SendResponse (response);
					return;
				case 16:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportCellSizeInPixels);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}6;16;10t";
					}

					terminal.SendResponse (response);
					return;
				case 17:
					return;
				case 18:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportScreenSizeCharacters);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}8;{terminal.Rows};{terminal.Cols}t";
					}

					terminal.SendResponse (response);
					return;
				case 19:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportScreenSizeCharacters);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}9;{terminal.Rows};{terminal.Cols}t";
					}

					terminal.SendResponse (response);
					return;
				case 20:
					response = terminal.IconTitle.Replace ("\\", "");
					terminal.SendResponse ($"{terminal.ControlCodes.OSC}l{response}{terminal.ControlCodes.ST}");
					return;
				case 21:
					response = terminal.Title.Replace ("\\", "");
					terminal.SendResponse ($"{terminal.ControlCodes.OSC}l{response}{terminal.ControlCodes.ST}");
					return;
				default:
					return;
				}
			}
		}

		/// <summary>
		/// Required by the test suite
		/// CSI Pi ; Pg ; Pt ; Pl ; Pb ; Pr * y
		/// Request Checksum of Rectangular Area (DECRQCRA), VT420 and up.
		/// Response is
		/// DCS Pi ! ~ x x x x ST
		///   Pi is the request id.
		///   Pg is the page number.
		///   Pt ; Pl ; Pb ; Pr denotes the rectangle.
		///   The x's are hexadecimal digits 0-9 and A-F.
		/// </summary>
		public void DECRQCRA (int[] pars)
		{
			var buffer = terminal.Buffer;

			int checksum = 0;
			var rid = pars.Length > 0 ? pars [0] : 1;
			var _ = pars.Length > 1 ? pars [1] : 0;
			var result = "0000";

			// Still need to imeplemnt the checksum here
			// Which is just the sum of the rune values
			if (terminal.Delegate.IsProcessTrusted()) {
				var rect = GetRectangleFromRequest (buffer, 2, pars);

				var top = rect.top;
				var left = rect.left;
				var bottom = rect.bottom;
				var right = rect.right;

				for (int row = top; row <= bottom; row++) {
					var line = buffer.Lines [row + buffer.YBase];
					for (int col = left; col <= right; col++) {
						var cd = line [col];

						//var ch = cd.getCharacter ();
						//for (scalar in ch.unicodeScalars) {
						//	checksum += scalar.value;
						//}
						checksum += cd.Code == 0 ? 32 : cd.Code;
					}
				}

				result = String.Format ("{0,4:X}", checksum);
			}

			terminal.SendResponse ($"{terminal.ControlCodes.DCS}{rid}!~{result}{terminal.ControlCodes.ST}");
		}

		/// <summary>
		/// DECERA - Erase Rectangular Area
		/// CSI Pt ; Pl ; Pb ; Pr ; $ z
		/// </summary>
		public void CommandDECERA (int [] pars)
		{
			var buffer = terminal.Buffer;
			var rect = GetRectangleFromRequest (buffer, 0, pars);

			for (int row = rect.top; row <= rect.bottom; row++) {
				var line = buffer.Lines [row + buffer.YBase];
				for (int col = rect.left; col <= rect.right; col++) {
					line [col] = new CharData(terminal.CurAttr, ' ', 1, 32);
				}
			}
		}

		/// <summary>
		/// Copy Rectangular Area (DECCRA), VT400 and up.
		/// CSI Pts ; Pls ; Pbs ; Prs ; Pps ; Ptd ; Pld ; Ppd $ v
		///  Pts ; Pls ; Pbs ; Prs denotes the source rectangle.
		///  Pps denotes the source page.
		///  Ptd ; Pld denotes the target location.
		///  Ppd denotes the target page.
		/// </summary>
		public void CsiCopyRectangularArea (int [] pars, string collect)
		{
			var buffer = terminal.Buffer;
			if (collect == "$") {
				var parArray = new int[8];
				parArray [0] = (pars.Length > 1 && pars [0] != 0 ? pars [0] : 1); // Pts default 1
				parArray [1] = (pars.Length > 2 && pars [1] != 0 ? pars [1] : 1); // Pls default 1
				parArray [2] = (pars.Length > 3 && pars [2] != 0 ? pars [2] : buffer.Rows - 1); // Pbs default to last line of page
				parArray [3] = (pars.Length > 4 && pars [3] != 0 ? pars [3] : buffer.Cols - 1); // Prs defaults to last column
				parArray [4] = (pars.Length > 5 && pars [4] != 0 ? pars [4] : 1); // Pps page source = 1
				parArray [5] = (pars.Length > 6 && pars [5] != 0 ? pars [5] : 1); // Ptd default is 1
				parArray [6] = (pars.Length > 7 && pars [6] != 0 ? pars [6] : 1); // Pld default is 1
				parArray [7] = (pars.Length > 8 && pars [7] != 0 ? pars [7] : 1); // Ppd default is 1

				// We only support copying on the same page, and the page being 1
				if (parArray [4] == parArray [7] && parArray [4] == 1) {
					var rect = GetRectangleFromRequest (buffer, 0, parArray);
					if (rect.top != 0 && rect.left != 0 && rect.bottom != 0 && rect.right != 0) {
						var rowTarget = parArray [5] - 1;
						var colTarget = parArray [6] - 1;

						// Block size
						var columns = rect.right - rect.left + 1;

						var cright = Math.Min (buffer.Cols - 1, rect.left + Math.Min (columns, buffer.Cols - colTarget));

						var lines = new List<List<CharData>> ();
						for (int row = rect.top; row <= rect.bottom; row++) {
							var line = buffer.Lines [row + buffer.YBase];
							var lineCopy = new List<CharData> ();
							for (int col = rect.left; col <= cright; col++) {
								lineCopy.Add (line [col]);
							}
							lines.Add (lineCopy);
						}

						for (int row = 0; row <= rect.bottom - rect.top; row++) {
							if (row + rowTarget >= buffer.Rows) {
								break;
							}

							var line = buffer.Lines [row + rowTarget + buffer.YBase];
							var lr = lines [row];
							for (int col = 0; col <= cright - rect.left; col++) {
								if (col >= buffer.Cols) {
									break;
								}

								line [colTarget + col] = lr [col];
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Restrict cursor to viewport size / scroll margin (origin mode)
		/// - Parameter limitCols: by default it is true, but the reverseWraparound mechanism in Backspace needs `x` to go beyond.
		/// </summary>
		public void RestrictCursor (bool limitCols = true)
		{
			var buffer = terminal.Buffer;
			buffer.X = Math.Min (buffer.Cols - (limitCols ? 1 : 0), Math.Max (0, buffer.X));
			buffer.Y = terminal.OriginMode
				? Math.Min (buffer.ScrollBottom, Math.Max (buffer.ScrollTop, buffer.Y))
				: Math.Min (buffer.Rows - 1, Math.Max (0, buffer.Y));

			terminal.UpdateRange (buffer.Y);
		}

		/// <summary>
		/// Backspace handler (Control-h)
		/// </summary>
		public void Backspace ()
		{
			var buffer = terminal.Buffer;

			RestrictCursor (!terminal.ReverseWraparound);

			int left = terminal.MarginMode ? buffer.MarginLeft : 0;
			int right = terminal.MarginMode ? buffer.MarginRight : buffer.Cols - 1;

			if (buffer.X > left) {
				buffer.X--;
			} else if (terminal.ReverseWraparound) {
				if (buffer.X <= left) {
					if (buffer.Y > buffer.ScrollTop && buffer.Y <= buffer.ScrollBottom && (buffer.Lines [buffer.Y + buffer.YBase].IsWrapped || terminal.MarginMode)) {
						if (!terminal.MarginMode) {
							buffer.Lines [buffer.Y + buffer.YBase].IsWrapped = false;
						}

						buffer.Y--;
						buffer.X = right;
						// TODO: find actual last cell based on width used
					} else if (buffer.Y == buffer.ScrollTop) {
						buffer.X = right;
						buffer.Y = buffer.ScrollBottom;
					} else if (buffer.Y > 0) {
						buffer.X = right;
						buffer.Y--;
					}
				}
			} else {
				if (buffer.X < left) {
					// This compensates for the scenario where backspace is supposed to move one step
					// backwards if the "x" position is behind the left margin.
					// Test BS_MovesLeftWhenLeftOfLeftMargin
					buffer.X--;
				} else if (buffer.X > left) {
					// If we have not reached the limit, we can go back, otherwise stop at the margin
					// Test BS_StopsAtLeftMargin
					buffer.X--;

				}
			}
		}

		/// <summary>
		/// Performs a linefeed
		/// </summary>
		public void LineFeed ()
		{
			var buffer = terminal.Buffer;
			if (terminal.Options.ConvertEol) {
				buffer.X = terminal.MarginMode ? buffer.MarginLeft : 0;
			}

			LineFeedBasic ();
		}

		/// <summary>
		/// Performs a basic linefeed
		/// </summary>
		public void LineFeedBasic ()
		{
			var buffer = terminal.Buffer;
			var by = buffer.Y;

			if (by == buffer.ScrollBottom) {
				terminal.Scroll (isWrapped: false);
			} else if (by == buffer.Rows - 1) {
			} else {
				buffer.Y = by + 1;
			}

			// If the end of the line is hit, prevent this action from wrapping around to the next line.
			if (buffer.X >= buffer.Cols) {
				buffer.X -= 1;
			}

			// This event is emitted whenever the terminal outputs a LF or NL.
			terminal.EmitLineFeed ();
		}


		bool IsUsingMargins ()
		{
			return terminal.OriginMode && terminal.MarginMode;
		}

		/// <summary>
		/// Validates optional arguments for top, left, bottom, right sent by various
		/// escape sequences and returns validated top, left, bottom, right in our 0-based
		/// internal coordinates
		/// </summary>
		(int top, int left, int bottom, int right) GetRectangleFromRequest (Buffer buffer, int start, int[] pars)
		{
			var top = Math.Max (1, pars.Length > start ? pars [start] : 1);
			var left = Math.Max (pars.Length > start + 1 ? pars [start + 1] : 1, 1);
			var bottom = pars.Length > start + 2 ? pars [start + 2] : -1;
			var right = pars.Length > start + 3 ? pars [start + 3] : -1;

			var rect = GetRectangleFromRequest (buffer, top, left, bottom, right);
			return rect;
		}

		/// <summary>
		/// Validates optional arguments for top, left, bottom, right sent by various
		/// escape sequences and returns validated top, left, bottom, right in our 0-based
		/// internal coordinates
		/// </summary>
		(int top, int left, int bottom, int right) GetRectangleFromRequest (Buffer buffer, int top, int left, int bottom, int right)
		{
			if (bottom < 0) {
				bottom = buffer.Rows;
			}
			if (right < 0) {
				right = buffer.Cols;
			}
			if (right > buffer.Cols) {
				right = buffer.Cols;
			}
			if (bottom > buffer.Rows) {
				bottom = buffer.Rows;
			}
			if (terminal.OriginMode) {
				top += buffer.ScrollTop;
				bottom += buffer.ScrollTop;
				left += buffer.MarginLeft;
				right += buffer.MarginLeft;
			}

			if (top > bottom || left > right) {
				return (0, 0, 0, 0);
			}

			return (top - 1, left - 1, bottom - 1, right - 1);
		}

	}
}
