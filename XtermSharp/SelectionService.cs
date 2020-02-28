using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace XtermSharp {
	/// <summary>
	/// Provides a service for working with selections
	/// </summary>
	public class SelectionService {
		readonly PointComparer comparer;
		readonly Terminal terminal;
		readonly NStack.ustring nullString;
		readonly NStack.ustring spaceString;
		private bool active;

		public SelectionService (Terminal terminal)
		{
			this.terminal = terminal;
			comparer = new PointComparer ();
			nullString = NStack.ustring.Make (CharData.Null.Rune);
			spaceString = NStack.ustring.Make (" ");
		}

		/// <summary>
		/// Gets or sets a value indicating whether the selection is active or not
		/// </summary>
		public bool Active {
			get => active;
			set {
				var oldState = active;
				active = value;
				if (oldState != value) {
					SelectionChanged?.Invoke ();
				}
			}
		}

		/// <summary>
		/// Gets the selection start point in buffer coordinates
		/// </summary>
		public Point Start { get; private set; }

		/// <summary>
		/// Gets the selection end point in buffer coordinates
		/// </summary>
		public Point End { get; private set; }

		/// <summary>
		/// Raised when the selection range or active state has been changed
		/// </summary>
		public event Action SelectionChanged;

		/// <summary>
		/// Starts selection from the given point in the buffer
		/// </summary>
		public void StartSelection (int row, int col)
		{
			Start = End = new Point (col, row + terminal.Buffer.YDisp);
			// set the field to bypass sending this event twice
			active = true;
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Starts selection from the last start position
		/// </summary>
		public void StartSelection ()
		{
			End = Start;
			// set the field to bypass sending this event twice
			active = true;
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Sets the start and end positions but does not start selection
		/// this lets us record the last position of mouse clicks so that
		/// drag and shift+click operations know from where to start selection
		/// from
		/// </summary>
		public void SetSoftStart(int row, int col)
		{
			Start = End = new Point (col, row + terminal.Buffer.YDisp);
		}

		/// <summary>
		/// Extends the selection based on the user "shift" clicking. This has
		/// slightly different semantics than a "drag" extension because we can
		/// shift the start to be the last prior end point if the new extension
		/// is before the current start point. 
		/// </summary>
		public void ShiftExtend (int row, int col)
		{
			active = true;
			var newEnd = new Point (col, row + terminal.Buffer.YDisp);

			var shouldSwapStart = false;
			if (comparer.Compare (Start, End) < 0) {
				// start is before end, is the new end before Start
				if (comparer.Compare (newEnd, Start) < 0) {
					// yes, swap Start and End
					shouldSwapStart = true;
				}
			} else if (comparer.Compare (Start, End) > 0) {
				if (comparer.Compare (newEnd, Start) > 0) {
					// yes, swap Start and End
					shouldSwapStart = true;
				}
			}

			if (shouldSwapStart) {
				Start = End;
			}

			End = newEnd;
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Extends the selection by moving the end point to the new point.
		/// </summary>
		public void DragExtend (int row, int col)
		{
			End = new Point (col, row + terminal.Buffer.YDisp);
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Selects the entire buffer
		/// </summary>
		public void SelectAll()
		{
			Start = new Point (0, 0);
			End = new Point (terminal.Cols - 1, terminal.Buffer.Lines.MaxLength - 1);

			// set the field to bypass sending this event twice
			active = true;
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Clears the selection
		/// </summary>
		public void SelectNone ()
		{
			active = false;
			SelectionChanged?.Invoke ();
		}

		/// <summary>
		/// Gets the selected range as text
		/// </summary>
		public string GetSelectedText ()
		{
			var lines = GetSelectedLines ();
			if (lines.Length == 0)
				return string.Empty;

			var builder = new StringBuilder ();
			foreach (var line in lines) {
				line.GetFragmentStrings (builder);
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Gets the selected range as an array of Line
		/// </summary>
		public Line [] GetSelectedLines()
		{
			var start = Start;
			var end = End;

			switch (comparer.Compare (start, End)) {
			case 0:
				return Array.Empty<Line>();
			case 1:
				start = End;
				end = Start;
				break;
			}

			if (start.Y < 0 || start.Y > terminal.Buffer.Lines.Length) {
				return Array.Empty<Line> ();
			}

			if (end.Y >= terminal.Buffer.Lines.Length) {
				end.Y = terminal.Buffer.Lines.Length - 1;
			}

			return GetSelectedLines (start, end);
		}

		Line [] GetSelectedLines (Point start, Point end)
		{
			var lines = new List<Line> ();
			var buffer = terminal.Buffer;
			string str;
			Line currentLine = new Line ();
			lines.Add (currentLine);

			// keep a list of blank lines that we see. if we see content after a group
			// of blanks, add those blanks but skip all remaining / trailing blanks
			// these will be blank lines in the selected text output
			var blanks = new List<LineFragment> ();

			Action addBlanks = () => {
				int lastLine = -1;
				foreach (var b in blanks) {
					if (lastLine != -1 && b.Line != lastLine) {
						currentLine = new Line ();
						lines.Add (currentLine);
					}

					lastLine = b.Line;
					currentLine.Add (b);
				}
				blanks.Clear ();
			};

			// get the first line
			BufferLine bufferLine = buffer.Lines [start.Y];
			if (bufferLine.HasAnyContent ()) {
				str = TranslateBufferLineToString (buffer, start.Y, start.X, start.Y < end.Y ? -1 : end.X);

				var fragment = new LineFragment (str, start.Y, start.X);
				currentLine.Add (fragment);
			}

			// get the middle rows
			var line = start.Y + 1;
			var isWrapped = false;
			while (line < end.Y) {
				bufferLine = buffer.Lines [line];
				isWrapped = bufferLine?.IsWrapped ?? false;

				str = TranslateBufferLineToString (buffer, line, 0, -1);

				if (bufferLine.HasAnyContent ()) {
					// add previously gathered blank fragments
					addBlanks ();

					if (!isWrapped) {
						// this line is not a wrapped line, so the
						// prior line has a hard linefeed
						// add a fragment to that line
						currentLine.Add (LineFragment.NewLine (line - 1));

						// start a new line
						currentLine = new Line ();
						lines.Add (currentLine);
					}

					// add the text we found to the current line
					currentLine.Add (new LineFragment (str, line, 0));
				} else {
					// this line has no content, which means that it's a blank line inserted
					// somehow, or one of the trailing blank lines after the last actual content
					// make a note of the line
					// check that this line is a wrapped line, if so, add a line feed fragment
					if (!isWrapped) {
						blanks.Add (LineFragment.NewLine (line - 1));
					}

					blanks.Add (new LineFragment (str, line, 0));
				}

				line++;
			}

			// get the last row
			if (end.Y != start.Y) {
				bufferLine = buffer.Lines [end.Y];
				if (bufferLine.HasAnyContent ()) {
					addBlanks ();

					isWrapped = bufferLine?.IsWrapped ?? false;
					str = TranslateBufferLineToString (buffer, end.Y, 0, end.X);
					if (!isWrapped) {
						currentLine.Add (LineFragment.NewLine (line - 1));
						currentLine = new Line ();
						lines.Add (currentLine);
					}

					currentLine.Add (new LineFragment (str, line, 0));
				}
			}

			return lines.ToArray ();
		}

		string TranslateBufferLineToString(Buffer buffer, int line, int start, int end)
		{
			return buffer.TranslateBufferLineToString (line, true, start, end).Replace (nullString, spaceString).ToString();
		}

		class PointComparer : IComparer<Point> {
			public int Compare (Point x, Point y)
			{
				if (x.Y < y.Y)
					return -1;
				if (x.Y > y.Y)
					return 1;
				// x and y are on the same row, compare columns
				if (x.X < y.X)
					return -1;
				if (x.X > y.X)
					return 1;
				// they are the same
				return 0;
			}
		}
	}
}
