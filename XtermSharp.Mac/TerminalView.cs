using System;
using System.Text;
using System.Collections.Generic;
using Foundation;
using CoreGraphics;
using AppKit;
using CoreText;
using ObjCRuntime;
using CoreFoundation;

namespace XtermSharp.Mac {
	/// <summary>
	/// An AppKit Terminal View.
	/// </summary>
	public class TerminalView : NSView, INSTextInputClient, INSUserInterfaceValidations, ITerminalDelegate {
		static CGAffineTransform textMatrix;

		readonly Terminal terminal;
		readonly NSView caret, debug;
		readonly SelectionView selectionView;
		readonly NSFont fontNormal, fontItalic, fontBold, fontBoldItalic;

		nfloat cellHeight, cellWidth, cellDelta;
		CircularList<NSAttributedString> buffer;

		public TerminalView (CGRect rect) : base (rect)
		{
			fontNormal = NSFont.FromFontName ("xLucida Sans Typewriter", 14) ?? NSFont.FromFontName ("Courier", 14);
			fontBold = NSFont.FromFontName ("xLucida Sans Typewriter Bold", 14) ?? NSFont.FromFontName ("Courier Bold", 14);
			fontItalic = NSFont.FromFontName ("xLucida Sans Typewriter Oblique", 14) ?? NSFont.FromFontName ("Courier Oblique", 14);
			fontBoldItalic = NSFont.FromFontName ("xLucida Sans Typewriter Bold Oblique", 14) ?? NSFont.FromFontName ("Courier Bold Oblique", 14);
			var textBounds = ComputeCellDimensions ();

			var cols = (int)(rect.Width / cellWidth);
			var rows = (int)(rect.Height / cellHeight);

			terminal = new Terminal (this, new TerminalOptions () { Cols = cols, Rows = rows });
			FullBufferUpdate ();

			var selectColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.8f);
			selectionView = new SelectionView (terminal, new CGRect (0, cellDelta, cellHeight, cellWidth), textBounds) {
				SelectionColor = selectColor,
			};

			caret = new NSView (new CGRect (0, cellDelta, cellHeight, cellWidth)) {
				WantsLayer = true
			};
			AddSubview (caret);
			debug = new NSView (new CGRect (0, 0, 10, 10)) {
				WantsLayer = true
			};
			//AddSubview (debug);

			var caretColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.5f);

			caret.Layer.BackgroundColor = caretColor.CGColor;

			debug.Layer.BackgroundColor = caretColor.CGColor;

			terminal.Scrolled += Terminal_Scrolled;
			terminal.Buffers.Activated += Buffers_Activated;
		}

		/// <summary>
		/// Gets the Terminal object that is being used for this terminal
		/// </summary>
		/// <value>The terminal.</value>
		public Terminal Terminal => terminal;

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="T:XtermSharp.Mac.TerminalView"/> treats the "Alt/Option" key on the mac keyboard as a meta key,
		/// which has the effect of sending ESC+letter when Meta-letter is pressed.   Otherwise, it passes the keystroke that MacOS provides from the OS keyboard.
		/// </summary>
		/// <value><c>true</c> if option acts as a meta key; otherwise, <c>false</c>.</value>
		public bool OptionAsMetaKey { get; set; } = true;

		/// <summary>
		/// Gets a value indicating the relative position of the terminal viewport
		/// </summary>
		public double ScrollPosition {
			get {
				if (Terminal.Buffers.IsAlternateBuffer)
					return 0;

				// strictly speaking these ought not to be outside these bounds
				if (Terminal.Buffer.YDisp <= 0)
					return 0;

				var maxScrollback = Terminal.Buffer.Lines.Length - Terminal.Rows;
				if (Terminal.Buffer.YDisp >= maxScrollback)
					return 1;

				return (double)Terminal.Buffer.YDisp / (double)maxScrollback;
			}
		}

		/// <summary>
		/// Gets a value indicating the scroll thumbsize
		/// </summary>
		public float ScrollThumbsize {
			get {
				if (Terminal.Buffers.IsAlternateBuffer)
					return 0;

				// the thumb size is the proportion of the visible content of the
				// entire content but don't make it too small
				return Math.Max((float)Terminal.Rows / (float)Terminal.Buffer.Lines.Length, 0.01f);
			}
		}

		/// <summary>
		/// Gets a value indicating whether or not the user can scroll the terminal contents
		/// </summary>
		public bool CanScroll {
			get {
				var shouldBeEnabled = !terminal.Buffers.IsAlternateBuffer;
				shouldBeEnabled = shouldBeEnabled && terminal.Buffer.HasScrollback;
				shouldBeEnabled = shouldBeEnabled && terminal.Buffer.Lines.Length > terminal.Rows;
				return shouldBeEnabled;
			}
		}

		public event Action<double> TerminalScrolled;

		public event Action<bool> CanScrollChanged;

		bool userScrolling;
		public void ScrollToPosition (double position)
		{
			userScrolling = true;
			try {
				var oldPosition = Terminal.Buffer.YDisp;

				var maxScrollback = Terminal.Buffer.Lines.Length - Terminal.Rows;
				int newScrollPosition = (int)(maxScrollback * position);
				if (newScrollPosition < 0)
					newScrollPosition = 0;
				if (newScrollPosition > maxScrollback)
					newScrollPosition = maxScrollback;

				if (newScrollPosition != oldPosition) {
					ScrollToRow (newScrollPosition);
				}
			} finally {
				userScrolling = false;
			}
		}

		public void PageUp()
		{
			ScrollUp (Terminal.Rows);
		}

		public void PageDown ()
		{
			ScrollDown (Terminal.Rows);
		}

		public void ScrollUp (int lines)
		{
			int newPosition = Math.Max (Terminal.Buffer.YDisp - lines, 0);
			ScrollToRow (newPosition);
		}

		public void ScrollDown (int lines)
		{
			int newPosition = Math.Min (Terminal.Buffer.YDisp + lines, Terminal.Buffer.Lines.Length - Terminal.Rows);
			ScrollToRow (newPosition);
		}

		void ScrollToRow(int row)
		{
			if (row != Terminal.Buffer.YDisp) {
				Terminal.Buffer.YDisp = row;

				// tell the terminal we want to refresh all the rows
				Terminal.Refresh (0, Terminal.Rows);

				// do the display update
				UpdateDisplay ();

				selectionView.NotifyScrolled ();
				TerminalScrolled?.Invoke (ScrollPosition);
			}
		}

		void Terminal_Scrolled (Terminal terminal, int yDisp)
		{
			selectionView.NotifyScrolled ();
			TerminalScrolled?.Invoke (ScrollPosition);
		}

		void Buffers_Activated (Buffer active, Buffer inactive)
		{
			CanScrollChanged?.Invoke (CanScroll);
		}

		CGRect ComputeCellDimensions ()
		{
			var line = new CTLine (new NSAttributedString ("W", new NSStringAttributes () { Font = fontNormal }));
			var bounds = line.GetBounds (CTLineBoundsOptions.UseOpticalBounds);
			cellWidth = bounds.Width;
			cellHeight = (int)bounds.Height;
			cellDelta = bounds.Y;

			return bounds;
		}

		StringBuilder basBuilder = new StringBuilder ();

		NSColor [] colors = new NSColor [257];

		NSColor MapColor (int color, bool isFg)
		{
			// The default color
			if (color == Renderer.DefaultColor) {
				if (isFg)
					return NSColor.Black;
				else
					return NSColor.Clear;
			} else if (color == Renderer.InvertedDefaultColor) {
				if (isFg)
					return NSColor.White;
				else
					return NSColor.Black;
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

			NSFont font;
			if (flags.HasFlag (FLAGS.BOLD)){
				if (flags.HasFlag (FLAGS.ITALIC))
					font = fontBoldItalic;
				else
					font = fontBold;
			} else if (flags.HasFlag (FLAGS.ITALIC))
				font = fontItalic;
			else
				font = fontNormal;
			
			var nsattr = new NSStringAttributes () { Font = font, ForegroundColor = MapColor (fg, true),  BackgroundColor = MapColor (bg, false)  };
			if (flags.HasFlag (FLAGS.UNDERLINE)) {
				nsattr.UnderlineColor = nsattr.ForegroundColor;
				nsattr.UnderlineStyle = (int) NSUnderlineStyle.Single;
		
			}
			attributes [attribute] = nsattr;
			return nsattr;
		}

		NSAttributedString BuildAttributedString (BufferLine line, int cols)
		{
			if (line == null) {
				return new NSAttributedString (string.Empty, GetAttributes (CharData.Null.Attribute));
			}

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
				basBuilder.Append (ch.Code == 0 ? " " : ch.Rune.ToString());
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
			caret.Frame = new CGRect (
				// -1 to pad outside the character a little bit
				terminal.Buffer.X * cellWidth - 1,
				// -2 to get the top of the selection to fit over the top of the text properly
				// and to align with the cursor
				Frame.Height - cellHeight - ((terminal.Buffer.Y + terminal.Buffer.YBase - terminal.Buffer.YDisp) * cellHeight - cellDelta - 2),
				// +2 to pad outside the character a little bit on the other side
				cellWidth + 2,
				cellHeight + 0);
		}

		void UpdateDisplay ()
		{
			terminal.GetUpdateRange (out var rowStart, out var rowEnd);
			terminal.ClearUpdateRange ();
			var cols = terminal.Cols;
			var tb = terminal.Buffer;
			for (int row = rowStart; row <= rowEnd; row++) {
				buffer [row + tb.YDisp] = BuildAttributedString (terminal.Buffer.Lines [row + tb.YDisp], cols);
			}
			//var baseLine = Frame.Height - cellDelta;
			// new CGPoint (0, baseLine - (cellHeight + row * cellHeight));
			UpdateCursorPosition ();

			// Should compute the rectangle instead
			//Console.WriteLine ($"Dirty range: {rowStart},{rowEnd}");
			var region = new CGRect (0, Frame.Height - cellHeight - (rowEnd * cellHeight - cellDelta - 1), Frame.Width, (cellHeight - cellDelta) * (rowEnd-rowStart+1));

			//debug.Frame = region;
			SetNeedsDisplayInRect (region);
			//Console.WriteLine ("Dirty rectangle: " + region);
			pendingDisplay = false;
		}

		// Flip coordinate system.
		//public override bool IsFlipped => true;

		// Simple tester API.
		public void Feed (string text)
		{
			terminal.Feed (Encoding.UTF8.GetBytes (text));
			QueuePendingDisplay ();
		}

		// 
		// The code below is intended to not repaint too often, which can produce flicker, for example
		// when the user refreshes the display, and this repains the screen, as dispatch delivers data
		// in blocks of 1024 bytes, which is not enough to cover the whole screen, so this delays
		// the update for a 1/600th of a secon.
		bool pendingDisplay;
		void QueuePendingDisplay ()
		{
			// throttle
			if (!pendingDisplay) {
				pendingDisplay = true;
				DispatchQueue.CurrentQueue.DispatchAfter (new DispatchTime (DispatchTime.Now, 16670000*2), UpdateDisplay);
			}
		}

		public void Feed (byte [] text, int length = -1)
		{
			terminal.Feed (text, length);

			// The problem is calling UpdateDisplay here, because there is still data pending.
			QueuePendingDisplay ();
		}

		public void Feed (IntPtr buffer, int length)
		{
			terminal.Feed (buffer, length);
			QueuePendingDisplay ();
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

				var newRows = (int) (value.Height / cellHeight);
				var newCols = (int) (value.Width / cellWidth);

				if (newCols != terminal.Cols || newRows != terminal.Rows) {
					terminal.Resize (newCols, newRows);
					FullBufferUpdate ();
				}

				// make the selection view the entire visible portion of the view
				// we will mask the selected text that is visible to the user
				selectionView.Frame = Bounds;

				UpdateCursorPosition ();
				// It might seem like this wrong place to call Loaded, and that
				// ViewDidMoveToSuperview might make more sense
				// but Editor code expects Loaded to be called after ViewportWidth and ViewportHeight are set
				if (!loadedCalled) {
					loadedCalled = true;
					Loaded?.Invoke ();
				}

				SizeChanged?.Invoke (newCols, newRows);
			}
		}

		/// <summary>
		///  This event is raised when the terminal size has change, due to a NSView frame changed.
		/// </summary>
		public event Action<int, int> SizeChanged;

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
			case "paste:":
				return true;
			case "copy:":
				// TODO: tell if we are actually selecting something
				return true;
			}

			Console.WriteLine ("Validating " + selector);
			return false;
		}

		[Export ("cut:")]
		void Cut (NSObject sender)
		{
		}

		[Export ("copy:")]
		void Copy (NSObject sender)
		{
			// find the selected range of text in the buffer and put in the clipboard
			var str = selectionView.GetSelectedText();

			var clipboard = NSPasteboard.GeneralPasteboard;
			clipboard.ClearContents ();
			clipboard.SetStringForType (str, NSPasteboard.NSPasteboardTypeString);
		}

		[Export ("paste:")]
		void Paste (NSObject sender)
		{
			var clipboard = NSPasteboard.GeneralPasteboard;
			var text = clipboard.GetStringForType (NSPasteboard.NSPasteboardTypeString);
			InsertText (text, new NSRange(0, 0));
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
			var eventFlags = theEvent.ModifierFlags;

			// Handle Option-letter to send the ESC sequence plus the letter as expected by terminals
			if (eventFlags.HasFlag (NSEventModifierMask.AlternateKeyMask) && OptionAsMetaKey) {
				var rawCharacter = theEvent.CharactersIgnoringModifiers;
				Send (EscapeSequences.CmdEsc);
				Send (Encoding.UTF8.GetBytes (rawCharacter));
				return;
			} else if (eventFlags.HasFlag (NSEventModifierMask.ControlKeyMask)) {
				// Sends the control sequence
				var ch = theEvent.CharactersIgnoringModifiers;
				if (ch.Length == 1) {
					var d = Char.ToUpper (ch [0]);
					if (d >= 'A' && d <= 'Z')
						Send (new byte [] { (byte)(d - 'A' + 1) });
					return;
				} 
			} else if (eventFlags.HasFlag (NSEventModifierMask.FunctionKeyMask)) {
				var ch = theEvent.CharactersIgnoringModifiers;
				if (ch.Length == 1) {
					NSFunctionKey code = (NSFunctionKey)ch [0];
					switch (code) {
					case NSFunctionKey.F1:
						Send (EscapeSequences.CmdF [0]);
						break;
					case NSFunctionKey.F2:
						Send (EscapeSequences.CmdF [1]);
						break;
					case NSFunctionKey.F3:
						Send (EscapeSequences.CmdF [2]);
						break;
					case NSFunctionKey.F4:
						Send (EscapeSequences.CmdF [3]);
						break;
					case NSFunctionKey.F5:
						Send (EscapeSequences.CmdF [4]);
						break;
					case NSFunctionKey.F6:
						Send (EscapeSequences.CmdF [5]);
						break;
					case NSFunctionKey.F7:
						Send (EscapeSequences.CmdF [6]);
						break;
					case NSFunctionKey.F8:
						Send (EscapeSequences.CmdF [7]);
						break;
					case NSFunctionKey.F9:
						Send (EscapeSequences.CmdF [8]);
						break;
					case NSFunctionKey.F10:
						Send (EscapeSequences.CmdF [9]);
						break;
					case NSFunctionKey.F11:
						Send (EscapeSequences.CmdF [10]);
						break;
					case NSFunctionKey.F12:
						Send (EscapeSequences.CmdF [11]);
						break;
					case NSFunctionKey.Delete:
						Send (EscapeSequences.CmdDelKey);
						break;
					case NSFunctionKey.UpArrow:
						Send (EscapeSequences.MoveUpNormal);
						break;
					case NSFunctionKey.DownArrow:
						Send (EscapeSequences.MoveDownNormal);
						break;
					case NSFunctionKey.LeftArrow:
						Send (EscapeSequences.MoveLeftNormal);
						break;
					case NSFunctionKey.RightArrow:
						Send (EscapeSequences.MoveRightNormal);
						break;
					case NSFunctionKey.PageUp:
						PageUp ();
						break;
					case NSFunctionKey.PageDown:
						PageDown ();
						break;
					}
				}
				return;
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

		void InsertText (string text, NSRange replacementRange)
		{
			if (!string.IsNullOrEmpty(text)) {
				var data = Encoding.UTF8.GetBytes (text);
				Send (data);
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

		public void Send (byte [] data)
		{
			UserInput?.Invoke (data);
		}

		

		[Export ("doCommandBySelector:")]
		public void DoCommandBySelector (Selector selector)
		{
			switch (selector.Name){
			case "insertNewline:":
				Send (EscapeSequences.CmdRet);
				break;
			case "cancelOperation:":
				Send (EscapeSequences.CmdEsc);
				break;
			case "deleteBackward:":
				Send (new byte [] { 0x7f });
				break;
			case "moveUp:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveUpApp : EscapeSequences.MoveUpNormal);
				break;
			case "moveDown:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveDownApp : EscapeSequences.MoveDownNormal);
				break;
			case "moveLeft:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveLeftApp : EscapeSequences.MoveLeftNormal);
				break;
			case "moveRight:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveRightApp : EscapeSequences.MoveRightNormal);
				break;
			case "insertTab:":
				Send (EscapeSequences.CmdTab);
				break;
			case "insertBackTab:":
				Send (EscapeSequences.CmdBackTab);
				break;
			case "moveToBeginningOfLine:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveHomeApp : EscapeSequences.MoveHomeNormal);
				break;
			case "moveToEndOfLine:":
				Send (terminal.ApplicationCursor ? EscapeSequences.MoveEndApp : EscapeSequences.MoveEndNormal);
				break;
			case "noop:":
				ProcessUnhandledEvent (NSApplication.SharedApplication.CurrentEvent);
				break;

				// Here the semantics depend on app mode, if set, then we function as scroll up, otherwise the modifier acts as scroll up.
			case "pageUp:":
				if (terminal.ApplicationCursor)
					Send (EscapeSequences.CmdPageUp);
				else {
					// TODO: view should scroll one page up.
				}
				break;

			case "pageUpAndModifySelection":
				if (terminal.ApplicationCursor){
					// TODO: view should scroll one page up.
				}
				else
					Send (EscapeSequences.CmdPageUp);
				break;
			case "pageDown:":
				if (terminal.ApplicationCursor)
					Send (EscapeSequences.CmdPageDown);
				else {
					// TODO: view should scroll one page down
				}
				break;
			case "pageDownAndModifySelection:":
				if (terminal.ApplicationCursor) {
					// TODO: view should scroll one page up.
				} else
					Send (EscapeSequences.CmdPageDown);
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
			//Console.WriteLine ($"DrawRect: {dirtyRect}");
			NSColor.White.Set ();
			NSGraphics.RectFill (dirtyRect);

			CGContext context = NSGraphicsContext.CurrentContext.GraphicsPort;
			context.SaveState ();

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
				var attrLine = buffer [row + yDisp];
				if (attrLine == null)
					continue;
				var ctline = new CTLine (attrLine);

				ctline.Draw (context);

#if DEBUG_DRAWING
				// debug code
				context.TextPosition = new CGPoint (Frame.Width - 40, baseLine - (cellHeight + row * cellHeight));
				ctline = new CTLine (new NSAttributedString ((row).ToString ()));
				ctline.Draw (context);

				context.TextPosition = new CGPoint (Frame.Width - 60, baseLine - (cellHeight + row * cellHeight));
				ctline = new CTLine (new NSAttributedString ((Terminal.Buffer.YBase).ToString ()));
				ctline.Draw (context);

				context.TextPosition = new CGPoint (Frame.Width - 80, baseLine - (cellHeight + row * cellHeight));
				ctline = new CTLine (new NSAttributedString ((yDisp).ToString ()));
				ctline.Draw (context);
#endif

			}

			context.RestoreState ();
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

		void ITerminalDelegate.SizeChanged (Terminal source)
		{
			throw new NotImplementedException ();
		}

		void ComputeMouseEvent (NSEvent theEvent, bool down, out int buttonFlags)
		{
			var flags = theEvent.ModifierFlags;

			buttonFlags = terminal.EncodeButton (
				(int)theEvent.ButtonNumber, release: false,
				shift: flags.HasFlag (NSEventModifierMask.ShiftKeyMask),
				meta: flags.HasFlag (NSEventModifierMask.AlternateKeyMask),
				control: flags.HasFlag (NSEventModifierMask.ControlKeyMask));
		}

		void SharedMouseEvent (NSEvent theEvent, bool down)
		{
			CalculateMouseHit (theEvent, down, out var col, out var row);
			ComputeMouseEvent (theEvent, down, out var buttonFlags);
			terminal.SendEvent (buttonFlags, col, row);
		}

		void CalculateMouseHit (NSEvent theEvent, bool down, out int col, out int row)
		{
			var point = theEvent.LocationInWindow;
			col = (int)(point.X / cellWidth);
			row = (int)((Frame.Height - point.Y) / cellHeight);
		}

		/* Mouse selection:
		 * 
		 * selectionStart. this is updated when we receive a mouse down event and the user is not pressing
		 * shift or is currently dragging.
		 * 
		 * selectionEnd. this is set when the user drags and is currently selecting, or when we receive a mouse down
		 * while the user holds (just) the shift key.
		 * 
		 * selecting. this is set true when the user drags the mouse after a mouse down event and is set false when
		 * a mouse up event is received
		 *
		 * Notes: we need to handle when the selection range exceeds the visible buffer, or when we need to scrollback
		 *
		 * When we start typing, selection is turned off
		 */
		bool selecting;
		bool didSelectionDrag;

		void StartSelection ()
		{
			selecting = true;
			AddSubview (selectionView);

			selectionView.SetStart (selectionView.Start.Y, selectionView.Start.X);
		}

		void StartSelection (int row, int col)
		{
			selecting = true;
			AddSubview (selectionView);

			selectionView.SetStart (row, col);
		}

		void StopSelecting ()
		{
			selecting = false;
			selectionView.RemoveFromSuperview ();
		}

		public override void MouseDown (NSEvent theEvent)
		{
			CalculateMouseHit (theEvent, down: true, out var col, out var row);

			if (terminal.MouseEvents) {
				SharedMouseEvent (theEvent, down: true);
				return;
			}

			if (theEvent.ModifierFlags == NSEventModifierMask.ShiftKeyMask) {
				if (!selecting) {
					StartSelection ();
					selectionView.ShiftExtend (row, col);
				}
			} else if (theEvent.ModifierFlags == 0) {
			}
		}

		public override void MouseUp (NSEvent theEvent)
		{
			if (terminal.MouseEvents) {
				if (terminal.MouseSendsRelease)
					SharedMouseEvent (theEvent, down: false);

				return;
			}

			CalculateMouseHit (theEvent, true, out var col, out var row);
			if (!selecting) {
				if (theEvent.ModifierFlags.HasFlag(NSEventModifierMask.ShiftKeyMask)) {
					// using the current selection point, or the current cursor position (or top of view)
					// calculate the selection end and set the selection on
					StartSelection ();
					selectionView.ShiftExtend (row, col);
					return;
				}

				selectionView.SetStart (row, col);
			} else {
				if (!didSelectionDrag) {
					if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ShiftKeyMask)) {
						// using the current selection point, or the current cursor position (or top of view)
						// calculate the selection end and set the selection on
						selectionView.ShiftExtend (row, col);
					} else {
						StopSelecting ();
						selectionView.SetStart (row, col);
					}
				}
			}

			didSelectionDrag = false;
		}

		public override void MouseDragged (NSEvent theEvent)
		{
			CalculateMouseHit (theEvent, true, out var col, out var row);

			if (terminal.MouseEvents) {
				if (terminal.MouseSendsAllMotion || terminal.MouseSendsMotionWhenPressed) {
					ComputeMouseEvent (theEvent, true, out var buttonFlags);
					terminal.SendMotion (buttonFlags, col, row);
				}

				return;
			}

			if (theEvent.ModifierFlags == NSEventModifierMask.ShiftKeyMask) {
				// extend the current range, otherwise set the initial selection start
			} else if (theEvent.ModifierFlags == 0) {
				if (!selecting) {
					// set initial selection values
					StartSelection (row, col);
				} else {
					selectionView.DragExtend (row, col);
				}
				didSelectionDrag = true;
			}
		}

		public override void ScrollWheel (NSEvent theEvent)
		{
			if (theEvent.Type == NSEventType.ScrollWheel) {

				if (theEvent.DeltaY == 0)
					return;

				var x = Math.Abs(theEvent.DeltaY);

				// simple velocity calculation, could be better
				int velocity = 1;
				if (x > 1)
					velocity = 3;
				if (x > 5)
					velocity = 10;
				if (x > 9)
					velocity = Terminal.Rows;
				
				if (theEvent.DeltaY > 0) {
					ScrollUp (velocity);
				} else {
					ScrollDown (velocity);
				}
			}
		}

		public override void ResetCursorRects ()
		{
			AddCursorRect (Bounds, NSCursor.IBeamCursor);
		}
	}
}
