using System;
using AppKit;
using CoreGraphics;
using CoreAnimation;
using System.Drawing;
using System.Collections.Generic;

namespace XtermSharp.Mac {
	/// <summary>
	/// Implements a view that is used to show the current selection in the terminal
	/// </summary>
	class SelectionView : NSView {
		readonly PointComparer comparer;
		readonly Terminal terminal;
		readonly nfloat rowHeight;
		readonly nfloat colWidth;
		readonly nfloat rowDelta;
		readonly CAShapeLayer maskLayer;
		NSColor selectionColor;

		public SelectionView (Terminal terminal, CGRect rect, CGRect typgographicalBounds) : base (rect)
		{
			this.terminal = terminal;

			comparer = new PointComparer ();
			rowHeight = (int)typgographicalBounds.Height;
			colWidth = typgographicalBounds.Width;
			rowDelta = typgographicalBounds.Y;

			WantsLayer = true;
			maskLayer = new CAShapeLayer ();
			Layer.Mask = maskLayer;
		}

		/// <summary>
		/// Gets or sets the color used to show selection
		/// </summary>
		public NSColor SelectionColor {
			get => selectionColor;
			set {
				selectionColor = value;
				Layer.BackgroundColor = selectionColor.CGColor;
			}
		}

		/// <summary>
		/// Gets or sets the selection start point in buffer coordinates
		/// </summary>
		public Point Start { get; private set; }

		/// <summary>
		/// Gets or sets the selection end point in buffer coordinates
		/// </summary>
		public Point End { get; private set; }

		public void SetStart(int row, int col)
		{
			Start = End = new Point (col, row + terminal.Buffer.YDisp);
			UpdateMask ();
		}

		/// <summary>
		/// Extends the selection based on the user "shift" clicking. This has
		/// slightly different semantics than a "drag" extension because we can
		/// shift the start to be the last prior end point if the new extension
		/// is before the current start point. 
		/// </summary>
		public void ShiftExtend (int row, int col)
		{
			var newEnd = new Point (col, row + terminal.Buffer.YDisp);

			var shouldSwapStart = false;
			if (comparer.Compare(Start, End) < 0) {
				// start is before end, is the new end before Start
				if (comparer.Compare(newEnd, Start) < 0) {
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
			UpdateMask ();
		}

		/// <summary>
		/// Extends the selection by moving the end point to the new point.
		/// This may set the end point
		/// </summary>
		public void DragExtend (int row, int col)
		{
			End = new Point (col, row + terminal.Buffer.YDisp);
			UpdateMask ();
		}

		/// <summary>
		/// Notify that the terminal contents were scrolled and that we need
		/// to update the selection coordinates
		/// </summary>
		public void NotifyScrolled()
		{
			UpdateMask ();
		}

		void UpdateMask ()
		{
			// remove the prior mask
			maskLayer.Path?.Dispose ();

			maskLayer.Frame = Bounds;
			var path = new CGPath ();

			var screenRowStart = Start.Y - terminal.Buffer.YDisp;
			var screenRowEnd = End.Y - terminal.Buffer.YDisp;

			// mask the row that contains the start position
			// snap to either the first or last column depending on
			// where the end position is in relation to the start
			int col = End.X;
			if (screenRowEnd > screenRowStart)
				col = terminal.Cols;
			if (screenRowEnd < screenRowStart)
				col = 0;

			MaskPartialRow (path, screenRowStart, Start.X,  col);

			if (screenRowStart == screenRowEnd) {
				// we're done, only one row to mask
				maskLayer.Path = path;
				return;
			}

			// now mask the row with the end position
			col = Start.X;
			if (screenRowEnd > screenRowStart)
				col = 0;
			if (screenRowEnd < screenRowStart)
				col = terminal.Cols;
			MaskPartialRow (path, screenRowEnd, col, End.X);

			// now mask any full rows in between
			var fullRowCount = screenRowEnd - screenRowStart;
			if (fullRowCount > 1) {
				// Mask full rows up to the last row
				MaskFullRows (path, screenRowStart + 1, fullRowCount-1);
			} else if (fullRowCount < -1) {
				// Mask full rows up to the last row
				MaskFullRows (path, screenRowStart - 0, fullRowCount+1);
			}

			maskLayer.Path = path;
		}

		void MaskFullRows (CGPath path, int rowStart, int rowCount)
		{
			int cursorYOffset = 2;

			nfloat startY = Frame.Height  - ((rowStart + rowCount) * rowHeight - rowDelta - cursorYOffset);
			var pathRect = new CGRect (
				0,
				startY,
				Frame.Width,
				rowHeight * rowCount);

			path.AddRect (pathRect);
		}

		void MaskPartialRow (CGPath path, int row, int colStart, int colEnd)
		{
			// -2 to get the top of the selection to fit over the top of the text properly
			// and to align with the cursor
			const int cursorXPadding = 1;
			int cursorYOffset = 2;

			CGRect pathRect;

			nfloat startY = Frame.Height - rowHeight - (row * rowHeight - rowDelta - cursorYOffset);
			nfloat startX = colStart * colWidth;

			if (colStart == colEnd) {
				// basically the same as the cursor
				pathRect = new CGRect (
					startX - cursorXPadding,
					startY,
					colWidth + (2 * cursorXPadding),
					rowHeight);

				path.AddRect (pathRect);
				return;
			}

			if (colStart < colEnd) {
				// start before the beginning of the start column and end just before the start of the next column
				pathRect = new CGRect (
					startX - cursorXPadding,
					startY,
					((colEnd - colStart) * colWidth) + (2 * cursorXPadding),
					rowHeight);

				path.AddRect (pathRect);
				return;
			}

			// start before the beginning of the _end_ column and end just before the start of the _start_ column
			// note this creates a rect with negative width
			pathRect = new CGRect (
				startX + cursorXPadding,
				startY,
				((colEnd - colStart) * colWidth) - (2 * cursorXPadding),
				rowHeight);

			path.AddRect (pathRect);
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
