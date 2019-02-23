using System;
using XtermSharp;
using System.IO;

namespace SimpleTester {
	class MainClass {
		public static void Main (string [] args)
		{

			foreach (var f in Directory.GetFiles ("/cvs/xterm.js/fixtures/escape_sequence_files", "*.in")) {
				Console.WriteLine ("Parsing " + Path.GetFileName (f));
				var x = new Terminal (new TerminalOptions ());
				var bytes = File.ReadAllBytes (f);
				x.Feed (bytes);
			}
			Console.WriteLine ("All tests ran");	
		}
	}
}
