using System;
using System.Text;

namespace XtermSharp {
	/// <summary>
	/// CSI input handler
	/// </summary>
	internal class CsiInputHandler {
		readonly Terminal terminal;

		public CsiInputHandler (Terminal terminal)
		{
			this.terminal = terminal;
		}

		/// <summary>
		/// CSI Ps ; Ps ; Ps t - Various window manipulations and reports (xterm)
		/// See https://invisible-island.net/xterm/ctlseqs/ctlseqs.html for a full
		/// list of commans for this escape sequence
		/// </summary>
		internal void SetWindowOptions (int [] pars)
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

					terminal.Delegate.Send (Encoding.UTF8.GetBytes (response));
					return;
				case 16:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportCellSizeInPixels);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}6;16;10t";
					}

					terminal.Delegate.Send (Encoding.UTF8.GetBytes (response));
					return;
				case 17:
					return;
				case 18:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportScreenSizeCharacters);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}8;{terminal.Rows};{terminal.Cols}t";
					}

					terminal.Delegate.Send (Encoding.UTF8.GetBytes (response));
					return;
				case 19:
					response = terminal.Delegate.WindowCommand (terminal, WindowManipulationCommand.ReportScreenSizeCharacters);
					if (response == null) {
						response = $"{terminal.ControlCodes.CSI}9;{terminal.Rows};{terminal.Cols}t";
					}

					terminal.Delegate.Send (Encoding.UTF8.GetBytes (response));
					return;
				case 20:
					response = terminal.IconTitle.Replace ("\\", "");
					terminal.Delegate.Send (Encoding.UTF8.GetBytes ($"\u001b]L{response}\\"));
					return;
				case 21:
					response = terminal.Title.Replace ("\\", "");
					terminal.Delegate.Send (Encoding.UTF8.GetBytes ($"\u001b]l{response}\\"));
					return;
				default:
					return;
				}
			}
		}

	}
}
