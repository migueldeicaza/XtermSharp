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
		private bool active;

		public SelectionService (Terminal terminal)
		{
			this.terminal = terminal;
			comparer = new PointComparer ();
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

		public void SelectAll()
		{
			Start = new Point (0, 0);
			End = new Point (terminal.Cols - 1, terminal.Buffer.Lines.MaxLength - 1);

			// set the field to bypass sending this event twice
			active = true;
			SelectionChanged?.Invoke ();
		}

		public string GetSelectedText ()
		{
			var start = Start;
			var end = End;

			switch (comparer.Compare (start, End)) {
			case 0:
				return string.Empty;
			case 1:
				start = End;
				end = Start;
				break;
			}

			if (start.Y < 0 || start.Y > terminal.Buffer.Lines.Length) {
				return string.Empty;
			}

			if (end.Y >= terminal.Buffer.Lines.Length) {
				end.Y = terminal.Buffer.Lines.Length - 1;
			}

			var nullStr = NStack.ustring.Make (CharData.Null.Rune);
			var spaceStr = NStack.ustring.Make (" ");

			var builder = new StringBuilder ();

			// get the first line
			BufferLine bufferLine = terminal.Buffer.Lines [start.Y];
			NStack.ustring str;
			if (bufferLine.HasAnyContent ()) {
				str = terminal.Buffer.TranslateBufferLineToString (start.Y, true, start.X, start.Y < end.Y ? -1 : end.X).Replace (nullStr, spaceStr);
				builder.Append (str.ToString ());
			}

			// keep a list of blank lines that we see. if we see content after a group
			// of blanks, add those blanks but skip all remaining / trailing blanks
			List<string> blanks = new List<string> ();

			Action addBlanks = () => {
				foreach (var bl in blanks) {
					builder.Append (bl);
				}
				blanks.Clear ();
			};

			// get the middle rows
			var line = start.Y + 1;
			var isWrapped = false;
			while (line < end.Y) {
				bufferLine = terminal.Buffer.Lines [line];
				isWrapped = bufferLine?.IsWrapped ?? false;

				str = terminal.Buffer.TranslateBufferLineToString (line, true, 0, -1).Replace (nullStr, spaceStr);

				if (bufferLine.HasAnyContent()) {
					addBlanks ();

					if (!isWrapped)
						builder.AppendLine ();

					builder.Append (str.ToString ());

				} else {
					if (!isWrapped)
						blanks.Add ("\n");

					blanks.Add (str.ToString ());
				}

				line++;
			}

			if (end.Y != start.Y) {
				// get the last row
				bufferLine = terminal.Buffer.Lines [end.Y];
				if (bufferLine.HasAnyContent ()) {
					addBlanks ();

					isWrapped = bufferLine?.IsWrapped ?? false;
					str = terminal.Buffer.TranslateBufferLineToString (end.Y, true, 0, end.X).Replace (nullStr, spaceStr);
					if (!isWrapped) {
						builder.AppendLine ();
					}

					builder.Append (str.ToString ());
				}
			}

			return builder.ToString ();
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
