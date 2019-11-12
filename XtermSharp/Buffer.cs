using System;
using System.Collections;
using System.Diagnostics;
using NStack;

namespace XtermSharp
{
	/// <summary>
	/// This class represents a terminal buffer (an internal state of the terminal), where text contents, cursor and scroll position are stored.
	/// </summary>
    	[DebuggerDisplay ("({X},{Y} YD{YDisp}:YB{YBase} Scroll={ScrollBottom,ScrollTop}")]
	public class Buffer {
		CircularList<BufferLine> lines;
		public int YDisp, YBase;
		public int X;
		int y;
		public int Y {
			get => y;
			set {
				if (value < 0 || value > Terminal.Rows-1)
					throw new Exception ();
				else
					y = value;
			}
		}
		public int ScrollBottom, ScrollTop;
		BitArray tabStops;
		public int SavedX, SavedY, SavedAttr = CharData.DefaultAttr;
		public Terminal Terminal { get; private set; }
		bool hasScrollback;

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:XtermSharp.Buffer"/> has scrollback.
		/// </summary>
		/// <value><c>true</c> if has scrollback; otherwise, <c>false</c>.</value>
		public bool HasScrollback => hasScrollback && lines.MaxLength > Terminal.Rows;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:XtermSharp.Buffer"/> class.
		/// </summary>
		/// <param name="terminal">The terminal the Buffer will belong to.</param>
		/// <param name="hasScrollback">Whether the buffer should respect the scrollback of the terminal.</param>
		public Buffer (Terminal terminal, bool hasScrollback = true)
		{
			Terminal = terminal;
			this.hasScrollback = hasScrollback;
			Clear ();
		}

		public CircularList<BufferLine> Lines => lines;

		// Gets the correct buffer length based on the rows provided, the terminal's
   		// scrollback and whether this buffer is flagged to have scrollback or not.
		int getCorrectBufferLength (int rows)
		{
			if (!hasScrollback)
				return rows;
			var correct = rows + Terminal.Options.Scrollback ?? 0;
			return correct > Int32.MaxValue ? Int32.MaxValue : correct;
		}

		public BufferLine GetBlankLine (int attribute, bool isWrapped = false)
		{
			var cd = new CharData (attribute); 

			return new BufferLine (Terminal.Cols, cd, isWrapped);
		}

		/// <summary>
		/// Clears the buffer to it's initial state, discarding all previous data.
		/// </summary>
		public void Clear ()
		{
			YDisp = 0;
			YBase = 0;
			X = 0;
			Y = 0;
			lines = new CircularList<BufferLine> (getCorrectBufferLength (Terminal.Rows));
			ScrollTop = 0;
			ScrollBottom = Terminal.Rows - 1;
			SetupTabStops ();
		}

		public bool IsCursorInViewport {
			get {
				var absoluteY = YBase + Y;
				var relativeY = absoluteY - YDisp;
				return (relativeY >= 0 && relativeY < Terminal.Rows);
			}
		}

		/// <summary>
		/// Fills the buffer's viewport with blank lines.
		/// </summary>
		public void FillViewportRows (int? attribute = null)
		{
			// TODO: limitation in original, this does not cope with partial fills, it is either zero or nothing
			if (lines.Length != 0)
				return;
			var attr = attribute.HasValue ? attribute.Value : CharData.DefaultAttr;
			for (int i = Terminal.Rows; i > 0; i--)
				lines.Push (GetBlankLine (attr));
		}

		/// <summary>
		/// Resize the buffer, adjusting its data accordingly
		/// </summary>
		/// <returns>The resize.</returns>
		/// <param name="newCols">New columns.</param>
		/// <param name="newRows">New rows.</param>
		public void Resize (int newCols, int newRows)
		{
			Clear ();
			FillViewportRows ();

			// FIXME: should this not fill int he additional tab stops?   Bug in original
			tabStops.Length = newCols;
		}

		/// <summary>
		/// Translates a buffer line to a string, with optional start and end columns.   Wide characters will count as two columns in the resulting string. This 
		/// function is useful for getting the actual text underneath the raw selection position.
		/// </summary>
		/// <returns>The buffer line to string.</returns>
		/// <param name="lineIndex">The line being translated.</param>
		/// <param name="trimRight">If set to <c>true</c> trim whitespace to the right.</param>
		/// <param name="startCol">The column to start at.</param>
		/// <param name="endCol">The column to end at.</param>
		public ustring TranslateBufferLineToString (int lineIndex, bool trimRight, int startCol = 0, int endCol = -1)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Setups the tab stops.
		/// </summary>
		/// <param name="index">Index to start setting tabs stops from.</param>
		public void SetupTabStops (int index = -1)
		{
			int cols = Terminal.Cols;

			if (index != -1 && tabStops != null) {
				var from = Math.Min (index, cols - 1);
				if (!tabStops [from])
					index = PreviousTabStop (from);
			} else 
				tabStops = new BitArray (cols);

			int tabStopWidth = Terminal.Options.TabStopWidth ?? 8;
			for (int i = 0; i < cols; i += tabStopWidth)
				tabStops [i] = true;
		}

		public void TabSet (int pos)
		{
			if (pos < tabStops.Length)
				tabStops [pos] = true;
		}

		public void ClearStop (int pos)
		{
			if (pos < tabStops.Length)
				tabStops [pos] = false;
		}

		public void ClearTabStops ()
		{
			tabStops = new BitArray (tabStops.Count);
		}

		/// <summary>
		/// Move the cursor to the previous tab stop from the given position (default is current).
		/// </summary>
		/// <returns>The tab stop.</returns>
		/// <param name="index">The position to move the cursor to the previous tab stop.</param>
		public int PreviousTabStop (int index = -1)
		{
			if (index == -1)
				index = X;
			while (index > 0 && tabStops [--index])
				;
			return index >= Terminal.Cols ? Terminal.Cols - 1 : index;
		}

		/// <summary>
		/// Move the cursor one tab stop forward from the given position (default is current).
		/// </summary>
		/// <returns>The tab stop.</returns>
		/// <param name="index">The position to move the cursor one tab stop forward.</param>
		public int NextTabStop (int index = -1)
		{
			if (index == -1) 
				index = X;
			do {
				index++;
				if (index == Terminal.Cols)
					break;
				if (tabStops [index])
					break;
			} while (index < Terminal.Cols);
			return index >= Terminal.Cols ? Terminal.Cols - 1 : index;
		}
	}

}
