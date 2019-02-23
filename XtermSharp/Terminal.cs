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
		public BufferSet Buffers => buffers;

		public bool ApplicationCursor { get; internal set; }
		public int SavedCols { get; internal set; }
		public bool ApplicationKeypad { get; internal set; }
		public object X10Mouse { get; internal set; }
		public bool SendFocus { get; internal set; }
		public bool UtfMouse { get; internal set; }
		public bool OriginMode { get; internal set; }
		public bool Vt200Mouse { get; internal set; }
		public bool NormalMouse { get; internal set; }
		public bool MouseEvents { get; internal set; }
		public bool SgrMouse { get; internal set; }
		public bool UrxvtMouse { get; internal set; }
		public bool CursorHidden { get; internal set; }
		public bool BracketedPasteMode { get; internal set; }

		public TerminalOptions Options;
		public int Cols, Rows;
		public bool Wraparound;
		public bool InsertMode;
		public int CurAttr;
		int gLevel;

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
			gLevel = v;
			if (CharSets.All.TryGetValue ((byte)v, out var cs))
				Charset = cs;
			else
				Charset = null;
		}

		internal int EraseAttr ()
		{
			throw new NotImplementedException ();
		}

		internal void EmitScroll (int v)
		{
			throw new NotImplementedException ();
		}

		internal void SetgCharset (int v, Dictionary<byte, string> @default)
		{
			throw new NotImplementedException ();
		}

		internal void Resize (int v, int rows)
		{
			throw new NotImplementedException ();
		}

		internal void SyncScrollArea ()
		{
			// This should call the viewport syncscrollarea
			throw new NotImplementedException ();
		}

		internal void EnableMouseEvents ()
		{
			// TODO:
	    	// DISABLE SELECTION MANAGER.
			throw new NotImplementedException ();
		}

		internal void DisableMouseEvents ()
		{
			// TODO:
	    	// ENABLE SELECTION MANAGER.
			throw new NotImplementedException ();
		}

		internal void Refresh (int v1, int v2)
		{
			throw new NotImplementedException ();
		}

		internal void ShowCursor ()
		{
			throw new NotImplementedException ();
		}

		static Dictionary<int,int> matchColorCache = new Dictionary<int, int> ();

		public int MatchColor (int r1, int g1, int b1)
		{
			throw new NotImplementedException ();
		}

		internal void EmitData (string txt)
		{
			throw new NotImplementedException ();
		}

		internal void SetCursorStyle (CursorStyle style)
		{
			throw new NotImplementedException ();
		}

		internal void SetTitle (string text)
		{
			throw new NotImplementedException ();
		}

		internal void TabSet ()
		{
			throw new NotImplementedException ();
		}

		internal void ReverseIndex ()
		{
			throw new NotImplementedException ();
		}
	}
}
