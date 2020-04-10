
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;

namespace XtermSharp.Tests.EscTests {
	/// <summary>
	/// CUP (CursorPosition) tests
	/// </summary>
	public class CUP_Tests : BaseTerminalTest {
		[Fact]
		public void CUP_DefaultParams ()
		{
			//"""With no params, CUP moves to 1,1."""
			//esccmd.CUP(Point(6, 3))

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 6)
			//AssertEQ(position.y(), 3)

			//esccmd.CUP()

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 1)
			//AssertEQ(position.y(), 1)

			Terminal.csiCUP ((6, 3));
			Terminal.AssertCursorPosition (6, 3);
			Terminal.csiCUP ();
			Terminal.AssertCursorPosition (1, 1);
		}

		[Fact]
		public void CUP_RowOnly ()
		{
			//"""Default column is 1."""
			//esccmd.CUP(Point(6, 3))

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 6)
			//AssertEQ(position.y(), 3)

			//esccmd.CUP(row=2)

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 1)
			//AssertEQ(position.y(), 2)
			Terminal.csiCUP ((6, 3));
			Terminal.AssertCursorPosition (6, 3);

			Terminal.csiCUP (2);
			Terminal.AssertCursorPosition (1, 2);
		}

		[Fact]
		public void CUP_ColumnOnly ()
		{
			//"""Default row is 1."""
			//esccmd.CUP(Point(6, 3))

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 6)
			//AssertEQ(position.y(), 3)

			//esccmd.CUP(col=2)

			//position = GetCursorPosition()
			//AssertEQ(position.x(), 2)
			//AssertEQ(position.y(), 1)

			Terminal.csiCUP ((6, 3));
			Terminal.AssertCursorPosition (6, 3);

			Terminal.csiCUP (0, 2);
			Terminal.AssertCursorPosition (2, 1);
		}

		[Fact]
		public void CUP_ZeroIsTreatedAsOne ()
		{
			//"""Zero args are treated as 1."""
			//esccmd.CUP(Point(6, 3))
			//esccmd.CUP(col=0, row=0)
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 1)
			//AssertEQ(position.y(), 1)
			Terminal.csiCUP ((6, 3));
			Terminal.csiCUP (0, 0);
			Terminal.AssertCursorPosition (1, 1);
		}

		[Fact]
		public void CUP_OutOfBoundsParams ()
		{
			//"""With overly large parameters, CUP moves as far as possible down and right."""
			//size = GetScreenSize()
			//esccmd.CUP(Point(size.width() + 10, size.height() + 10))

			//position = GetCursorPosition()
			//AssertEQ(position.x(), size.width())
			//AssertEQ(position.y(), size.height())

			var sz = Terminal.GetScreenSize ();
			Terminal.csiCUP ((sz.cols + 10, sz.rows + 10));
			Terminal.AssertCursorPosition (sz.cols, sz.rows);
		}

		[Fact]
		public void CUP_RespectsOriginMode ()
		{
			//"""CUP is relative to margins in origin mode."""
			//# Set a scroll region.
			//esccmd.DECSTBM(6, 11)
			//esccmd.DECSET(esccmd.DECLRMM)
			//esccmd.DECSLRM(5, 10)

			//# Move to center of region
			//esccmd.CUP(Point(7, 9))
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 7)
			//AssertEQ(position.y(), 9)

			//# Turn on origin mode.
			//esccmd.DECSET(esccmd.DECOM)

			//# Move to top-left
			//esccmd.CUP(Point(1, 1))

			//# Check relative position while still in origin mode.
			//position = GetCursorPosition()
			//AssertEQ(position.x(), 1)
			//AssertEQ(position.y(), 1)

			//escio.Write("X")

			//# Turn off origin mode. This moves the cursor.
			//esccmd.DECRESET(esccmd.DECOM)

			//# Turn off scroll regions so checksum can work.
			//esccmd.DECSTBM()
			//esccmd.DECRESET(esccmd.DECLRMM)

			//# Make sure there's an X at 5,6
			//AssertScreenCharsInRectEqual(Rect(5, 6, 5, 6),
			//                                ["X"])


			Terminal.csiDECSTBM (6, 11);
			Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
			Terminal.csiDECSLRM (5, 10);

			Terminal.csiCUP ((7, 9));
			Terminal.AssertCursorPosition (7, 9);

			Terminal.csiDECSET (CsiCommandCodes.DECOM);

			Terminal.csiCUP ((1, 1));
			Terminal.AssertCursorPosition (1, 1);

			Terminal.Feed ("X");

			Terminal.csiDECRESET (CsiCommandCodes.DECOM);

			Terminal.csiDECSTBM ();
			Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);

			AssertScreenCharsInRectEqual ((5, 6, 5, 6), "X");
		}
	}
}
