using System;
using System.Diagnostics;

namespace XtermSharp.Mac {
	/// <summary>
	/// Handles macOS accessibilty for the terminal view
	/// </summary>
	public class AccessibilityService {
		readonly Terminal terminal;
		readonly SelectionService selection;
		AccessibilitySnapshot cache;

		public AccessibilityService (Terminal terminal)
		{
			this.terminal = terminal;
			this.selection = new SelectionService (terminal);

			// TODO: handle terminal.Buffer.scrolled and handle lines being scrolled off the top of the buffer
			// TODO: hook up events in terminal that lets us know when the buffer content has changed
		}

		public AccessibilitySnapshot GetSnapshot ()
		{
			if (cache == null) {
				var result = CalculateSnapshot ();
				cache = result;
			}

			return cache;
		}

		public void Invalidate ()
		{
			cache = null;
		}

		private AccessibilitySnapshot CalculateSnapshot ()
		{
			selection.SelectAll ();

			// TODO: calc visible char range

			var text = selection.GetSelectedText ();

			// add a space at the end for the space between the prompt and the caret
			if (terminal.Buffers.IsAlternateBuffer) {
				text += " ";
			}

			int caret = CalculateCaretPosition(text);

			AccessibilitySnapshot.Range visble = new AccessibilitySnapshot.Range { Start = 0, Length = text.Length };

			var result = new AccessibilitySnapshot (text, visble, caret);

			return result;
		}

		int CalculateCaretPosition(string text)
		{
			if (terminal.Buffers.IsAlternateBuffer) {
				// TODO: alternate buffer support
				return text.Length;
			}

			// when the normal buffer is active we always have the caret on the last row of text, somewhere along
			// the line. Buffer.X is that position on the last row. We need to find the beginning of the last line
			// of text
			int lineStart = 0;
			for (int i = text.Length - 1; i > 0; i--) {
				if (text[i] == '\n') {
					lineStart = i + 1;
					break;
				}
			}

			return lineStart + terminal.Buffer.X;
		}
	}

	public class AccessibilitySnapshot {
		public AccessibilitySnapshot (string text, Range visible, int caret)
		{
			Text = text;
			VisibleRange = visible;
			CaretPosition = caret;
		}

		public string Text { get; }

		public Range VisibleRange { get; }

		public int CaretPosition { get; }

		[DebuggerDisplay("{Start}, {Length}")]
		public struct Range {
			public int Start;
			public int Length;
		}
	}
}
