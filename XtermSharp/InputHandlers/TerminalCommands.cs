using System;
using System.Text;

namespace XtermSharp {
	/// <summary>
	/// Used by input handlers to perform commands on the terminal and the active buffer
	/// </summary>
	// TODO: unit tests, mock Terminal
	internal class TerminalCommands {
		readonly Terminal terminal;

		public TerminalCommands (Terminal terminal)
		{
			this.terminal = terminal;
		}

		/// <summary>
		// CSI Ps B
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
					var y = buffer.Y + 1 - (terminal.OriginMode ? buffer.ScrollTop : 0);
					// Need the max, because the cursor could be before the leftMargin
					var x = Math.Max (1, buffer.X + 1 - (IsUsingMargins () ? buffer.MarginLeft : 0));
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
					terminal.EmitData ($"\x1b[?{y};{x}R");
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

				// TODO: backport device status
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
			terminal.Wraparound = true;  // defaults: xterm - true, vt100 - false
			terminal.ApplicationKeypad = false; // ?
			terminal.SyncScrollArea ();
			terminal.ApplicationCursor = false;
			terminal.CurAttr = CharData.DefaultAttr;

			terminal.Charset = null;
			terminal.SetgLevel (0);







			terminal.OriginMode = false;
			terminal.SavedOriginMode = false;
			terminal.InsertMode = false;
			terminal.MarginMode = false;
			terminal.SavedMarginMode = false;




			//savedWraparound = false
			//savedOriginMode = false
			//savedMarginMode = false
			//reverseWraparound = false
			//savedReverseWraparound = false
			//wraparound = true  // defaults: xterm - true, vt100 - false
			//applicationKeypad = false
			//syncScrollArea ()
			//applicationCursor = false
			//buffer.scrollTop = 0
			//buffer.scrollBottom = rows - 1
			//curAttr = CharData.defaultAttr
			buffer.SavedAttr = CharData.DefaultAttr;
			buffer.SavedY = 0;
			buffer.SavedX = 0;
			buffer.MarginRight = buffer.Cols;
			buffer.MarginLeft = 0;
			terminal.Charset = null;
			//setgLevel (0)
			//conformance = .vt500
			// MIGUEL TODO:
			// TODO: audit any new variables, those in setup might be useful
			//terminal.Buffer.ScrollTop = 0;
			//terminal.Buffer.ScrollBottom = terminal.Rows - 1;
			//terminal.Buffer.X = 0;
			//terminal.Buffer.Y = 0;
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
			//savedWraparound = wraparound
			terminal.SavedMarginMode = terminal.MarginMode;
			terminal.SavedOriginMode = terminal.OriginMode;
			//savedReverseWraparound = reverseWraparound
		}

		public void RestoreCursor ()
		{
			var buffer = terminal.Buffer;
			buffer.X = buffer.SavedX;
			buffer.Y = buffer.SavedY;
			terminal.CurAttr = buffer.SavedAttr;
			terminal.MarginMode = terminal.SavedMarginMode;
			terminal.OriginMode = terminal.SavedOriginMode;
			//wraparound = savedWraparound
			//reverseWraparound = savedReverseWraparound
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
					terminal.SendResponse ($"\u001b]L{response}\\");
					return;
				case 21:
					response = terminal.Title.Replace ("\\", "");
					terminal.SendResponse ($"\u001b]l{response}\\");
					return;
				default:
					return;
				}
			}
		}

		/// <summary>
		/// Restrict cursor to viewport size / scroll margin (origin mode)
		/// - Parameter limitCols: by default it is true, but the reverseWraparound mechanism in Backspace needs `x` to go beyond.
		/// </summary>
		void RestrictCursor (bool limitCols = true)
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

			var reverseWraparound = false;

			//RestrictCursor (!terminal.ReverseWraparound);
			RestrictCursor ();

			int left = terminal.MarginMode ? buffer.MarginLeft : 0;
			int right = terminal.MarginMode ? buffer.MarginRight : buffer.Cols - 1;

			if (buffer.X > left) {
				buffer.X--;
			} else if (reverseWraparound) {
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
				if (buffer.Y < left) {
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

		bool IsUsingMargins ()
		{
			return terminal.OriginMode && terminal.MarginMode;
		}
	}
}
