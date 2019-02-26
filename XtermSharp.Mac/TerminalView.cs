using System;
using Foundation;
using CoreGraphics;
using AppKit;
using CoreText;

namespace XtermSharp.Mac {
	public class TerminalView : NSView, ITerminalDelegate {
		static CGAffineTransform textMatrix;

		Terminal terminal;
		NSFont font;

		public TerminalView (CGRect rect) : base (rect)
		{
			terminal = new Terminal (this, null);
			font = NSFont.FromFontName ("Lucida Sans Typewriter", 14);
			textMatrix = CGAffineTransform.MakeIdentity ();
			textMatrix.Scale (1, -1);
		}
		// Flip coordinate system.
		public override bool IsFlipped => true;

		// Simple tester API.
		public void Feed (string text)
		{
			terminal.Feed (System.Text.Encoding.UTF8.GetBytes (text));
		}


		int count;
		public override void DrawRect (CGRect dirtyRect)
		{
			NSColor.White.Set ();
			NSGraphics.RectFill (dirtyRect);

			CGContext context = NSGraphicsContext.CurrentContext.GraphicsPort;
			context.TextMatrix = textMatrix;

			var maxCol = terminal.Cols;
			var maxRow = terminal.Rows;

			for (int row = 0; row < maxRow; row++) {
				context.TextPosition = new CGPoint (0, 15 + row * 15);
				for (int col = 0; col < maxCol; col++) {
					var ch = terminal.Buffer.Lines [row] [col];
					var str = ch.Code == 0 ? new NSAttributedString (" ") : new NSAttributedString ("" + (char) ch.Rune);

					var ctline = new CTLine (str);
				
					ctline.Draw (context);
				}
			}
		}

		void ITerminalDelegate.ShowCursor (Terminal terminal)
		{
		}

		public event Action<TerminalView, string> TitleChanged;

		void ITerminalDelegate.SetTerminalTitle (Terminal source, string title)
		{
			if (TitleChanged != null)
				TitleChanged (this, title);
		}
	}
}
