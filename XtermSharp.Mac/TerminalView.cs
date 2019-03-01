using System;
using Foundation;
using CoreGraphics;
using AppKit;
using CoreText;
using ObjCRuntime;
using System.Text;
using System.Collections.Generic;
using XtermSharp;

namespace XtermSharp.Mac {
	/// <summary>
	/// An AppKit Terminal View.
	/// </summary>
	public class TerminalView : NSView, INSTextInputClient, INSUserInterfaceValidations, ITerminalDelegate {
		static CGAffineTransform textMatrix;

		Terminal terminal;
		CircularList<NSAttributedString> buffer;
		NSFont font;
		NSView caret;
		
		nfloat cellHeight, cellWidth, cellDelta;

		public TerminalView (CGRect rect) : base (rect)
		{
			font = NSFont.FromFontName ("Lucida Sans Typewriter", 14);
			ComputeCellDimensions ();

			var cols = (int)(rect.Width / cellWidth);
			var rows = (int)(rect.Height / cellHeight);

			terminal = new Terminal (this, new TerminalOptions () { Cols = cols, Rows = rows });
			FullBufferUpdate ();
			
			caret = new NSView (new CGRect (0, cellDelta, cellHeight, cellWidth)) {
				WantsLayer = true
			};
			AddSubview (caret);

			var caretColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.5f);

			caret.Layer.BackgroundColor = caretColor.CGColor;
		}
		public Terminal Terminal => terminal;

		void ComputeCellDimensions ()
		{
			var line = new CTLine (new NSAttributedString ("W", new NSStringAttributes () { Font = font }));
			var bounds = line.GetBounds (CTLineBoundsOptions.UseOpticalBounds);
			cellWidth = bounds.Width;
			cellHeight = bounds.Height;
			cellDelta = bounds.Y;
		}

		StringBuilder basBuilder = new StringBuilder ();

		NSColor [] colors = new NSColor [257];

		NSColor MapColor (int color, bool isFg)
		{
			// The default color
			if (color == 256) {
				if (isFg)
					return NSColor.Black;
				else
					return NSColor.White;
			}

			if (colors [color] == null) {
				Color tcolor = Color.DefaultAnsiColors [color];

				colors [color] = NSColor.FromCalibratedRgb (tcolor.Red / 255f, tcolor.Green / 255f, tcolor.Blue / 255f);
			}
			return colors [color];
		}

		Dictionary<int, NSStringAttributes> attributes = new Dictionary<int, NSStringAttributes> ();
		NSStringAttributes GetAttributes (int attribute)
		{
			// ((int)flags << 18) | (fg << 9) | bg;
			int bg = attribute & 0x1ff;
			int fg = (attribute >> 9) & 0x1ff;
			var flags = (FLAGS) (attribute >> 18);

			if (flags.HasFlag (FLAGS.INVERSE)) {
				var tmp = bg;
				bg = fg;
				fg = tmp;

				if (fg == Renderer.DefaultColor)
					fg = Renderer.InvertedDefaultColor;
				if (bg == Renderer.DefaultColor)
					bg = Renderer.InvertedDefaultColor;
			}

			if (attributes.TryGetValue (attribute, out var result))
				return result;

			var color = new NSStringAttributes () { Font = font, ForegroundColor = MapColor (fg, true),  BackgroundColor = MapColor (bg, false)  };
			attributes [attribute] = color;
			return color;
		}

		NSAttributedString BuildAttributedString (BufferLine line, int cols)
		{
			var res = new NSMutableAttributedString ();
			int attr = 0;

			basBuilder.Clear ();
			for (int col = 0; col < cols; col++) {
				var ch = line [col];
				if (col == 0)
					attr = ch.Attribute;
				else {
					if (attr != ch.Attribute) {
						res.Append (new NSAttributedString (basBuilder.ToString (), GetAttributes (attr)));
						basBuilder.Clear ();
						attr = ch.Attribute;
					}
				}
				basBuilder.Append (ch.Code == 0 ? ' ' : (char)ch.Rune);
			}
			res.Append (new NSAttributedString (basBuilder.ToString (), GetAttributes (attr)));
			return res;
		}

		void FullBufferUpdate ()
		{
			var rows = terminal.Rows;
			if (buffer == null)
				buffer = new CircularList<NSAttributedString> (terminal.Buffer.Lines.MaxLength);
			var cols = terminal.Cols;
			for (int row = 0; row < rows; row++)
				buffer [row] = BuildAttributedString (terminal.Buffer.Lines [row], cols);
		}

		void UpdateCursorPosition ()
		{
			caret.Frame = new CGRect (terminal.Buffer.X * cellWidth - 1, Frame.Height - cellHeight - (terminal.Buffer.Y * cellHeight - cellDelta - 1), cellWidth + 2, cellHeight + 2);
		}

		void UpdateDisplay ()
		{
			terminal.GetUpdateRange (out var rowStart, out var rowEnd);
			var cols = terminal.Cols;
			var tb = terminal.Buffer;
			for (int row = rowStart; row <= rowEnd; row++) {
				buffer [row + tb.YDisp] = BuildAttributedString (terminal.Buffer.Lines [row + tb.YDisp], cols);
			}
			//var baseLine = Frame.Height - cellDelta;
			// new CGPoint (0, baseLine - (cellHeight + row * cellHeight));
			UpdateCursorPosition ();
			
			// Should compute the rectangle instead
			NeedsDisplay = true;
		}

		// Flip coordinate system.
		//public override bool IsFlipped => true;

		// Simple tester API.
		public void Feed (string text)
		{
			terminal.Feed (Encoding.UTF8.GetBytes (text));
			UpdateDisplay ();
		}

		public void Feed (byte [] text, int length = -1)
		{
			terminal.Feed (text, length);
			UpdateDisplay ();
		}

		public void Feed (IntPtr buffer, int length)
		{
			terminal.Feed (buffer, length);
			UpdateDisplay ();
		}

		NSTrackingArea trackingArea;

		public override void CursorUpdate (NSEvent theEvent)
		    => NSCursor.IBeamCursor.Set ();

		void MakeFirstResponder ()
		{
			Window.MakeFirstResponder (this);
		}

		bool loadedCalled;
		internal event Action Loaded;
		public override CGRect Frame {
			get => base.Frame; set {
				var oldSize = base.Frame.Size;
				base.Frame = value;
				OnSizeChanged (oldSize, value.Size);

				var newRows = (int) (value.Height / cellHeight);
				var newCols = (int) (value.Width / cellWidth);

				if (newRows != terminal.Cols || newRows != terminal.Rows) {
					//terminal.Resize (newCols, newRows);
					//FullBufferUpdate ();
				}

				UpdateCursorPosition ();
				// It might seem like this wrong place to call Loaded, and that
				// ViewDidMoveToSuperview might make more sense
				// but Editor code expects Loaded to be called after ViewportWidth and ViewportHeight are set
				if (!loadedCalled) {
					loadedCalled = true;
					Loaded?.Invoke ();
				}
			}
		}

		void OnSizeChanged (CGSize oldSize, CGSize newSize)
		{
		}

		[Export ("validateUserInterfaceItem:")]
		bool INSUserInterfaceValidations.ValidateUserInterfaceItem (INSValidatedUserInterfaceItem item)
		{
			var selector = item.Action.Name;

			switch (selector) {
			case "performTextFinderAction:":
				switch ((NSTextFinderAction)(long)item.Tag) {
				case NSTextFinderAction.ShowFindInterface:
				case NSTextFinderAction.ShowReplaceInterface:
				case NSTextFinderAction.HideFindInterface:
				case NSTextFinderAction.HideReplaceInterface:
					return true;
				}
				return false;
			}

			Console.WriteLine ("Validating " + selector);
			return false;
		}

		[Export ("cut:")]
		void Cut (NSObject sender)
		{ }

		[Export ("copy:")]
		void Copy (NSObject sender)
		{ }

		[Export ("paste:")]
		void Paste (NSObject sender)
		{
		}

		[Export ("selectAll:")]
		void SelectAll (NSObject sender)
		{
		}

		[Export ("undo:")]
		void Undo (NSObject sender)
		{ }

		[Export ("redo:")]
		void Redo (NSObject sender)
		{
		}

		[Export ("zoomIn:")]
		void ZoomIn (NSObject sender)
		{ }

		[Export ("zoomOut:")]
		void ZoomOut (NSObject sender)
		{ }

		[Export ("zoomReset:")]
		void ZoomReset (NSObject sender)
		{ }

		#region Input / NSTextInputClient

		public override bool BecomeFirstResponder ()
		{

			var response = base.BecomeFirstResponder ();
			if (response) {
				HasFocus = true;
			}
			return response;
		}

		public override bool ResignFirstResponder ()
		{
			var response = base.ResignFirstResponder ();
			if (response) {
				HasFocus = false;
			}
			return response;
		}

		public override bool AcceptsFirstResponder ()
		    => true;

		public override void KeyDown (NSEvent theEvent)
		{
			if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ControlKeyMask)) {
				var ch = theEvent.CharactersIgnoringModifiers;
				if (ch.Length == 1) {
					var d = Char.ToUpper (ch [0]);
					if (d >= 'A' && d <= 'Z')
						Send (new byte [] { (byte)(d - 'A' + 1) });
					return;
				}
			}

			InterpretKeyEvents (new [] { theEvent });
		}

		[Export ("validAttributesForMarkedText")]
		public NSArray<NSString> ValidAttributesForMarkedText ()
		    => new NSArray<NSString> ();

		[Export ("insertText:replacementRange:")]
		public void InsertText (NSObject text, NSRange replacementRange)
		{
			if (text is NSString str) {
				var data = str.Encode (NSStringEncoding.UTF8);
				Send (data.ToArray ());
			}
			NeedsDisplay = true;
		}

		static NSRange notFoundRange = new NSRange (NSRange.NotFound, 0);

		[Export ("hasMarkedText")]
		public bool HasMarkedText ()
		{
			return false;
		}

		[Export ("markedRange")]
		public NSRange MarkedRange ()
		{
			return notFoundRange;
		}

		[Export ("setMarkedText:selectedRange:replacementRange:")]
		public void SetMarkedText (NSObject setMarkedText, NSRange selectedRange, NSRange replacementRange)
		{

		}

		void ProcessUnhandledEvent (NSEvent evt)
		{
			// Handle Control-letter
			if (evt.ModifierFlags.HasFlag (NSEventModifierMask.ControlKeyMask)) {
				
			}
		}

		// Invoked to raise input on the control, which should probably be sent to the actual child process or remote connection
		public Action<byte []> UserInput;

		void Send (byte [] data)
		{
			UserInput?.Invoke (data);
		}

		byte [] cmdNewline = new byte [] { 10 };
		byte [] cmdEsc = new byte [] { 0x1b };
		byte [] cmdDel = new byte [] { 0x7f };
		byte [] moveUpApp = new byte [] { 0x1b, (byte)'O', (byte)'A' };
		byte [] moveUpNormal = new byte [] { 0x1b, (byte)'[', (byte)'A' };
		byte [] moveDownApp = new byte [] { 0x1b, (byte)'O', (byte)'B' };
		byte [] moveDownNormal = new byte [] { 0x1b, (byte)'[', (byte)'B' };
		byte [] moveLeftApp = new byte [] { 0x1b, (byte)'O', (byte)'D' };
		byte [] moveLeftNormal = new byte [] { 0x1b, (byte)'[', (byte)'D' };
		byte [] moveRightApp = new byte [] { 0x1b, (byte)'O', (byte)'C' };
		byte [] moveRightNormal = new byte [] { 0x1b, (byte)'[', (byte)'C' };
		byte [] moveHomeApp = new byte [] { 0x1b, (byte)'O', (byte)'H' };
		byte [] moveHomeNormal = new byte [] { 0x1b, (byte)'[', (byte)'H' };
		byte [] moveEndApp = new byte [] { 0x1b, (byte)'O', (byte)'F' };
		byte [] moveEndNormal = new byte [] { 0x1b, (byte)'[', (byte)'F' };
		byte [] cmdTab = new byte [] { 9 };
		byte [] cmdBackTab = new byte []{ 0x1b, (byte)'[', (byte)'Z' };
		byte [] cmdPageUp = new byte [] { 0x1b, (byte)'[', (byte)'5', (byte)'~' };
		byte [] cmdPageDown = new byte [] { 0x1b, (byte)'[', (byte)'6', (byte)'~' };

		[Export ("doCommandBySelector:")]
		public void DoCommandBySelector (Selector selector)
		{
			switch (selector.Name){
			case "insertNewline:":
				Send (cmdNewline);
				break;
			case "cancelOperation:":
				Send (cmdEsc);
				break;
			case "deleteBackward:":
				Send (new byte [] { 0x7f });
				break;
			case "moveUp:":
				Send (terminal.ApplicationCursor ? moveUpApp : moveUpNormal);
				break;
			case "moveDown:":
				Send (terminal.ApplicationCursor ? moveDownApp : moveDownNormal );
				break;
			case "moveLeft:":
				Send (terminal.ApplicationCursor ?  moveLeftApp : moveLeftNormal);
				break;
			case "moveRight:":
				Send (terminal.ApplicationCursor ? moveRightApp : moveRightNormal);
				break;
			case "insertTab:":
				Send (cmdTab);
				break;
			case "insertBackTab:":
				Send (cmdBackTab);
				break;
			case "moveToBeginningOfLine:":
				Send (terminal.ApplicationCursor ? moveHomeApp: moveHomeNormal);
				break;
			case "moveToEndOfLine:":
				Send (terminal.ApplicationCursor ? moveEndApp : moveEndNormal);
				break;
			case "noop:":
				ProcessUnhandledEvent (NSApplication.SharedApplication.CurrentEvent);
				break;

				// Here the semantics depend on app mode, if set, then we function as scroll up, otherwise the modifier acts as scroll up.
			case "pageUp:":
				if (terminal.ApplicationCursor)
					Send (cmdPageUp);
				else {
					// TODO: view should scroll one page up.
				}
				break;

			case "pageUpAndModifySelection":
				if (terminal.ApplicationCursor){
					// TODO: view should scroll one page up.
				}
				else
					Send (cmdPageUp);
				break;
			case "pageDown:":
				if (terminal.ApplicationCursor)
					Send (cmdPageDown);
				else {
					// TODO: view should scroll one page down
				}
				break;
			case "pageDownAndModifySelection:":
				if (terminal.ApplicationCursor) {
					// TODO: view should scroll one page up.
				} else
					Send (cmdPageDown);
				break;
			default:
				Console.WriteLine ("Unhandled key event: " + selector.Name);
				break;
			}
			
		}

		[Export ("selectedRange")]
		public NSRange SelectedRange => notFoundRange;

		public bool HasFocus { get; private set; }

		[Export ("attributedSubstringForProposedRange:actualRange:")]
		public NSAttributedString AttributedSubstringForProposedRange (NSRange range, out NSRange actualRange)
		{
			actualRange = range;
			return null;
		}

		[Export ("firstRectForCharacterRange:")]
		public CGRect FirstRectForCharacterRange (NSRange range)
		{
			return FirstRectForCharacterRange (range, out var _);
		}

		[Export ("firstRectForCharacterRange:actualRange:")]
		public CGRect FirstRectForCharacterRange (NSRange range, out NSRange actualRange)
		{
			throw new NotImplementedException ();
		}

		#endregion

		int count;
		public override void DrawRect (CGRect dirtyRect)
		{
			NSColor.White.Set ();
			NSGraphics.RectFill (dirtyRect);

			CGContext context = NSGraphicsContext.CurrentContext.GraphicsPort;
			//context.TextMatrix = textMatrix;

#if false
			var maxCol = terminal.Cols;
			var maxRow = terminal.Rows;

			for (int row = 0; row < maxRow; row++) {
				context.TextPosition = new CGPoint (0, 15 + row * 15);
				var str = "";
				for (int col = 0; col < maxCol; col++) {
					var ch = terminal.Buffer.Lines [row] [col];
					str += (ch.Code == 0) ? ' ' : (char)ch.Rune;
				}
				var ctline = new CTLine (new NSAttributedString (str, new NSStringAttributes () { Font = font }));
				
				ctline.Draw (context);
			}
#else
			var maxRow = terminal.Rows;
			var yDisp = terminal.Buffer.YDisp;
			var baseLine = Frame.Height - cellDelta;
			for (int row = 0; row < maxRow; row++) {
				context.TextPosition = new CGPoint (0, baseLine - (cellHeight + row * cellHeight));
				var ctline = new CTLine (buffer [row+yDisp]);

				ctline.Draw (context);
			}
#endif
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
