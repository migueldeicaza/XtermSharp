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
		readonly SelectionService activeSelection;
		AccessibilitySnapshot cache;

		public AccessibilityService (Terminal terminal, SelectionService selectionService)
		{
			this.terminal = terminal;
			this.activeSelection = selectionService;
			this.selection = new SelectionService (terminal);
		}

		public AccessibilitySnapshot GetSnapshot ()
		{
			if (cache == null) {
				Line [] selectedLines = null;
				if (activeSelection.Active) {
					selectedLines = activeSelection.GetSelectedLines ();
				}

				var result = CalculateSnapshot (selectedLines);
				cache = result;
			}

			return cache;
		}

		public void Invalidate ()
		{
			cache = null;
		}

		private AccessibilitySnapshot CalculateSnapshot (Line [] selectedLines = null)
		{
			selection.SelectAll ();

			// TODO: calc visible char range

			var lines = selection.GetSelectedLines ();

			return CalculateSnapshot (lines, selectedLines);
		}

		AccessibilitySnapshot CalculateSnapshot (Line[] lines, Line[] selectedLines)
		{
			// add a space at the end for the space between the prompt and the caret
			if (terminal.Buffers.IsAlternateBuffer) {
				if (lines.Length > 0) {
					var lastLine = lines [lines.Length - 1];
					lastLine.Add (new LineFragment (" ", lastLine.StartLine, lastLine.Length));
				}
			}

			var count = CountLines (lines);
			int caret = CalculateCaretPosition (count.Item1);

			AccessibilitySnapshot.Range selectedRange = new AccessibilitySnapshot.Range();
			if (selectedLines != null) {
				selectedRange = CalculateSelectedTextRange (lines, selectedLines);
			}

			AccessibilitySnapshot.Range visble = new AccessibilitySnapshot.Range { Start = 0, Length = count.Item2 };

			var result = new AccessibilitySnapshot (lines, visble, caret, selectedRange);

			return result;
		}

		AccessibilitySnapshot.Range CalculateSelectedTextRange (Line [] lines, Line [] selectedLines)
		{
			// selectedLines contains the collection of lines that are selected
			// the first line will have a line and offset into the buffer
			// find this offset in the array of all lines
			if (selectedLines.Length == 0) {
				return new AccessibilitySnapshot.Range ();
			}

			var start = selectedLines [0].StartLine;
			var location = selectedLines [0].StartLocation;
			if (location == -1) {
				// for some reason we have the new line character selected as the first char, we
				// can skip it and just use 0
				location = 0;
			}

			int count = 0;
			for (int i = 0; i < lines.Length; i++) {
				if (i < start) {
					count += lines [i].Length;
					continue;
				}

				count += location;
				break;
			}

			int totalCount = 0;
			for (int i = 0; i < selectedLines.Length; i++) {
					totalCount += selectedLines [i].Length;
			}

			return new AccessibilitySnapshot.Range { Start = count, Length = totalCount };
		}

		int CalculateCaretPosition (int lengthToLastRow)
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

		public AccessibilitySnapshot (Line[] lines, Range visible, int caret, Range selected)
		{
			this.lines = lines;
			Text = GetTextFromLines(lines);
			VisibleRange = visible;
			SelectedRange = selected;
			CaretPosition = caret;
		}

		public string Text { get; }

		public Range VisibleRange { get; }

		public Range SelectedRange { get; }

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

		/// <summary>
		/// Given a location, find the line number that contains that location
		/// </summary>
		public int FindLine(int location)
		{
			if (location <= 0 || lines.Length == 0)
				return 0;

			int count = 0;

			for (int i = 0; i < lines.Length; i++) {
				count += lines [i].Length;

				// did we advance past where we care about?
				if (count > location)
					return i;
			}

			return lines.Length - 1;
		}

		/// <summary>
		/// Given a line in the snapshot, find the start and end locations in the buffer
		/// </summary>
		public (int, int) FindRangeForLine (int line)
		{
			// the start is the sum of all the lines before this line
			if (line < 0)
				return (0, 0);

			if (line > lines.Length - 1)
				line = lines.Length - 1;

			int count = 0;
			for (int i = 0; i < line; i++) {
				count += lines [i].Length;
			}

			return (count, lines [line].Length);
		}

		string GetTextFromLines (Line [] lines)
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
