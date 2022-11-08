using Xunit;

namespace XtermSharp.Tests.BufferTests {
	public class SelectionTests {
		[Fact]
		public void DoesNotCrashWhenSelectingWordOrExpressionOutsideColumnRange ()
		{
			var terminal = new Terminal (null, new TerminalOptions { Rows = 10, Cols = 10 });
			var selection = new SelectionService (terminal);

			terminal.Feed ("1234567890");

			// depending on the size of terminal view, there might be a space near the margin where the user
			// clicks which might result in a col or row outside the bounds of terminal,
			selection.SelectWordOrExpression (-1, 0);
			selection.SelectWordOrExpression (11, 0);
		}

		[Fact]
		public void DoesNotCrashWhenSelectingWordOrExpressionOutsideRowRange ()
		{
			var terminal = new Terminal (null, new TerminalOptions { Rows = 10, Cols = 10, Scrollback = 0 });
			var selection = new SelectionService (terminal);

			terminal.Feed ("1234567890");

			// depending on the size of terminal view, there might be a space near the margin where the user
			// clicks which might result in a col or row outside the bounds of terminal,
			selection.SelectWordOrExpression (0, -1);
		}
	}
}
