using System;
using AppKit;

namespace XtermSharp.Mac {
	/// <summary>
	/// The set of fonts to use when displaying content in the view
	/// </summary>
	public sealed class TerminalFonts {
		public TerminalFonts (NSFont normal, NSFont bold, NSFont italic, NSFont boldItalic)
		{
			if (normal == null)
				throw new ArgumentNullException (nameof (normal));
			if (bold == null)
				throw new ArgumentNullException (nameof (bold));
			if (italic == null)
				throw new ArgumentNullException (nameof (italic));
			if (boldItalic == null)
				throw new ArgumentNullException (nameof (boldItalic));

			Normal = normal;
			Bold = bold;
			Italic = italic;
			BoldItalic = boldItalic;
		}

		public NSFont Normal { get; }

		public NSFont Bold { get; }

		public NSFont Italic { get; }

		public NSFont BoldItalic { get; }
	}
}
