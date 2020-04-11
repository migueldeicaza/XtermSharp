using XtermSharp.CommandExtensions;

namespace XtermSharp.Tests.EscTests {
	public class DECFRA_Tests : FillRectangleTest {

		public DECFRA_Tests ()
		{
			TestCharacter = '%';
		}

		protected override void Fill ((int top, int left, int bottom, int right) rect)
		{
			Terminal.csiDECFRA ((int)TestCharacter, rect.top, rect.left, rect.bottom, rect.right);
		}
	}
}
