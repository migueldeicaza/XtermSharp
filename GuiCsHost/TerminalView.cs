using System;
using Terminal.Gui;
using XtermSharp;

namespace GuiCsHost {
	public class TerminalView : View {
		public TerminalView ()
		{

		}

		public override bool ProcessKey (KeyEvent keyEvent)
		{
			return base.ProcessKey (keyEvent);
		}

		public override void Redraw (Rect region)
		{
			base.Redraw (region);
		}


	}
}
