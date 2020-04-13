using Xunit;
using XtermSharp.CommandExtensions;
using System;

namespace XtermSharp.Tests {
	static class CommandExtensions {
		public static void AssertCursorPosition(this Terminal terminal, int col, int row)
		{
			var buffer = terminal.Buffer;
			var y = buffer.Y + 1 - (terminal.OriginMode ? buffer.ScrollTop : 0);
			// Need the max, because the cursor could be before the leftMargin
			var x = Math.Max (1, buffer.X + 1 - (terminal.IsUsingMargins () ? buffer.MarginLeft : 0));

			Assert.True (x == col && y == row, $"Expected ({col}, {row}) but found ({x}, {y})");
		}

		public static (int cols, int rows) GetScreenSize(this Terminal terminal)
		{
			return (terminal.Buffer.Cols, terminal.Buffer.Rows);
		}
	}
}
