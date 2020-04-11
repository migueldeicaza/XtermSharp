
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;

namespace XtermSharp.Tests.EscTests {
	public abstract class FillRectangleTest : BaseTerminalTest {
		string [] fillData = new string [] {
			"abcdefgh",
			"ijklmnop",
			"qrstuvwx",
			"yz012345",
			"ABCDEFGH",
			"IJKLMNOP",
			"QRSTUVWX",
			"YZ6789!@"
		};

		protected char TestCharacter = '_';

		void Prepare ()
		{
			Terminal.csiCUP ((1, 1));
			foreach (var line in fillData) {
				Terminal.Feed (line + "\r\n");
			}
		}

		protected abstract void Fill ((int top, int left, int bottom, int right) rect);

		[Fact]
		public void Basic ()
		{
			Prepare ();
			Fill ((5, 5, 7, 7));

			AssertScreenCharsInRectEqual ((1, 1, 8, 8), GetTestString (
				"abcdefgh" +
				"ijklmnop" +
				"qrstuvwx" +
				"yz012345" +
				"ABCD***H" +
				"IJKL***P" +
				"QRST***X" +
				"YZ6789!@"
				));
		}

		[Fact]
		public void InvalidRectDoesNothing ()
		{
			Prepare ();
			Fill ((5, 5, 4, 4));

			AssertScreenCharsInRectEqual ((1, 1, 8, 8), GetTestString (
				"abcdefgh" +
				"ijklmnop" +
				"qrstuvwx" +
				"yz012345" +
				"ABCDEFGH" +
				"IJKLMNOP" +
				"QRSTUVWX" +
				"YZ6789!@"
				));
		}

		//[Fact]
		// TODO: DefaultArgs
		public void DefaultArgs ()
		{
			//"""Write a value at each corner, run fill with no args, and verify the
			//corners have all been replaced with self.character."""
			//size = GetScreenSize()
			//points = [Point(1, 1),
			//Point(size.width(), 1),
			//Point(size.width(), size.height()),
			//Point(1, size.height())]
			//n = 1
			//for point in points:
			//esccmd.CUP(point)
			//escio.Write(str(n))
			//n += 1

			//self.fill()

			//for point in points:
			//AssertScreenCharsInRectEqual(
			//Rect(point.x(), point.y(), point.x(), point.y()),
			//[self.characters(point, 1)])
		}

		string GetTestString (string template)
		{
			return template.Replace ('*', TestCharacter);
		}
	}
}
