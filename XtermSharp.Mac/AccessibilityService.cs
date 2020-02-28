using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace XtermSharp.Mac {
	/// <summary>
	/// Handles macOS accessibilty for the terminal view
	/// </summary>
	class AccessibilityService {
		readonly Terminal terminal;
		readonly SelectionService selection;
		AccessibilitySnapshot cache;

		public AccessibilityService (Terminal terminal)
		{
			this.terminal = terminal;
			this.selection = new SelectionService (terminal);

			// TODO: handle terminal.Buffer.scrolled and handle lines being scrolled off the top of the buffer
			// TODO: hook up events in terminal that lets us know when the buffer content has changed
		}

		public AccessibilitySnapshot GetSnapshot ()
		{
			if (cache == null) {
				var result = CalculateSnapshot ();
				cache = result;
			}

			return cache;
		}

		public void Invalidate ()
		{
			cache = null;
		}

		private AccessibilitySnapshot CalculateSnapshot ()
		{
			selection.SelectAll ();

			// TODO: calc visible char range

			var lines = selection.GetSelectedLines ();

			// add a space at the end for the space between the prompt and the caret
			if (terminal.Buffers.IsAlternateBuffer) {
				if (lines.Length > 0) {
					var lastLine = lines [lines.Length - 1];
					lastLine.Add (new LineFragment (" ", lastLine.StartLine, lastLine.Length));
				}
			}

			var count = CountLines (lines);
			int caret = CalculateCaretPosition(count.Item1);

			AccessibilitySnapshot.Range visble = new AccessibilitySnapshot.Range { Start = 0, Length = count.Item2 };

			var result = new AccessibilitySnapshot (lines, visble, caret);

			return result;
		}

		int CalculateCaretPosition(int lengthToLastRow)
		{
			if (terminal.Buffers.IsAlternateBuffer) {
				// TODO: alternate buffer support
				// for now, assuming beginning of last line
				return lengthToLastRow;
			}

			// when the normal buffer is active we always have the caret on the last row of text, somewhere along
			// the line. Buffer.X is that position on the last row. We need to find the beginning of the last line
			// of text

			return lengthToLastRow + terminal.Buffer.X;
		}

		(int, int) CountLines(Line [] lines)
		{
			if (lines.Length == 0)
				return (0, 0);

			int count = 0;
			for (int i = 0; i < lines.Length - 1; i++) {
				count += lines [i].Length;
			}

			return (count, count + lines [lines.Length - 1].Length);
		}
	}

	public class AccessibilitySnapshot {
		readonly Line [] lines;

		public AccessibilitySnapshot (Line[] lines, Range visible, int caret)
		{
			this.lines = lines;
			Text = GetTextFromLines(lines);
			VisibleRange = visible;
			CaretPosition = caret;
		}

		public string Text { get; }

		public Range VisibleRange { get; }

		public int CaretPosition { get; }

		[DebuggerDisplay("{Start}, {Length}")]
		public struct Range {
			public int Start;
			public int Length;
		}

		/// <summary>
		/// Given a range of text in the snapshot, find the start and end points in the buffer
		/// </summary>
		public (Point, Point) FindRange(Range textRange)
		{
			Point start = Point.Empty;
			Point end = Point.Empty;

			int count = 0;
			int endLocation = textRange.Start + textRange.Length;

			for (int i = 0; i < lines.Length; i++) {
				// is the start on this line
				if (count <= textRange.Start && textRange.Start < count + lines[i].Length) {
					var y = lines [i].StartLine;
					// how far along 
					var x = textRange.Start - count;

					start = new Point (x, y);
				}

				// is the end on this line
				if (count <= endLocation && endLocation < count + lines [i].Length) {
					var y = lines [i].StartLine;
					// how far along 
					var x = endLocation - count;

					end = new Point (x, y);
					break;
				}

				// go to next line
				count += lines [i].Length;
			}

			return (start, end);
		}

		string GetTextFromLines(Line [] lines)
		{
			if (lines.Length == 0)
				return string.Empty;

			var builder = new StringBuilder ();
			foreach (var line in lines) {
				line.GetFragmentStrings (builder);
			}

			return builder.ToString ();
		}
	}
}
