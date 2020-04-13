
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;

namespace XtermSharp.Tests.EscTests {
	/// <summary>
	/// BS (Backspace) tests
	/// </summary>
	public class BS_Tests : BaseTerminalTest {
		[Fact]
		public void BS_Basic ()
		{
			//esccmd.CUP(Point(3, 3))
			//escio.Write(esc.BS)
			//AssertEQ (GetCursorPosition(), Point(2, 3))
			Terminal.csiCUP ((3, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (2, 3);
		}

		[Fact]
		public void BS_NoWrapByDefault ()
		{
			//esccmd.CUP(Point(1, 3))
			//escio.Write(esc.BS)
			//AssertEQ(GetCursorPosition(), Point(1, 3))
			Terminal.csiCUP ((1, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (1, 3);
		}

		[Fact]
		public void BS_WrapsInWraparoundMode ()
		{
			//esccmd.DECSET (esccmd.DECAWM)
			//esccmd.DECSET (esccmd.ReverseWraparound)
			//esccmd.CUP (Point (1, 3))
			//escio.Write (esc.BS)
			//size = GetScreenSize ()
			//AssertEQ (GetCursorPosition (), Point (size.width (), 2))
			Terminal.csiDECSET(CsiCommandCodes.DECAWM);
			Terminal.csiDECSET (CsiCommandCodes.ReverseWraparound);
			Terminal.csiCUP ((1, 3));
			Terminal.Backspace ();
			var sz = Terminal.GetScreenSize ();
			Terminal.AssertCursorPosition (sz.cols, 2);
		}

		[Fact]
		public void BS_ReverseWrapRequiresDECAWM ()
		{
			//esccmd.DECRESET (esccmd.DECAWM)
			//esccmd.DECSET (esccmd.ReverseWraparound)
			//esccmd.CUP (Point (1, 3))
			//escio.Write (esc.BS)
			//AssertEQ (GetCursorPosition (), Point (1, 3))

			//esccmd.DECSET (esccmd.DECAWM)
			//esccmd.DECRESET (esccmd.ReverseWraparound)
			//esccmd.CUP (Point (1, 3))
			//escio.Write (esc.BS)
			//AssertEQ (GetCursorPosition (), Point (1, 3))

			// TODO: DECSET wrappers
			Terminal.Wraparound = false;
			Terminal.csiDECSET (CsiCommandCodes.ReverseWraparound);

			Terminal.csiCUP ((1, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (1, 3);

			Terminal.Wraparound = true;
			Terminal.ReverseWraparound = false;
			Terminal.csiCUP ((1, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (1, 3);
		}

		[Fact]
		public void BS_ReverseWrapWithLeftRight ()
		{
			//esccmd.DECSET(esccmd.DECAWM)
			//esccmd.DECSET(esccmd.ReverseWraparound)
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(5, 3))
			//escio.Write(esc.BS)
			//AssertEQ(GetCursorPosition(), Point(10, 2))
			Terminal.csiDECSET (CsiCommandCodes.DECAWM);
			Terminal.csiDECSET (CsiCommandCodes.ReverseWraparound);
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((5, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (10, 2);
		}

		[Fact]
		public void BS_ReversewrapFromLeftEdgeToRightMargin ()
		{
			//"""If cursor starts at left edge of screen, left of left margin, backspace
			//takes it to the right margin."""
			//esccmd.DECSET(esccmd.DECAWM)
			//esccmd.DECSET(esccmd.ReverseWraparound)
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(1, 3))
			//escio.Write(esc.BS)
			//AssertEQ(GetCursorPosition(), Point(10, 2))

			Terminal.csiDECSET (CsiCommandCodes.DECAWM);
			Terminal.csiDECSET (CsiCommandCodes.ReverseWraparound);
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((1, 3));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (10, 2);
		}

		// Not implemented
		//[Fact]
		public void BS_ReverseWrapGoesToBottom ()
		{
			//"""If the cursor starts within the top/bottom margins, after doing a
			//reverse wrap, the cursor remains within those margins.

			//Reverse-wrap is a feature of xterm since its first release in 1986.
			//The X10.4 version would reverse-wrap (as some hardware terminals did)
			//from the upper-left corner of the screen to the lower-right.
			//Left/right margin support, which was added to xterm in 2012,
			//modified the reverse-wrap feature to limit the cursor to those margins.
			//Because top/bottom margins should be treated consistently,
			//xterm was modified in 2018 to further amend the handling of
			//reverse-wrap."""
			//esccmd.DECSET(esccmd.DECAWM)
			//esccmd.DECSET(esccmd.ReverseWraparound)
			//esccmd.DECSTBM(2, 5)
			//esccmd.CUP(Point(1, 2))
			//escio.Write(esc.BS)
			//AssertEQ(GetCursorPosition(), Point(80, 5))

			//Terminal.Wraparound = true;
			//Terminal.ReverseWraparound = true;
			//Terminal.MarginMode = true;
			//Terminal.Buffer.MarginLeft = 5 - 1;
			//Terminal.Buffer.MarginRight = 10 - 1;
			//Terminal.csiCUP (1, 2);
			//Commander.Backspace ();
			//Terminal.AssertCursorPosition (80, 5);
		}

		[Fact]
		public void BS_StopsAtLeftMargin ()
		{
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(5, 1))
			//escio.Write(esc.BS)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//AssertEQ(GetCursorPosition(), Point(5, 1))
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((5, 1));
			Terminal.Backspace ();
			Terminal.MarginMode = false;
			Terminal.AssertCursorPosition (5, 1);
		}

		[Fact]
		public void BS_MovesLeftWhenLeftOfLeftMargin ()
		{
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(4, 1))
			//escio.Write(esc.BS)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//AssertEQ(GetCursorPosition(), Point(3, 1))
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((4, 1));
			Terminal.Backspace ();
			Terminal.MarginMode = false;
			Terminal.AssertCursorPosition (3, 1);
		}

		[Fact]
		public void BS_StopsAtOrigin ()
		{
			//esccmd.CUP(Point(1, 1))
			//escio.Write(esc.BS)
			//AssertEQ(GetCursorPosition(), Point(1, 1))
			Terminal.csiCUP ((1, 1));
			Terminal.Backspace ();
			Terminal.AssertCursorPosition (1, 1);
		}

		[Fact]
		public void BS_CursorStartsInDoWrapPosition ()
		{
			//"""Cursor is right of right edge of screen."""
			//size = GetScreenSize()
			//esccmd.CUP(Point(size.width() - 1, 1))
			//escio.Write("ab")
			//escio.Write(esc.BS)
			//escio.Write("X")
			//AssertScreenCharsInRectEqual(Rect(size.width() - 1, 1, size.width(), 1), ["Xb"])
			var size = Terminal.GetScreenSize ();
			Terminal.csiCUP ((size.cols - 1, 1));
			Terminal.Feed ("ab");
			Terminal.Backspace ();
			Terminal.Feed ("X");

			AssertScreenCharsInRectEqual ((size.cols - 1, 1, size.cols, 1), "Xb");
		}
	}
}
