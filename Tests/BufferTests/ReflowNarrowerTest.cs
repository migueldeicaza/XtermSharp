using System;
using XtermSharp;
using Xunit;

namespace XtermSharp.Tests.BufferTests {

	public class ReflowNarrowerTests {
		[Fact]
		public void DoesNotCrashWhenReflowingToTinyWidth ()
		{
			var options = new TerminalOptions () { Cols = 10, Rows = 10 };
			options.Scrollback = 1;
			var terminal = new Terminal (null, options);

			terminal.Feed ("1234567890\r\n");
			terminal.Feed ("ABCDEFGH\r\n");
			terminal.Feed ("abcdefghijklmnopqrstxxx\r\n");
			terminal.Feed ("\r\n");

			// if we resize to a small column width, content is pushed back up and out the top
			// of the buffer. Ensure that this does not crash
			terminal.Resize (3, 10);
		}
	}
}
