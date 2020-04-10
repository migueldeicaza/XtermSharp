using Xunit;
using XtermSharp.CommandExtensions;

namespace XtermSharp.Tests {
	static class CommandExtensions {
		public static void AssertCursorPosition(this Terminal terminal, int col, int row)
		{
			Assert.True (terminal.Buffer.X == col - 1 && terminal.Buffer.Y == row - 1, $"Expected ({col}, {row}) but found ({terminal.Buffer.X + 1}, {terminal.Buffer.Y + 1})");
		}

		public static (int cols, int rows) GetScreenSize(this Terminal terminal)
		{
			return (terminal.Buffer.Cols, terminal.Buffer.Rows);
		}
	}
}
