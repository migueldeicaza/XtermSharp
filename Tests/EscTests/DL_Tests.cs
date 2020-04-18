
using Xunit;
using XtermSharp.CommandExtensions;
using XtermSharp.CsiCommandExtensions;
using System.Text;

namespace XtermSharp.Tests.EscTests {
	/// <summary>
	/// DL (Delete Line) tests
	/// </summary>
	public class DL_Tests : BaseTerminalTest {
                [Fact]
                public void DL_DefaultParam() {
                        //"""DL with no parameter should delete a single line."""
                        //# Set up the screen with 0001, 0002, ..., height
                        //Prepare();

                        //# Delete the second line, moving subsequent lines up.
                        //esccmd.DL()

                        //# Build an array of 0001, 0003, 0004, ..., height
                        //height = GetScreenSize().height()
                        //y = 1
                        //expected_lines = []
                        //for i in xrange(height):
                        //if y != 2:
                        //expected_lines.append("%04d" % y)
                        //y += 1

                        //# The last line should be blank
                        //expected_lines.append(empty() * 4)
                        //AssertScreenCharsInRectEqual(Rect(1, 1, 4, height), expected_lines)
                        var height = Prepare ();
                        Terminal.csiDL ();

                        var sb = new StringBuilder ();
                        for (int y = 1; y <= height; y++) {
                                if (y != 2) {
                                        sb.Append ($"{y:d4}");
                                }
                        }
                        sb.Append ("    ");
                        AssertScreenCharsInRectEqual ((1, 1, 4, height), sb.ToString());
                }

                [Fact]
                public void DL_ExplicitParam() {
                        //"""DL should delete the given number of lines."""
                        //# Set up the screen with 0001, 0002, ..., height
                        //Prepare();

                        //# Delete two lines starting at the second line, moving subsequent lines up.
                        //esccmd.DL(2)

                        //# Build an array of 0001, 0004, ..., height
                        //height = GetScreenSize().height()
                        //y = 1
                        //expected_lines = []
                        //for i in xrange(height):
                        //if y < 2 or y > 3:
                        //expected_lines.append("%04d" % y)
                        //y += 1

                        //# The last two lines should be blank
                        //expected_lines.append(empty() * 4)
                        //expected_lines.append(empty() * 4)

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 4, height), expected_lines)
                        var height = Prepare ();
                        Terminal.csiDL (2);

                        var sb = new StringBuilder ();
                        for (int y = 1; y <= height; y++) {
                                if (y != 2 && y != 3) {
                                        sb.Append ($"{y:d4}");
                                }
                        }
                        sb.Append ("    ");
                        sb.Append ("    ");
                        AssertScreenCharsInRectEqual ((1, 1, 4, height), sb.ToString ());
                }

                [Fact]
                public void DL_DeleteMoreThanVisible() {
                        //"""Test passing a too-big parameter to DL."""
                        //# Set up the screen with 0001, 0002, ..., height
                        //Prepare();

                        //# Delete more than the height of the screen.
                        //height = GetScreenSize().height()
                        //esccmd.DL(height * 2)

                        //# Build an array of 0001 followed by height-1 empty lines.
                        //y = 1
                        //expected_lines = ["0001"]
                        //for i in xrange(height - 1):
                        //expected_lines.append(empty() * 4)

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 4, height), expected_lines)
                        var height = Prepare ();
                        Terminal.csiDL (height * 2);

                        var sb = new StringBuilder ();
                        sb.Append ("0001");
                        for (int y = 1; y <= height - 1; y++) {
                                sb.Append ("    ");
                        }
                        AssertScreenCharsInRectEqual ((1, 1, 4, height), sb.ToString ());
                }

                [Fact]
                public void DL_InScrollRegion() {
                        //"""Test that DL does the right thing when the cursor is inside the scroll
                        //region."""
                        //PrepareForRegion();
                        //esccmd.DECSTBM(2, 4)
                        //esccmd.CUP(Point(3, 2))
                        //esccmd.DL()
                        //esccmd.DECSTBM()

                        //expected_lines = ["abcde",
                        //                "klmno",
                        //                "pqrst",
                        //                empty() * 5,
                        //                "uvwxy"]
                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSTBM (2, 4);
                        Terminal.csiCUP ((3, 2));
                        Terminal.csiDL ();
                        Terminal.csiDECSTBM ();
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "klmno" +
                                "pqrst" +
				"     " +
                                "uvwxy");
                }

                [Fact]
                public void DL_OutsideScrollRegion() {
                        //"""Test that DL does nothing when the cursor is outside the scroll
                        //region."""
                        //PrepareForRegion();
                        //esccmd.DECSTBM(2, 4)
                        //esccmd.CUP(Point(3, 1))
                        //esccmd.DL()
                        //esccmd.DECSTBM()

                        //expected_lines = ["abcde",
                        //                "fghij",
                        //                "klmno",
                        //                "pqrst",
                        //                "uvwxy"]

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSTBM (2, 4);
                        Terminal.csiCUP ((3, 1));
                        Terminal.csiDL ();
                        Terminal.csiDECSTBM ();
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "fghij" +
                                "klmno" +
                                "pqrst" +
                                "uvwxy");
                }

                [Fact]
                public void DL_InLeftRightScrollRegion() {
                        //"""Test that DL respects left-right margins."""
                        //PrepareForRegion();
                        //esccmd.DECSET(esccmd.DECLRMM)
                        //esccmd.DECSLRM(2, 4)
                        //esccmd.CUP(Point(3, 2))
                        //esccmd.DL()
                        //esccmd.DECRESET(esccmd.DECLRMM)

                        //expected_lines = ["abcde",
                        //                "flmnj",
                        //                "kqrso",
                        //                "pvwxt",
                        //                "u" + empty() * 3 + "y"]

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSLRM (2, 4);
                        Terminal.csiCUP ((3, 2));
                        Terminal.csiDL ();
                        Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "flmnj" +
                                "kqrso" +
                                "pvwxt" +
                                "u   y");
                }

                [Fact]
                public void DL_OutsideLeftRightScrollRegion() {
                        //"""Test that DL does nothing outside a left-right margin."""
                        //PrepareForRegion();
                        //esccmd.DECSET(esccmd.DECLRMM)
                        //esccmd.DECSLRM(2, 4)
                        //esccmd.CUP(Point(1, 2))
                        //esccmd.DL()
                        //esccmd.DECRESET(esccmd.DECLRMM)

                        //expected_lines = ["abcde",
                        //                "fghij",
                        //                "klmno",
                        //                "pqrst",
                        //                "uvwxy"]

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSLRM (2, 4);
                        Terminal.csiCUP ((1, 2));
                        Terminal.csiDL ();
                        Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "fghij" +
                                "klmno" +
                                "pqrst" +
                                "uvwxy");
                }

                [Fact]
                public void DL_InLeftRightAndTopBottomScrollRegion() {
                        //"""Test that DL respects left-right margins together with top-bottom."""
                        //PrepareForRegion();
                        //esccmd.DECSET(esccmd.DECLRMM)
                        //esccmd.DECSLRM(2, 4)
                        //esccmd.DECSTBM(2, 4)
                        //esccmd.CUP(Point(3, 2))
                        //esccmd.DL()
                        //esccmd.DECRESET(esccmd.DECLRMM)
                        //esccmd.DECSTBM()

                        //expected_lines = ["abcde",
                        //                "flmnj",
                        //                "kqrso",
                        //                "p" + empty() * 3 + "t",
                        //                "uvwxy"]

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSLRM (2, 4);
                        Terminal.csiDECSTBM (2, 4);
                        Terminal.csiCUP ((3, 2));
                        Terminal.csiDL ();
                        Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSTBM ();
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "flmnj" +
                                "kqrso" +
                                "p   t" +
                                "uvwxy");
                }

                [Fact]
                public void DL_ClearOutLeftRightAndTopBottomScrollRegion() {
                        //"""Erase the whole scroll region with both kinds of margins."""
                        //PrepareForRegion();
                        //esccmd.DECSET(esccmd.DECLRMM)
                        //esccmd.DECSLRM(2, 4)
                        //esccmd.DECSTBM(2, 4)
                        //esccmd.CUP(Point(3, 2))
                        //esccmd.DL(99)
                        //esccmd.DECRESET(esccmd.DECLRMM)
                        //esccmd.DECSTBM()

                        //expected_lines = ["abcde",
                        //                "f" + empty() * 3 + "j",
                        //                "k" + empty() * 3 + "o",
                        //                "p" + empty() * 3 + "t",
                        //                "uvwxy"]

                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSLRM (2, 4);
                        Terminal.csiDECSTBM (2, 4);
                        Terminal.csiCUP ((3, 2));
                        Terminal.csiDL (99);
                        Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSTBM ();
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "f   j" +
                                "k   o" +
                                "p   t" +
                                "uvwxy");
                }

                [Fact]
                public void DL_OutsideLeftRightAndTopBottomScrollRegion() {
                        //"""Test that DL does nothing outside left-right margins together with top-bottom."""
                        //PrepareForRegion();
                        //esccmd.DECSET(esccmd.DECLRMM)
                        //esccmd.DECSLRM(2, 4)
                        //esccmd.DECSTBM(2, 4)
                        //esccmd.CUP(Point(1, 1))
                        //esccmd.DL()
                        //esccmd.DECRESET(esccmd.DECLRMM)
                        //esccmd.DECSTBM()
                        //expected_lines = ["abcde",
                        //                "fghij",
                        //                "klmno",
                        //                "pqrst",
                        //                "uvwxy"]
                        //AssertScreenCharsInRectEqual(Rect(1, 1, 5, 5), expected_lines)
                        PrepareForRegion ();
                        Terminal.csiDECSET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSLRM (2, 4);
                        Terminal.csiDECSTBM (2, 4);
                        Terminal.csiCUP ((1, 1));
                        Terminal.csiDL ();
                        Terminal.csiDECRESET (CsiCommandCodes.DECLRMM);
                        Terminal.csiDECSTBM ();
                        AssertScreenCharsInRectEqual ((1, 1, 5, 5),
                                "abcde" +
                                "fghij" +
                                "klmno" +
                                "pqrst" +
                                "uvwxy");
                }

                int Prepare() {
                        //"""Fills the screen with 4-char line numbers (0001, 0002, ...) down to the
                        //last line and puts the cursor on the start of the second line."""
                        var height = Terminal.GetScreenSize ().rows;
                        for (int i = 0; i < height; i++) {
                                Terminal.csiCUP ((1, i + 1));
                                Terminal.Feed ($"{i + 1:d4}");
                        }

                        Terminal.csiCUP ((1, 2));
                        return height;
                }

                void PrepareForRegion ()
                {
                        //"""Sets the screen up as
                        //abcde
                        //fghij
                        //klmno
                        //pqrst
                        //uvwxy

                        //With the cursor on the 'h'."""

                        string [] lines = {"abcde",
                                "fghij",
                                "klmno",
                                "pqrst",
                                "uvwxy" };

                        for (int i = 0; i < lines.Length; i++) {
                                Terminal.csiCUP ((1, i + 1));
                                Terminal.Feed (lines[i]);
                        }

                        Terminal.csiCUP ((3, 2));
                }
        }
}
