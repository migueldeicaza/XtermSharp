using System;
using XtermSharp;
using Xunit;

namespace Tests {

	public class UnitTest1 {
		public string SetScrollRange (int start, int end) => $"\x1b[{start};{end}";
		public string CursorDown (int n) => $"\x1b[{n}B";
		public string Clear () => "\x1b[2J";

		//
		// This tests that scrolling that happens outside the defined scroll region
		// is ignored.  For example if the scroll region is 12,13, but the cursor
		// is at 24, and it writes, new lines are added, and the region 12,13 is not
		// affected
		[Fact]
		public void ScrollingOutsideScrollRegionHappens ()
		{
			var x = new Terminal (null, new TerminalOptions () { Cols = 80, Rows = 40 });
			x.Feed (Clear () + SetScrollRange (12, 13) + CursorDown (24) + "1\n2\n3\n4\n");
			Assert.Equal (49, x.Buffer.Lines [24] [0].Code);
			Assert.Equal (50, x.Buffer.Lines [25] [0].Code);
			Assert.Equal (51, x.Buffer.Lines [26] [0].Code);
			Assert.Equal (52, x.Buffer.Lines [27] [0].Code);
			Assert.Equal (0, x.Buffer.Lines [28] [0].Code);
		}
	}
}
