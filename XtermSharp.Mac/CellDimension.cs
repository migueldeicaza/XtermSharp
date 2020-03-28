using System;
using AppKit;
using CoreGraphics;
using CoreText;
using Foundation;

namespace XtermSharp.Mac {
	/// <summary>
	/// The dimensions of a single character cell in the terminal view
	/// </summary>
	struct CellDimension {
		public CellDimension (TerminalFonts fonts)
		{
			if (fonts == null)
				throw new ArgumentNullException (nameof (fonts));

			var textBounds = ComputeCellDimensions (fonts.Normal);

			Width = textBounds.Width;
			Height = (nfloat)Math.Round(textBounds.Height);
			Offset = textBounds.Y;
		}

		/// <summary>
		/// Gets the width of a character cell
		/// </summary>
		public nfloat Width { get; }

		/// <summary>
		/// Gets the height of a character cell
		/// </summary>
		public nfloat Height { get; }

		/// <summary>
		/// Gets the offset of a character cell
		/// </summary>
		nfloat Offset { get; }

		/// <summary>
		/// Given a row, returns the lower Y position
		/// </summary>
		public nfloat GetRowPos(int row)
		{
			return (row * Height) + Height;
		}

		/// <summary>
		/// Given a row, returns the lower left Y position relative to the content frame
		/// </summary>
		public CGPoint GetTextRowPosInContentFrame (int row, CGRect contentFrame)
		{
			return new CGPoint(contentFrame.X, (contentFrame.Height + contentFrame.Y) - GetTextRowPos(row));
		}

		/// <summary>
		/// Given a row, returns the Y position for the top of the row
		/// </summary>
		public nfloat GetColPos (int col)
		{
			return (col * Width);
		}

		/// <summary>
		/// Computes the cell dimensions for a given font
		/// </summary>
		static CGRect ComputeCellDimensions (NSFont font)
		{
			var line = new CTLine (new NSAttributedString ("W", new NSStringAttributes () { Font = font }));
			return line.GetBounds (CTLineBoundsOptions.UseOpticalBounds);
		}

		/// <summary>
		/// Given a row, returns the lower Y position where 0,0 is the upper left corner
		/// </summary>
		nfloat GetTextRowPos (int row)
		{
			return (row * Height) + Height + Offset;
		}
	}
}
