using System;
using System.Text;
using System.Collections.Generic;
using Foundation;
using CoreGraphics;
using AppKit;
using CoreText;
using ObjCRuntime;
using CoreFoundation;
using System.Timers;

namespace XtermSharp.Mac {
	/// <summary>
	/// An AppKit Terminal View.
	/// </summary>
	public partial class TerminalView : NSView,
		INSTextInputClient,
		ITerminalDelegate,
		INSUserInterfaceValidations,
		INSAccessibilityStaticText {
		static CGAffineTransform textMatrix;

		readonly Terminal terminal;
		readonly SelectionService selection;
		readonly AccessibilityService accessibility;
		readonly NSView caret, debug;
		readonly SelectionView selectionView;
		readonly NSFont fontNormal, fontItalic, fontBold, fontBoldItalic;
		readonly Timer autoScrollTimer = new Timer (80);

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
			selection = new SelectionService (terminal);
			selection.SelectionChanged += HandleSelectionChanged;
			FullBufferUpdate ();

			var selectColor = NSColor.FromColor (NSColor.Blue.ColorSpace, 0.4f, 0.2f, 0.9f, 0.8f);
			selectionView = new SelectionView (terminal, selection, new CGRect (0, cellDelta, cellHeight, cellWidth), textBounds) {
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

			autoScrollTimer.Elapsed += AutoScrollTimer_Elapsed;

			accessibility = new AccessibilityService (terminal);
			SetupAccessibility ();
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

		void EnsureCaretIsVisible()
		{
			int realCaret = Terminal.Buffer.Y + Terminal.Buffer.YBase;
			int viewportEnd = Terminal.Buffer.YDisp + Terminal.Rows;

			if (realCaret >= viewportEnd || realCaret < Terminal.Buffer.YDisp) {
				ScrollToRow (Terminal.Buffer.YBase);
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
				basBuilder.Append (ch.Code == 0 ? " " : ch.Rune.ToString ().TrimEnd ('\0'));
			}
			res.Append (new NSAttributedString (basBuilder.ToString ().TrimEnd(' '), GetAttributes (attr)));

			return res;
		}

		void FullBufferUpdate ()
		{
			var rows = terminal.Rows;
			if (buffer == null) {
				buffer = new CircularList<NSAttributedString> (terminal.Buffer.Lines.MaxLength);
			} else {
				if (terminal.Buffer.Lines.MaxLength > buffer.MaxLength) {
					buffer.MaxLength = terminal.Buffer.Lines.MaxLength;
				}
			}

			var cols = terminal.Cols;
			for (int row = 0; row < rows; row++) {
				buffer [row] = BuildAttributedString (terminal.Buffer.Lines [row], cols);
			}
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

			accessibility.Invalidate ();
			NSAccessibility.PostNotification (this, NSAccessibilityNotifications.ValueChangedNotification);
			NSAccessibility.PostNotification (this, NSAccessibilityNotifications.SelectedTextChangedNotification);
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
			case "selectAll:":
				return true;
			case "copy:":
				return selection.Active;
			}

			//Console.WriteLine ("Validating " + selector);
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
			var str = selection.GetSelectedText();

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
			selection.SelectAll ();
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
			selection.Active = false;

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
			EnsureCaretIsVisible ();
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
				//Console.WriteLine ("Unhandled key event: " + selector.Name);
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
			actualRange = range;
			return CallSafely (() =>
			{
				var rect = caret.Frame;

				return this.Window.ConvertRectToScreen (
				    this.ConvertRectToView (
					new CGRect (
					    rect.Left,
					    rect.Top,
					    rect.Width,
					    rect.Height),
					null));
			});
		}

		T CallSafely<T> (Func<T> call, T valueOnThrow = default)
		{
			try {
				return call ();
			} catch (Exception e) {
				return valueOnThrow;
			}
		}

		#endregion

		#region Accessibility
		public override bool AccessibilityEnabled { get; set; } = true;

		// --> NSAccessibilityStaticText

		string INSAccessibilityStaticText.AccessibilityValue => GetAccessibiliyText ();

		[Export ("accessibilityAttributedStringForRange:")]
		public override NSAttributedString GetAccessibilityAttributedString (NSRange range)
		{
			return new NSAttributedString (GetAccessibilityString (range));
		}

		public override nint AccessibilityNumberOfCharacters {
			get {
				var snapshot = accessibility.GetSnapshot ();
				return snapshot.Text.Length;
			}

			set => base.AccessibilityNumberOfCharacters = value;
		}

		public override NSRange AccessibilityVisibleCharacterRange {
			get {
				var snapshot = accessibility.GetSnapshot ();
				return new NSRange (snapshot.VisibleRange.Start, snapshot.VisibleRange.Length);
			}
			set {
				base.AccessibilityVisibleCharacterRange = value;
			}
		}
		// <-- NSAccessibilityStaticText

		public override string AccessibilitySelectedText {
			get {
				return "";
			}
			set => base.AccessibilitySelectedText = value;
		}

		public override NSRange AccessibilitySelectedTextRange {
			get {
				// here we are just returning the caret position
				var snapshot = accessibility.GetSnapshot ();
				return new NSRange (snapshot.CaretPosition, 0);
			}
			set {
				// not implemented
			}
		}

		public override string GetAccessibilityString (NSRange range)
		{
			try {
				var snapshot = accessibility.GetSnapshot ();
				
				var safeLength = Math.Min (snapshot.Text.Length, (int)range.Length);
				var text = snapshot.Text.Substring ((int)range.Location, safeLength);
				return text;
			} catch (Exception e) {
				// sometimes, Mac OS calls us with a weird range, and that range
				// seems to then trigger an exception. 
				return string.Empty;
			}
		}

		string GetAccessibiliyText()
		{
			return accessibility.GetSnapshot ().Text;
		}

		void SetupAccessibility ()
		{
			AccessibilityRole = NSAccessibilityRoles.TextAreaRole;
			AccessibilityLabel = "shell";
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

				context.TextPosition = new CGPoint (Frame.Width - 70, baseLine - (cellHeight + row * cellHeight));
				ctline = new CTLine (new NSAttributedString ((attrLine.Length).ToString ()));
				ctline.Draw (context);

				//context.TextPosition = new CGPoint (Frame.Width - 80, baseLine - (cellHeight + row * cellHeight));
				///ctline = new CTLine (new NSAttributedString ((yDisp).ToString ()));
				//ctline.Draw (context);
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

		public override void MouseDown (NSEvent theEvent)
		{
			if (terminal.MouseEvents) {
				SharedMouseEvent (theEvent, down: true);
				return;
			}

			autoScrollTimer.AutoReset = true;

			autoScrollTimer.Enabled = true;
		}

		bool didSelectionDrag;

		public override void MouseUp (NSEvent theEvent)
		{
			autoScrollTimer.Enabled = false;

			if (terminal.MouseEvents) {
				if (terminal.MouseSendsRelease)
					SharedMouseEvent (theEvent, down: false);

				return;
			}

			CalculateMouseHit (theEvent, true, out var col, out var row);
			if (!selection.Active) {
				if (theEvent.ModifierFlags.HasFlag(NSEventModifierMask.ShiftKeyMask)) {
					selection.ShiftExtend (row, col);
				} else {
					selection.SetSoftStart (row, col);
				}
			} else {
				if (!didSelectionDrag) {
					if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ShiftKeyMask)) {
						selection.ShiftExtend (row, col);
					} else {
						selection.Active = false;
						selection.SetSoftStart (row, col);
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

			if (!selection.Active) {
				selection.StartSelection (row, col);
			} else {
				selection.DragExtend (row, col);
			}

			didSelectionDrag = true;

			autoScrollDelta = 0;
			if (selection.Active) {
				if (row <= 0) {
					autoScrollDelta = CalcVelocity (row * -1) * -1;
				} else if (row >= terminal.Rows) {
					autoScrollDelta = CalcVelocity (row - terminal.Rows);
				}
			}
		}

		public override void ScrollWheel (NSEvent theEvent)
		{
			if (theEvent.Type == NSEventType.ScrollWheel) {
				if (theEvent.DeltaY == 0)
					return;

				// simple velocity calculation, could be better
				int velocity = CalcVelocity((int)Math.Abs (theEvent.DeltaY));
				
				if (theEvent.DeltaY > 0) {
					ScrollUp (velocity);
				} else {
					ScrollDown (velocity);
				}
			}
		}

		int autoScrollDelta = 0;

		private void AutoScrollTimer_Elapsed (object sender, ElapsedEventArgs e)
		{
			if (autoScrollDelta == 0)
				return;

			if (autoScrollDelta < 0) {
				this.BeginInvokeOnMainThread (() => {
					ScrollUp (autoScrollDelta * -1);
				});
			} else {
				this.BeginInvokeOnMainThread (() => {
					ScrollDown (autoScrollDelta);
				});
			}
		}

		/// <summary>
		/// Calculates a velocity for scrolling
		/// </summary>
		int CalcVelocity (int delta)
		{
			// this could be improved I'm sure
			if (delta > 9)
				return Math.Max (Terminal.Rows, 20);

			if (delta > 5)
				return 10;

			if (delta > 1)
				return 3;

			return 1;
		}

		public override void ResetCursorRects ()
		{
			AddCursorRect (Bounds, NSCursor.IBeamCursor);
		}

		void HandleSelectionChanged ()
		{
			if (selection.Active) {
				AddSubview (selectionView);
			} else {
				selectionView.RemoveFromSuperview ();
			}
		}
	}
}
