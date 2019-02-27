using System;
using Foundation;
using CoreGraphics;
using AppKit;
using CoreText;
using ObjCRuntime;

namespace XtermSharp.Mac {
	/// <summary>
	/// An AppKit Terminal View.
	/// </summary>
	public class TerminalView : NSView, INSTextInputClient, INSUserInterfaceValidations, ITerminalDelegate {
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

		public void Feed (byte [] text, int length = -1)
		{
			terminal.Feed (text, length);
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
		    => InterpretKeyEvents (new [] { theEvent });

		[Export ("validAttributesForMarkedText")]
		public NSArray<NSString> ValidAttributesForMarkedText ()
		    => new NSArray<NSString> ();

		[Export ("insertText:replacementRange:")]
		public void InsertText (NSObject text, NSRange replacementRange)
		{
			if (text is NSString str) {
				var data = str.Encode (NSStringEncoding.UTF8);
				UserInput?.Invoke (data.ToArray ());
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

		// Invoked to raise input on the control, which should probably be sent to the actual child process or remote connection
		public Action<byte []> UserInput;

		[Export ("doCommandBySelector:")]
		public void DoCommandBySelector (Selector selector)
		{
			switch (selector.Name){
			case "insertNewline:":
				UserInput?.Invoke (new byte [] { 10 });
				break;
			case "cancelOperation:":
				UserInput?.Invoke (new byte [] { 0x1b });
				break;
			}
			Console.WriteLine ("Got: " + selector.Name);
			NeedsDisplay = true;
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
			context.TextMatrix = textMatrix;

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
