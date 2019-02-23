using System;
namespace XtermSharp {
	public enum CursorStyle {
		BlinkBlock, SteadyBlock, BlinkUnderline, SteadyUnderline, BlinkingBar, SteadyBar
	}

	public class TerminalOptions {
		public int Cols, Rows;
		public bool ConvertEol, CursorBlink;
		public string TermName;
		public CursorStyle CursorStyle;
		public bool ScreenReaderMode;
	}
}
