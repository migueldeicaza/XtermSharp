
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;

namespace XtermSharp.Tests.EscTests {
	/// <summary>
	/// CR (CarriageReturn) tests
	/// </summary>
	public class CR_Tests : BaseTerminalTest {
		[Fact]
		public void CR_Basic ()
		{
			//esccmd.CUP(Point(3, 3))
			//escio.Write(esc.CR)
			//AssertEQ(GetCursorPosition(), Point(1, 3))
			Terminal.csiCUP ((3, 3));
			Terminal.CarriageReturn ();
			Terminal.AssertCursorPosition (1, 3);
		}

		[Fact]
		public void CR_MovesToLeftMarginWhenRightOfLeftMargin ()
		{
			//"""Move the cursor to the left margin if it starts right of it."""
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(6, 1))
			//escio.Write(esc.CR)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//AssertEQ(GetCursorPosition(), Point(5, 1))
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((6, 1));
			Terminal.CarriageReturn ();
			Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
			Terminal.AssertCursorPosition (5, 1);
		}

		[Fact]
		public void CR_MovesToLeftOfScreenWhenLeftOfLeftMargin ()
		{
			//"""Move the cursor to the left edge of the screen when it starts of left the margin."""
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(4, 1))
			//escio.Write(esc.CR)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//AssertEQ(GetCursorPosition(), Point(1, 1))
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((4, 1));
			Terminal.CarriageReturn ();
			Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
			Terminal.AssertCursorPosition (1, 1);
		}

		[Fact]
		public void CR_StaysPutWhenAtLeftMargin ()
		{
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.CUP(Point(5, 1))
			//escio.Write(esc.CR)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//AssertEQ(GetCursorPosition(), Point(5, 1))
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiCUP ((5, 1));
			Terminal.CarriageReturn ();
			Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
			Terminal.AssertCursorPosition (5, 1);
		}

		[Fact]
		public void CR_MovesToLeftMarginWhenLeftOfLeftMarginInOriginMode ()
		{
			//"""In origin mode, always go to the left margin, even if the cursor starts left of it."""
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)
			//esccmd.DECSET(esccmd.DECOM)
			//esccmd.CUP(Point(4, 1))
			//escio.Write(esc.CR)
			//esccmd.DECRESET(esccmd.DECLRMM)
			//escio.Write("x")
			//esccmd.DECRESET(esccmd.DECOM)
			//AssertScreenCharsInRectEqual(Rect(5, 1, 5, 1), ["x"])
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);
			Terminal.csiDECSET (CsiCommandCodes.DECOM);
			Terminal.csiCUP ((4, 1));
			Terminal.CarriageReturn ();
			Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
			Terminal.Feed ("x");
			Terminal.csiDECRESET (CsiCommandCodes.DECOM);
			AssertScreenCharsInRectEqual ((5, 1, 5, 1), "x");
		}
	}
}
