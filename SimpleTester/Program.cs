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
						break;
					lineText += (char) cd.Rune;
				}
				// rtrim empty cells as xterm does
				lineText = lineText.TrimEnd ();
				result += lineText;
				result += '\n';
			}
			return result;
		}

		public static void Main (string [] args)
		{

			foreach (var f in Directory.GetFiles ("/cvs/xterm.js/fixtures/escape_sequence_files", "*.in")) {
				Console.WriteLine ("Parsing " + Path.GetFileName (f));
				var x = new Terminal (new TerminalOptions ());
				var bytes = File.ReadAllBytes (f);
				x.Feed (bytes);

				var expected = File.ReadAllText (Path.ChangeExtension (f, ".text"));
				var result = TerminalToString (x);
				if (result != expected) {
					Console.WriteLine ("FAILED");
					for (int i = 0; i < Math.Min (result.Length, expected.Length); i++){
						if (result [i] != expected [i]) {
							Console.WriteLine ("Offset: {0} expected 0x{1:x} got 0x{2:x}", i, (int) result [i], (int) expected [i]);
							break;
						}
					}
				}
			}
			Console.WriteLine ("All tests ran");	
		}
	}
}
