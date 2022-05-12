using System;
using System.Drawing;
using AppKit;
using CoreAnimation;
using CoreGraphics;

namespace XtermSharp.Mac {
	/// <summary>
	/// The vew that renders the caret in the view
	/// </summary>
	class CaretView : NSView {
		readonly CAShapeLayer maskLayer;
		CellDimension dimensions;
		Point pos;
		NSColor caretColor;
		bool focused;
		nfloat padding;

		public CaretView (CellDimension dimensions) : base (new CGRect (0, 0, dimensions.Height, dimensions.Width + 2))
		{
			this.dimensions = dimensions;
			pos = new Point (0, 0);
			padding = 1;
			WantsLayer = true;
			CaretColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.5f);

			maskLayer = new CAShapeLayer ();
			Layer.Mask = maskLayer;
			Focused = false;
		}

		/// <summary>
		/// Gets or sets the color used to show selection
		/// </summary>
		public NSColor CaretColor {
			get => caretColor;
			set {
				caretColor = value;
				Layer.BackgroundColor = caretColor.CGColor;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating the additional width to color the caret around the caret position
		/// </summary>
		public nfloat Padding {
			get {
				return padding;
			}

			set {
				padding = value;
				NeedsDisplay = true;
			}
		}

		/// <summary>
		/// Gets or sets the position of the caret and updates the caret mask
		/// </summary>
		public Point Pos {
			get {
				return pos;
			}

			set {
				pos = value;
				UpdateMask ();
				NeedsDisplay = true;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the caret should show focused or non-focused
		/// </summary>
		public bool Focused {
			get {
				return focused;
			}
			set {
				focused = value;
				UpdateMask ();
			}
		}

		/// <summary>
		/// Gets or sets the dimensions of the cells
		/// </summary>
		public CellDimension CellDimensions {
			get {
				return dimensions;
			}

			set {
				dimensions = value;
				UpdateMask ();
			}
		}

		public override NSView HitTest (CGPoint aPoint)
		{
			// we do not want to steal hits, let the terminal view take them
			return null;
		}

		void UpdateMask ()
		{
			// remove the prior mask
			maskLayer.Path?.Dispose ();

			maskLayer.Frame = Bounds;
			var path = new CGPath ();
			var pathRect = new CGRect (
				dimensions.GetColPos (pos.X) - padding,
				Frame.Height - dimensions.GetRowPos (pos.Y),
				dimensions.Width + (2 * padding),
				dimensions.Height);

			if (Focused) {
				path.AddRect (pathRect);
			} else {
				const int caretStroke = 1;

				path.AddLines (new CGPoint [] {
					new CGPoint(pathRect.Left+caretStroke, pathRect.Bottom-caretStroke),
					new CGPoint(pathRect.Right-caretStroke, pathRect.Bottom-caretStroke),
					new CGPoint(pathRect.Right-caretStroke, pathRect.Top+caretStroke),
					new CGPoint(pathRect.Left+caretStroke, pathRect.Top+caretStroke),
					new CGPoint(pathRect.Left+caretStroke, pathRect.Bottom-caretStroke)
				});

				path = path.CopyByStrokingPath (2 * caretStroke, CGLineCap.Square, CGLineJoin.Miter, 4);
			}

			maskLayer.Path = path;
		}
	}
}
