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
		INSAccessibilityStaticText,
		INSAccessibilityNavigableStaticText {
		static CGAffineTransform textMatrix;

		readonly Terminal terminal;
		readonly SelectionService selection;
		readonly AccessibilityService accessibility;
		readonly SearchService search;
		readonly NSScroller scroller;
		readonly SelectionView selectionView;
		readonly CaretView caret;
		readonly Timer autoScrollTimer = new Timer (80);
		readonly Dictionary<int, NSStringAttributes> attributes = new Dictionary<int, NSStringAttributes> ();

		CircularList<NSAttributedString> buffer;

		TerminalFonts fonts;
		CellDimension cellDimensions;
		NSColor backgroundColor;
		NSColor foregroundColor;
		NSColor invertedForegroundColor;
		nfloat contentPadding;

		public TerminalView (CGRect rect) : base (rect)
		{
			contentPadding = 4;
			backgroundColor = NSColor.White;
			foregroundColor = NSColor.Black;
			invertedForegroundColor = NSColor.White;

			// calculate initial state of the terminal but do not update the display, we are still setting things up
			SetFonts (null, false);

			var viewFrames = CalculateLayouts (rect);

			// get the dimensions of terminal (cols and rows)
			var dimensions = CalculateVisibleRowsAndColumns (cellDimensions, viewFrames.contentFrame);
			var options = new TerminalOptions () { Cols = dimensions.cols, Rows = dimensions.rows };

			// the terminal itself and services
			terminal = new Terminal (this, options);
			selection = new SelectionService (terminal);
			accessibility = new AccessibilityService (terminal, selection);
			search = new SearchService (terminal);

			// scroller
			scroller = new NSScroller (viewFrames.scrollerFrame);
			scroller.ScrollerStyle = NSScrollerStyle.Legacy;
			scroller.DoubleValue = 0.0;
			scroller.KnobProportion = 0.1f;
			scroller.Enabled = false;
			AddSubview (scroller);

			// caret view
			caret = new CaretView (cellDimensions);
			AddSubview (caret);

			// selection view
			selectionView = new SelectionView (terminal, selection, new CGRect (0, 0, rect.Width, rect.Height), cellDimensions);

			// hook up terminal events
			terminal.Scrolled += HandleTerminalScrolled;
			terminal.Buffers.Activated += HandleBuffersActivated;

			// service events
			SetupAccessibility ();
			selection.SelectionChanged += HandleSelectionChanged;

			// UI events
			autoScrollTimer.Elapsed += AutoScrollTimer_Elapsed;
			scroller.Activated += ScrollerActivated;

			// trigger an update of the buffers
			FullBufferUpdate ();
			UpdateDisplay ();
		}

		/// <summary>
		/// Gets the Terminal object that is being used for this terminal
		/// </summary>
		/// <value>The terminal.</value>
		public Terminal Terminal => terminal;

		/// <summary>
		/// Gets the SearchService for this terminal view
		/// </summary>
		public SearchService SearchService => search;

		/// <summary>
		/// Gets the SelectionService for this terminal view
		/// </summary>
		public SelectionService SelectionService => selection;

		/// <summary>
		/// Gets or sets the color of the caret
		/// </summary>
		public NSColor CaretColor { get { return caret.CaretColor; } set { caret.CaretColor = value; } }

		/// <summary>
		/// Gets or sets the color of selected text
		/// </summary>
		public NSColor SelectionColor { get { return selectionView.SelectionColor; } set { selectionView.SelectionColor = value; } }

		public NSColor BackgroundColor {  get {
				return backgroundColor;
			}

			set {
				backgroundColor = value;
				NeedsDisplay = true;
			}
		}

		public NSColor ForegroundColor {
			get {
				return foregroundColor;
			}

			set {
				foregroundColor = value;
				NeedsDisplay = true;
			}
		}

		public NSColor InvertedForegroundColor {
			get {
				return invertedForegroundColor;
			}

			set {
				invertedForegroundColor = value;
				NeedsDisplay = true;
			}
		}

		public nfloat ContentPadding { get {
				return contentPadding;
			}

			set {
				contentPadding = value;
				// trigger a redisplay and relayout
				SetFonts (fonts);
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="T:XtermSharp.Mac.TerminalView"/> treats the "Alt/Option" key on the mac keyboard as a meta key,
		/// which has the effect of sending ESC+letter when Meta-letter is pressed.   Otherwise, it passes the keystroke that MacOS provides from the OS keyboard.
		/// </summary>
		/// <value><c>true</c> if option acts as a meta key; otherwise, <c>false</c>.</value>
		public bool OptionAsMetaKey { get; set; } = true;

		/// <summary>
		/// Gets a value indicating the relative position of the terminal scroller
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

		/// <summary>
		///  This event is raised when the terminal size (cols and rows, width, height) has change, due to a NSView frame changed.
		/// </summary>
		public event Action<int, int, nfloat, nfloat> SizeChanged;

		/// <summary>
		/// Invoked to raise input on the control, which should probably be sent to the actual child process or remote connection 
		/// </summary>
		public Action<byte []> UserInput;

		/// <summary>
		/// Scrolls the terminal contents up by the given number of lines, up is negative, down is positive
		/// </summary>
		public void ScrollLines (int lines)
		{
			Terminal.ScrollLines (lines);
		}

		/// <summary>
		/// Scrolls the terminal contents up one page (Terminal.Rows lines) 
		/// </summary>
		public void PageUp()
		{
			ScrollLines (Terminal.Rows * -1);
		}

		/// <summary>
		/// Scrolls the terminal contents down one page (Terminal.Rows lines) 
		/// </summary>
		public void PageDown ()
		{
			ScrollLines (Terminal.Rows);
		}

		/// <summary>
		/// Searches the given text and returns the number of instances found and selects the first result
		/// </summary>
		public int Search(string txt)
		{
			var snapshot = search.GetSnapshot ();
			int result = snapshot.FindText (txt);

			if (result > 0)
				SelectSearchResult (snapshot.FindNext ());

			return result;
		}

		/// <summary>
		/// Selects the next result, cycling back to the first and returns the index of the currently selected search instance
		/// </summary>
		public int SelectNextSearchResult()
		{
			var snapshot = search.GetSnapshot ();
			if (snapshot.LastSearchResults.Length > 0) {
				SelectSearchResult (snapshot.FindNext ());
				return snapshot.CurrentSearchResult;
			}

			return -1;
		}

		/// <summary>
		/// Selects the previous result, cycling back to the last and returns the index of the currently selected search instance
		/// </summary>
		public int SelectPreviousSearchResult ()
		{
			var snapshot = search.GetSnapshot ();
			if (snapshot.LastSearchResults.Length > 0) {
				SelectSearchResult (snapshot.FindPrevious ());
				return snapshot.CurrentSearchResult;
			}

			return -1;
		}

		StringBuilder basBuilder = new StringBuilder ();

		NSColor [] colors = new NSColor [257];

		NSColor MapColor (int color, bool isFg)
		{
			// The default color
			if (color == Renderer.DefaultColor) {
				if (isFg)
					return foregroundColor;
				else
					return NSColor.Clear;
			} else if (color == Renderer.InvertedDefaultColor) {
				if (isFg)
					return invertedForegroundColor;
				else
					return foregroundColor;
			}

			if (colors [color] == null) {
				Color tcolor = Color.DefaultAnsiColors [color];

				colors [color] = NSColor.FromCalibratedRgb (tcolor.Red / 255f, tcolor.Green / 255f, tcolor.Blue / 255f);
			}
			return colors [color];
		}

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
					font = fonts.BoldItalic;
				else
					font = fonts.Bold;
			} else if (flags.HasFlag (FLAGS.ITALIC))
				font = fonts.Italic;
			else
				font = fonts.Normal;
			
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

			cols = Math.Min (cols, line.Length);

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
			caret.Pos = new System.Drawing.Point (terminal.Buffer.X, terminal.Buffer.Y - terminal.Buffer.YDisp + terminal.Buffer.YBase);
		}

		void UpdateDisplay ()
		{
			UpdateDisplay (true);
		}

		void UpdateDisplay (bool notifyAccessibility)
		{
			terminal.GetUpdateRange (out var rowStart, out var rowEnd);
			terminal.ClearUpdateRange ();

			var cols = terminal.Cols;
			var tb = terminal.Buffer;
			for (int row = rowStart; row <= rowEnd; row++) {
				buffer [row + tb.YDisp] = BuildAttributedString (terminal.Buffer.Lines [row + tb.YDisp], cols);
			}

			UpdateCursorPosition ();
			UpdateScroller ();

			if (rowStart == int.MaxValue || rowEnd < 0) {
				SetNeedsDisplayInRect (Bounds);
			} else {
				var rowY = Frame.Height - contentPadding - cellDimensions.GetRowPos (rowEnd);
				var region = new CGRect (0, rowY, Frame.Width, cellDimensions.Height * (rowEnd - rowStart + 1));

				SetNeedsDisplayInRect (region);
			}

			pendingDisplay = false;

			if (notifyAccessibility) {
				accessibility.Invalidate ();
				NSAccessibility.PostNotification (this, NSAccessibilityNotifications.ValueChangedNotification);
				NSAccessibility.PostNotification (this, NSAccessibilityNotifications.SelectedTextChangedNotification);
			}
		}

		// Flip coordinate system.
		//public override bool IsFlipped => true;

		// Simple tester API.
		public void Feed (string text)
		{
			search.Invalidate ();
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
			search.Invalidate ();
			terminal.Feed (text, length);

			// The problem is calling UpdateDisplay here, because there is still data pending.
			QueuePendingDisplay ();
		}

		public void Feed (IntPtr buffer, int length)
		{
			search.Invalidate ();
			terminal.Feed (buffer, length);
			QueuePendingDisplay ();
		}

		public override void CursorUpdate (NSEvent theEvent)
		    => NSCursor.IBeamCursor.Set ();

		void MakeFirstResponder ()
		{
			Window.MakeFirstResponder (this);
		}

		bool loadedCalled;
		// TODO: is Loaded still needed?
		internal event Action Loaded;
		public override CGRect Frame {
			get => base.Frame; set {
				base.Frame = value;

				ResizeTerminal (value);

				// It might seem like this wrong place to call Loaded, and that
				// ViewDidMoveToSuperview might make more sense
				// but Editor code expects Loaded to be called after ViewportWidth and ViewportHeight are set
				if (!loadedCalled) {
					loadedCalled = true;
					Loaded?.Invoke ();
				}
			}
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
					var d = ch [0];

					byte value;
					switch (d) {
					case char c when c >= 'A' && c <= 'Z':
						value = (byte)(d - 'A' + 1);
						break;
					case char c when c >= 'a' && c <= 'z':
						value = (byte)(d - 'a' + 1);
						break;
					case ' ':
						value = 0;
						break;
					case '\\':
						value = 0x1c;
						break;
					case '_':
						value = 0x1f;
						break;
					case ']':
						value = 0x1d;
						break;
					case '[':
						value = 0x1b;
						break;
					case '^':
						value = 0x1e;
						break;
					default:
						return;
					}

					Send (new byte [] { value });
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
					case NSFunctionKey.PageUp:
						PageUp ();
						break;
					case NSFunctionKey.PageDown:
						PageDown ();
						break;
					default:
						InterpretKeyEvents (new [] { theEvent });
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
		public NSRange SelectedRange {
			get {
				// can we just return this in all cases, or just when selection active?
				if (selection.Active) {
					return AccessibilitySelectedTextRange;
				}

				return notFoundRange;
			}
		}

		bool hasFocus;
		public bool HasFocus {
			get { return hasFocus; }
			private set {
				hasFocus = value;
				caret.Focused = value;
			}
		}

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

		// --> INSAccessibilityNavigableStaticText

		public override nint GetAccessibilityLine (nint index)
		{
			try {
				var snapshot = accessibility.GetSnapshot ();
				var lineNumber = snapshot.FindLine ((int)index);
				return lineNumber;
			} catch (Exception e) {
				// in cases where the text was deleted while voice over was
				// reading it, the OS would request an index that is out of range.
				// While we've since added additional checks, we'll also catch
				// exceptions, and log them.
				return 0;
			}
		}

		public override NSRange GetAccessibilityRangeForLine (nint line)
		{
			try {
				var snapshot = accessibility.GetSnapshot ();
				var locations = snapshot.FindRangeForLine ((int)line);

				return new NSRange (locations.start, locations.end);
			} catch (Exception e) {
				return new NSRange (0, 0);
			}
		}

		// <-- INSAccessibilityNavigableStaticText

		public override CGRect GetAccessibilityFrame (NSRange range)
		{
			// this is called when VO navigates between words and lines in the text
			// that was returned.
			// we need to know what buffer line this range maps to

			var snapshot = accessibility.GetSnapshot ();
			var locations = snapshot.FindRange (new AccessibilitySnapshot.Range { Start = (int)range.Location, Length = (int)range.Length });

			// scroll to ensure range start is visible, try to get the start somewhere in the middle of the view
			if ((locations.start.Y < terminal.Buffer.YDisp) || (locations.start.Y >= terminal.Buffer.YDisp + terminal.Rows)) {
				var newYDisp = Math.Max(locations.start.Y - (terminal.Rows / 2), 0);
				ScrollToYDisp (newYDisp, false);
			}

			// calculate the frame for the start.
			var startPos = GetCaretPos (locations.start.X, locations.start.Y - terminal.Buffer.YDisp);

			nfloat height = Math.Max(locations.end.Y - locations.start.Y, 1) * cellDimensions.Height;
			nfloat width = range.Length * cellDimensions.Width;

			return CallSafely (() => {
				var rect = new CGRect(startPos.x, startPos.y, width, height);

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

		(float x, float y) GetCaretPos (int x, int y)
		{
			var x_ = (x * (float)cellDimensions.Width) + (float)contentPadding;

			var y_ = (float)Frame.Height - (float)cellDimensions.GetRowPos(y) - (float)contentPadding;

			return (x_, y_);
		}

		public override string AccessibilitySelectedText {
			get {
				return string.Empty;
			}
			set => base.AccessibilitySelectedText = value;
		}

		public override NSRange AccessibilitySelectedTextRange {
			get {
				var snapshot = accessibility.GetSnapshot ();

				if (selection.Active) {
					return new NSRange (snapshot.SelectedRange.Start, snapshot.SelectedRange.Length);
				}

				// here we are just returning the caret position
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

		public override void DrawRect (CGRect dirtyRect)
		{
			backgroundColor.Set ();
			NSGraphics.RectFill (dirtyRect);

			CGContext context = NSGraphicsContext.CurrentContext.GraphicsPort;
			context.SaveState ();
			try {
				var contentFrame = CalculateLayouts (Bounds).contentFrame;
#if DEBUG_DRAWING
				DrawDebugFrame ();
				DrawDebugGrid (x.contentFrame);
#endif
				var maxRow = terminal.Rows;
				var yDisp = terminal.Buffer.YDisp;

				for (int row = 0; row < maxRow; row++) {
					var attrLine = buffer [row + yDisp];
					if (attrLine == null)
						continue;

					context.TextPosition = cellDimensions.GetTextRowPosInContentFrame (row, contentFrame);

					using (var ctline = new CTLine (attrLine)) {
						ctline.Draw (context);
					}
#if DEBUG_DRAWING
					DrawDebugContent (context, row, contentFrame);
#endif
				}
			} finally {
				context.RestoreState ();
			}
		}

#if DEBUG_DRAWING
		void DrawDebugContent(CGContext context, int row, CGRect contentFrame)
		{
			// debug code
			var rowY = cellDimensions.GetTextRowPosInContentFrame (row, contentFrame);
			context.TextPosition = new CGPoint (contentFrame.X + contentFrame.Width - (cellDimensions.Width * 3), rowY.Y);
			using (var ctline = new CTLine (new NSAttributedString (row.ToString ()))) {
				ctline.Draw (context);
			}
			//ctline = new CTLine (new NSAttributedString ((row).ToString ()));
			//ctline.Draw (context);

			//context.TextPosition = new CGPoint (Frame.Width - 70, rowY);
			//ctline = new CTLine (new NSAttributedString ((attrLine.Length).ToString ()));
			//ctline.Draw (context);

			//context.TextPosition = new CGPoint (Frame.Width - 80, baseLine - (cellHeight + row * cellHeight));
			///ctline = new CTLine (new NSAttributedString ((yDisp).ToString ()));
			//ctline.Draw (context);
		}

		void DrawDebugFrame ()
		{
			NSColor.Orange.Set ();
			NSGraphics.FrameRectWithWidth (Bounds, 1);
		}

		void DrawDebugGrid(CGRect contentFrame)
		{
			NSColor.Red.Set ();
			NSGraphics.FrameRectWithWidth (contentFrame, 1);

			var maxCol = terminal.Cols;
			var maxRow = terminal.Rows;
			for (int row = 0; row < maxRow; row++) {
				var rowY = (contentFrame.Height + contentFrame.Y) - (cellDimensions.Height * row) - cellDimensions.Height;
				var rowFrame = new CGRect (contentFrame.X, rowY, contentFrame.Width, cellDimensions.Height);

				NSColor.Green.Set ();
				NSGraphics.FrameRectWithWidth (rowFrame, 1);

				for (int col = 0; col < maxCol; col++) {
					var colFrame = new CGRect (contentFrame.X + (col * cellDimensions.Width), rowY, cellDimensions.Width, cellDimensions.Height);
					NSColor.Gray.Set ();
					NSGraphics.FrameRectWithWidth (colFrame, 1);
				}
			}
		}
#endif

#region ITerminalDelegate

		/// <summary>
		/// Raised when the title of the teminal has changed.
		/// </summary>
		public event Action<TerminalView, string> TitleChanged;

		public void Send (byte [] data)
		{
			EnsureCaretIsVisible ();
			UserInput?.Invoke (data);
		}

		void ITerminalDelegate.ShowCursor (Terminal terminal)
		{
		}

		void ITerminalDelegate.SetTerminalTitle (Terminal source, string title)
		{
			if (TitleChanged != null)
				TitleChanged (this, title);
		}

		void ITerminalDelegate.SetTerminalIconTitle (XtermSharp.Terminal source, string title)
		{
		}

		void ITerminalDelegate.SizeChanged (Terminal source)
		{
		}

		string ITerminalDelegate.WindowCommand (XtermSharp.Terminal source, WindowManipulationCommand command, params int [] args)
		{
			return null;
		}

		bool ITerminalDelegate.IsProcessTrusted ()
		{
			return true;
		}

		#endregion

		void ComputeMouseEvent (NSEvent theEvent, bool down, out int buttonFlags)
		{
			var flags = theEvent.ModifierFlags;

			buttonFlags = terminal.EncodeMouseButton (
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
			var point = ConvertPointFromView (theEvent.LocationInWindow, null);
			col = (int)((point.X - contentPadding) / cellDimensions.Width);
			row = (int)((Frame.Height - point.Y - contentPadding) / cellDimensions.Height);
		}

		public override void MouseDown (NSEvent theEvent)
		{
			if (terminal.MouseMode.SendButtonPress()) {
				SharedMouseEvent (theEvent, down: true);
				base.MouseDown (theEvent);
				return;
			}

			autoScrollTimer.AutoReset = true;

			autoScrollTimer.Enabled = true;
			base.MouseDown (theEvent);
		}

		bool didSelectionDrag;

		public override void MouseUp (NSEvent theEvent)
		{
			autoScrollTimer.Enabled = false;

			if (terminal.MouseMode.SendButtonRelease()) {
				SharedMouseEvent (theEvent, down: false);
				base.MouseUp (theEvent);
				return;
			}

			CalculateMouseHit (theEvent, true, out var col, out var row);

			switch (theEvent.ClickCount) {
			case 1:
				if (!selection.Active) {
					if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ShiftKeyMask)) {
						selection.ShiftExtend (row, col);
					} else {
						selection.SetSoftStart (row, col);
					}
				} else {
					if (!didSelectionDrag) {
						if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ShiftKeyMask)) {
							selection.ShiftExtend (row, col);
						} else if (!theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ControlKeyMask)) {
							selection.Active = false;
							selection.SetSoftStart (row, col);
						}
					}
				}
				break;
			case 2:
				selection.SelectWordOrExpression (col, row);
				break;
			case 3:
				selection.SelectRow (row);
				break;
			}

			didSelectionDrag = false;

			if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.ControlKeyMask)) {
				OnShowContextMenu (theEvent);
			}

			base.MouseUp (theEvent);
		}

		public override void RightMouseUp (NSEvent theEvent)
		{
			OnShowContextMenu (theEvent);
		}

		protected virtual void OnShowContextMenu(NSEvent theEvent)
		{
		}

		public override void MouseDragged (NSEvent theEvent)
		{
			CalculateMouseHit (theEvent, true, out var col, out var row);

			if (terminal.MouseMode.SendMotionEvent()) {
				ComputeMouseEvent (theEvent, true, out var buttonFlags);
				terminal.SendMouseMotion (buttonFlags, col, row);
				base.MouseDragged (theEvent);
				return;
			}

			if (terminal.MouseMode != MouseMode.Off) {
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

			base.MouseDragged (theEvent);
		}

		public override void MouseMoved (NSEvent theEvent)
		{
			if (terminal.MouseMode.SendMotionEvent ()) {
				CalculateMouseHit (theEvent, true, out var col, out var row);
				ComputeMouseEvent (theEvent, true, out var buttonFlags);
				terminal.SendMouseMotion (buttonFlags, col, row);
				return;
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
					ScrollLines (velocity * -1);
				} else {
					ScrollLines (velocity);
				}
			}
		}

		int autoScrollDelta = 0;

		private void AutoScrollTimer_Elapsed (object sender, ElapsedEventArgs e)
		{
			if (autoScrollDelta == 0)
				return;

			this.BeginInvokeOnMainThread (() => {
				ScrollLines (autoScrollDelta);
			});
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

		/// <summary>
		/// Ensures that the mouse cursor shows correctly when hovering the view
		/// </summary>
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

			accessibility.Invalidate ();
			NSAccessibility.PostNotification (this, NSAccessibilityNotifications.SelectedTextChangedNotification);
		}

		void SelectSearchResult(SearchSnapshot.SearchResult searchResult)
		{
			selection.SelectNone ();
			if (searchResult != null) {
				selection.SetSoftStart (searchResult.Start.Y - terminal.Buffer.YDisp, searchResult.Start.X);
				selection.ShiftExtend (searchResult.End.Y - terminal.Buffer.YDisp, searchResult.End.X);

				// scroll to ensure range start is visible, try to get the start somewhere in the middle of the view
				if ((searchResult.Start.Y < terminal.Buffer.YDisp) || (searchResult.Start.Y >= terminal.Buffer.YDisp + terminal.Rows)) {
					var newYDisp = Math.Max (searchResult.Start.Y - (terminal.Rows / 2), 0);
					ScrollToYDisp (newYDisp, false);
				}
			}
		}

#region Font handling
		/// <summary>
		/// Sets up the fonts and computes cell dimensions and re-adjusts the terminals rows and columns to suit
		/// </summary>
		public void SetFonts (TerminalFonts fonts = null)
		{
			SetFonts (fonts, true);
		}

		/// <summary>
		/// Sets up the fonts and computes cell dimensions and optionally triggers a display update
		/// </summary>
		void SetFonts (TerminalFonts fonts, bool needsUpdateDisplay)
		{
			var newFonts = fonts ?? GetDefaultFonts ();
			this.fonts = newFonts;
			cellDimensions = new CellDimension (newFonts);
			attributes.Clear ();

			if (needsUpdateDisplay) {
				// TODO: clear the selection for now, but we need to handle the remapping of buffer coords when lines are wrapped / unwrapped due to size changes
				selection.Active = false;

				// update the selection and caret view dimensions
				selectionView.CellDimensions = cellDimensions;
				caret.CellDimensions = cellDimensions;

				ResizeTerminal (Frame);

				Terminal.Refresh (0, Terminal.Rows);
				UpdateDisplay ();
			}
		}

		/// <summary>
		/// Gets the default set of fonts for the terminal view
		/// </summary>
		static TerminalFonts GetDefaultFonts(int fontSize = 14)
		{
			var fontNormal = NSFont.FromFontName ("Menlo Regular", fontSize) ?? NSFont.MonospacedSystemFont (fontSize, NSFontWeight.Regular);
			var fontBold = NSFont.FromFontName ("Menlo Bold", fontSize) ?? NSFont.MonospacedSystemFont (fontSize, NSFontWeight.Bold);
			var fontItalic = NSFont.FromFontName ("Menlo Italic", fontSize) ?? NSFont.MonospacedSystemFont (fontSize, NSFontWeight.Regular);
			var fontBoldItalic = NSFont.FromFontName ("Menlo Bold Italic", fontSize) ?? NSFont.MonospacedSystemFont (fontSize, NSFontWeight.Bold);

			return new TerminalFonts (fontNormal, fontBold, fontItalic, fontBoldItalic);
		}
#endregion

#region Terminal dimension logic

		/// <summary>
		/// Calculates the visible number of rows and columns given the frame size and font
		/// </summary>
		static (int cols, int rows) CalculateVisibleRowsAndColumns (CellDimension dimensions, CGRect frame)
		{
			var cols = (int)(frame.Width / dimensions.Width);
			var rows = (int)(frame.Height / dimensions.Height);

			return (cols, rows);
		}

		/// <summary>
		/// Resizes the terminal given a new frame and adjusts the number of rols and cols
		/// allowing for the scroller
		/// </summary>
		void ResizeTerminal(CGRect frame)
		{
			var frames = CalculateLayouts (frame);
			var dimensions = CalculateVisibleRowsAndColumns (cellDimensions, frames.contentFrame);

			ResizeTerminalColsAndRows (dimensions.cols, dimensions.rows);

			// make the selection view the entire visible portion of the view
			// we will mask the selected text that is visible to the user
			selectionView.Frame = frames.contentFrame;
			caret.Frame = frames.contentFrame;

			UpdateCursorPosition ();

			accessibility.Invalidate ();
			search.Invalidate ();

			OnSizeChanged (dimensions.cols, dimensions.rows);
		}

		void ResizeTerminalColsAndRows(int cols, int rows)
		{
			if (cols != terminal.Cols || rows != terminal.Rows) {
				terminal.Resize (cols, rows);
				FullBufferUpdate ();
			}
		}

#endregion

#region Layout / scrolling logic

		public override void Layout ()
		{
			var frames = CalculateLayouts (Frame);

			scroller.Frame = frames.scrollerFrame;

			caret.Frame = frames.contentFrame;
			selectionView.Frame = frames.contentFrame;
		}

		/// <summary>
		/// Calculates the layout of the views given a frame
		/// </summary>
		/// <remarks>
		/// `contentFrame` is the region of the view within which to render terminal output
		/// </remarks>
		(CGRect scrollerFrame, CGRect contentFrame) CalculateLayouts (CGRect rect)
		{
			var scrollWidth = NSScroller.ScrollerWidthForControlSize (NSControlSize.Regular);
			var scrollFrame = new CGRect (rect.Width - scrollWidth, 0, scrollWidth, rect.Height);

			var terminalFrame = new CGRect (contentPadding, contentPadding, rect.Width - scrollWidth - (2 * contentPadding), rect.Height - (2 * contentPadding));

			return (scrollFrame, terminalFrame);
		}

		/// <summary>
		/// Handles user interaction with the scroller
		/// </summary>
		void ScrollerActivated (object sender, EventArgs e)
		{
			switch (scroller.HitPart) {
			case NSScrollerPart.DecrementPage:
				PageUp ();
				scroller.DoubleValue = ScrollPosition;
				break;
			case NSScrollerPart.IncrementPage:
				PageDown ();
				scroller.DoubleValue = ScrollPosition;
				break;
			case NSScrollerPart.Knob:
				ScrollToPosition (scroller.DoubleValue);
				break;
			}
		}

		void OnTerminalScrolled (double scrollPosition)
		{
			UpdateScroller ();
		}

		void OnCanScrollChanged (bool obj)
		{
			UpdateScroller ();
		}

		void UpdateScroller ()
		{
			var shouldBeEnabled = !Terminal.Buffers.IsAlternateBuffer;
			shouldBeEnabled = shouldBeEnabled && Terminal.Buffer.HasScrollback;
			shouldBeEnabled = shouldBeEnabled && Terminal.Buffer.Lines.Length > Terminal.Rows;
			scroller.Enabled = shouldBeEnabled;

			scroller.DoubleValue = ScrollPosition;
			scroller.KnobProportion = ScrollThumbsize;
		}

		/// <summary>
		/// Scrolls the terminal contents so that the given row is at the top of the view
		/// </summary>
		void ScrollToYDisp (int ydisp, bool notifyAccessibility = true)
		{
			int linesToScroll = ydisp - Terminal.Buffer.YDisp;
			Terminal.ScrollLines (linesToScroll, !notifyAccessibility);
			if (!notifyAccessibility) {
				UpdateDisplay (notifyAccessibility);

				selectionView.NotifyScrolled ();
				OnTerminalScrolled (ScrollPosition);
			}
		}

		/// <summary>
		/// Scrolls the terminal contents to the relative position in the buffer
		/// </summary>
		void ScrollToPosition (double position)
		{
			var maxScrollback = Terminal.Buffer.Lines.Length - Terminal.Rows;
			int newScrollPosition = (int)(maxScrollback * position);
			if (newScrollPosition < 0)
				newScrollPosition = 0;
			if (newScrollPosition > maxScrollback)
				newScrollPosition = maxScrollback;

			ScrollToYDisp (newScrollPosition);
		}

		/// <summary>
		/// Handles notfications that the terminal adjusted its YDisp and scrolled contents
		/// </summary>
		/// <param name="terminal">The terminal that scrolled</param>
		/// <param name="yDisp">The new yDisp of the terminal</param>
		void HandleTerminalScrolled (Terminal terminal, int yDisp)
		{
			selectionView.NotifyScrolled ();
			OnTerminalScrolled (ScrollPosition);

			QueuePendingDisplay ();
			//UpdateDisplay ();
		}

#endregion

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				terminal.Scrolled -= HandleTerminalScrolled;
				terminal.Buffers.Activated -= HandleBuffersActivated;

				selection.SelectionChanged -= HandleSelectionChanged;

				autoScrollTimer.Elapsed -= AutoScrollTimer_Elapsed;
				scroller.Activated -= ScrollerActivated;
			}

			base.Dispose (disposing);
		}

		void OnSizeChanged (int cols, int rows)
		{
			SizeChanged?.Invoke (cols, rows, Frame.Width, Frame.Height);
			UpdateScroller ();
		}

		void HandleBuffersActivated (Buffer active, Buffer inactive)
		{
			OnCanScrollChanged (CanScroll);
		}

		/// <summary>
		/// Ensures that the caret is visible
		/// </summary>
		void EnsureCaretIsVisible ()
		{
			ScrollToYDisp (Terminal.Buffer.YBase);
		}
	}
}
