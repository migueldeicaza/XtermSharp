using System;
using AppKit;
using CoreGraphics;
using CoreAnimation;
using System.Drawing;

namespace XtermSharp.Mac {
	/// <summary>
	/// Implements a view that is used to show the current selection in the terminal
	/// </summary>
	class SelectionView : NSView {
		readonly Terminal terminal;
		readonly SelectionService selection;
		readonly CAShapeLayer maskLayer;

		NSColor selectionColor;
		CellDimension cellDimensions;

		public SelectionView (Terminal terminal, SelectionService selection, CGRect rect, CellDimension cellDimensions) : base (rect)
		{
			if (terminal == null)
				throw new ArgumentNullException (nameof (terminal));
			if (selection == null)
				throw new ArgumentNullException (nameof (selection));
			if (rect == null)
				throw new ArgumentNullException (nameof (rect));
			if (cellDimensions == null)
				throw new ArgumentNullException (nameof (cellDimensions));

			this.terminal = terminal;
			this.selection = selection;
			this.cellDimensions = cellDimensions;

			selection.SelectionChanged += HandleSelectionChanged;

			WantsLayer = true;
			maskLayer = new CAShapeLayer ();
			Layer.Mask = maskLayer;

			SelectionColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.8f);
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

		public CellDimension CellDimensions {
			get {
				return cellDimensions;
			}

			set {
				if (value == null)
					throw new ArgumentNullException (nameof (CellDimensions));

				cellDimensions = value;
				UpdateMask ();
			}
		}

		/// <summary>
		/// Notify that the terminal contents were scrolled and that we need
		/// to update the selection coordinates
		/// </summary>
		public void NotifyScrolled()
		{
			UpdateMask ();
		}

		void HandleSelectionChanged ()
		{
			UpdateMask ();
		}

		void UpdateMask ()
		{
			// remove the prior mask
			maskLayer.Path?.Dispose ();

			maskLayer.Frame = Bounds;
			var path = new CGPath ();

			var screenRowStart = selection.Start.Y - terminal.Buffer.YDisp;
			var screenRowEnd = selection.End.Y - terminal.Buffer.YDisp;

			// mask the row that contains the start position
			// snap to either the first or last column depending on
			// where the end position is in relation to the start
			int col = selection.End.X;
			if (screenRowEnd > screenRowStart)
				col = terminal.Cols;
			if (screenRowEnd < screenRowStart)
				col = 0;

			MaskPartialRow (path, screenRowStart, selection.Start.X,  col);

			if (screenRowStart == screenRowEnd) {
				// we're done, only one row to mask
				maskLayer.Path = path;
				return;
			}

			// now mask the row with the end position
			col = selection.Start.X;
			if (screenRowEnd > screenRowStart)
				col = 0;
			if (screenRowEnd < screenRowStart)
				col = terminal.Cols;
			MaskPartialRow (path, screenRowEnd, col, selection.End.X);

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
			const int cursorXPadding = 1;
			nfloat startY = Frame.Height  - cellDimensions.GetRowPos (rowStart + rowCount - 1);

			var pathRect = new CGRect (
				0,
				startY,
				(terminal.Cols * cellDimensions.Width) + cursorXPadding,
				cellDimensions.Height * rowCount);

			path.AddRect (pathRect);
		}

		void MaskPartialRow (CGPath path, int row, int colStart, int colEnd)
		{
			// -2 to get the top of the selection to fit over the top of the text properly
			// and to align with the cursor
			const int cursorXPadding = 1;

			CGRect pathRect;

			nfloat startY = Frame.Height - cellDimensions.GetRowPos(row);
			nfloat startX = cellDimensions.GetColPos (colStart); 

			if (colStart == colEnd) {
				// basically the same as the cursor
				pathRect = new CGRect (
					startX - cursorXPadding,
					startY,
					cellDimensions.Width + (2 * cursorXPadding),
					cellDimensions.Height);

				path.AddRect (pathRect);
				return;
			}

			if (colStart < colEnd) {
				// start before the beginning of the start column and end just before the start of the next column
				pathRect = new CGRect (
					startX - cursorXPadding,
					startY,
					((colEnd - colStart) * cellDimensions.Width) + (2 * cursorXPadding),
					cellDimensions.Height);

				path.AddRect (pathRect);
				return;
			}

			// start before the beginning of the _end_ column and end just before the start of the _start_ column
			// note this creates a rect with negative width
			pathRect = new CGRect (
				startX + cursorXPadding,
				startY,
				((colEnd - colStart) * cellDimensions.Width) - (2 * cursorXPadding),
				cellDimensions.Height);

			path.AddRect (pathRect);
		}
	}
}
