using System;
using XtermSharp;
using System.IO;

namespace SimpleTester {
	class MainClass {
		static string TerminalToString (Terminal term)
		{
			var result = "";
			var lineText = "";
			for (var line = term.Buffer.YBase; line < term.Buffer.YBase + term.Rows; line++) {
				lineText = "";
				for (var cell = 0; cell < term.Cols; ++cell) {
					var cd = term.Buffer.Lines [line] [cell];
					// (line).get (cell) [CHAR_DATA_CHAR_INDEX] || WHITESPACE_CELL_CHAR;
					if (cd.Code == 0)
						lineText += " ";
		    			else
						lineText += (char) cd.Rune;
				}
				// rtrim empty cells as xterm does
				lineText = lineText.TrimEnd ();
				result += lineText;
				result += '\n';
			}
			return result;
		}

		static void Skip (string msg) => Console.WriteLine ("   => Skipping due to " + msg);
		public static void Main (string [] args)
		{

			foreach (var f in Directory.GetFiles ("/cvs/xterm.js/fixtures/escape_sequence_files", "*.in")) {
				switch (Path.GetFileName (f)) {
				case "t0008-BS.in":
					Skip ("known issue - backspace on the boundary of the border, and incompatible with Mac");
					continue;
				}
				Console.WriteLine ("Parsing " + Path.GetFileName (f));
				var x = new Terminal (new TerminalOptions ());
				var bytes = File.ReadAllBytes (f);

				x.Feed (bytes);

				var expected = File.ReadAllText (Path.ChangeExtension (f, ".text"));
				var result = TerminalToString (x);

				var expected2 = "";
				// Cope with some blank space on the results
				foreach (var l in expected.Split ('\n')) {
					expected2 += l.TrimEnd ();
					expected2 += "\n";
				}
				expected2 = expected2.Substring (0, expected2.Length-1);

				if (result != expected2) {
					Console.WriteLine ("FAILED");
					for (int i = 0; i < Math.Min (result.Length, expected2.Length); i++){
						if (result [i] != expected2 [i]) {
							File.WriteAllText (Path.ChangeExtension (f, ".xts"), result); 
							Console.WriteLine ("Offset: {0} expected 0x{1:x} got 0x{2:x}", i, (int) result [i], (int) expected2 [i]);
							break;
						}
					}
				}
			}
			Console.WriteLine ("All tests ran");	
		}
	}
}
