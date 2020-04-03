using System;
using System.Collections.Generic;

// 
// Not implemented (either in xterm.js):
// DECUDK (https://vt100.net/docs/vt510-rm/DECUDK.html)
// DCS + q Pt ST (xterm) * Request Terminfo String
//  DCS + p Pt ST (xterm) * set terminfo data

namespace XtermSharp {
	/// <summary>
	/// DCS Subparser implementations
	/// 
	/// DCS $ q Pt ST
	/// DECRQSS (https://vt100.net/docs/vt510-rm/DECRQSS.html)
	/// Request Status String (DECRQSS), VT420 and up.
	/// Response: DECRPSS (https://vt100.net/docs/vt510-rm/DECRPSS.html)
	/// </summary>
	class DECRQSS : IDcsHandler {
		readonly Terminal terminal;
		List<byte> data;

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
}
