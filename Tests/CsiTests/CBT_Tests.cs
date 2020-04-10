
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;

namespace XtermSharp.Tests.CsiTests {
	/// <summary>
	/// CBT (Backward Tab) tests
	/// </summary>
	public class CBT_Tests : BaseTerminalTest {
		[Fact]
		public void CBT_OneTabStopByDefault ()
		{
			//esccmd.CUP(Point(17, 1))
			//esccmd.CBT()
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 9)
			Terminal.csiCUP ((17, 1));
			Terminal.csiCBT ();
			Terminal.AssertCursorPosition (9, 1);
		}

		[Fact]
		public void CBT_ExplicitParameter ()
		{
			//esccmd.CUP(Point(25, 1))
			//esccmd.CBT(2)
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 9)
			Terminal.csiCUP ((25, 1));
			Terminal.csiCBT (2);
			Terminal.AssertCursorPosition (9, 1);
		}

		[Fact]
		public void CBT_StopsAtLeftEdge ()
		{
			//esccmd.CUP (Point (25, 2))
			//esccmd.CBT (5)
			//position = GetCursorPosition ()
			//AssertEQ (position.x (), 1)
			//AssertEQ (position.y (), 2)
			Terminal.csiCUP ((25, 2));
			Terminal.csiCBT (5);
			Terminal.AssertCursorPosition (1, 2);
		}

		[Fact]
		public void CBT_IgnoresRegion ()
		{
			//# Set a scroll region.
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 30)
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 30);

			//# Move to center of region
			//esccmd.CUP(Point(7, 9))
			Terminal.csiCUP ((7, 2));

			//# Tab backwards out of the region.
			//esccmd.CBT(2)
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 1)
			Terminal.csiCBT (2);
			Terminal.AssertCursorPosition (1, 2);
		}
	}
}
