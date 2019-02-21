using System;
namespace XtermSharp {

	public interface ITerminalOptions
	{
		bool Rows { get; }
		int? Scrollback { get; }
		int? TabStopWidth { get; }
	}

	public interface ITerminal {
		int Rows { get; }
		int Cols { get; }
		ITerminalOptions Options { get; }
		BufferLine BlankLine (bool erase, bool isWrapped, int cols);
	}
}
