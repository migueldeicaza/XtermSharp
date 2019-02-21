using System;
using System.Collections.Generic;

namespace XtermSharp {

	public class Terminal {
		BufferSet buffers;

		public Terminal ()
		{
		}

		public void Handler (string txt)
		{
		}

		public void Error (string txt, params object [] args)
		{
		}

		public bool Debug { get; set; }
		public void Log (string text, params object [] args)
		{
		}

		public Dictionary<byte, string> Charset { get; set; }

		public Buffer Buffer => buffers.Active;

		public TerminalOptions Options;
		public int Cols, Rows;
		public bool Wraparound;
		public bool InsertMode;
		public int CurAttr;

		internal void UpdateRange (int y)
		{
			throw new NotImplementedException ();
		}

		internal void EmitChar (char ch)
		{
			// For accessibility purposes 'a11y.char' in the original source.
		}

		internal void Reset ()
		{
			throw new NotImplementedException ();
		}

		internal void Index ()
		{
			throw new NotImplementedException ();
		}

		internal void Scroll (bool isWrapped = false)
		{
			throw new NotImplementedException ();
		}

		internal void Bell (byte code)
		{
			throw new NotImplementedException ();
		}

		public void EmitLineFeed ()
		{
		}

		internal void EmitA11yTab (object p)
		{
			throw new NotImplementedException ();
		}

		internal void SetgLevel (int v)
		{
			throw new NotImplementedException ();
		}

		internal int EraseAttr ()
		{
			throw new NotImplementedException ();
		}
	}
}
