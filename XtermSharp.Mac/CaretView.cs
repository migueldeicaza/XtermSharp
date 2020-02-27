using AppKit;
using CoreGraphics;

namespace XtermSharp.Mac {
	class CaretView : NSView {
		NSColor caretColor;
		bool focused;

		public CaretView (CGRect rect) : base (rect)
		{
			WantsLayer = true;
		}

		/// <summary>
		/// Gets or sets the color used to show selection
		/// </summary>
		public NSColor CaretColor {
			get => caretColor;
			set {
				caretColor = value;
				Layer.BorderColor = caretColor.CGColor;
				if (Focused) {
					Layer.BackgroundColor = caretColor.CGColor;
					Layer.BorderWidth = 0;
				} else {
					Layer.BorderWidth = 1;
				}
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
				if (value) {
					Layer.BackgroundColor = caretColor.CGColor;
					Layer.BorderWidth = 0;

				} else {
					Layer.BackgroundColor = NSColor.Clear.CGColor;
					Layer.BorderWidth = 2;

				}
			}
		}
	}
}
