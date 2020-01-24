//
// This sample shows how to embed a TerminalView in a ViewController
// and how to wire it up to a bash shell on MacOS.
//
using System;
using AppKit;
using Foundation;
using XtermSharp.Mac;

namespace MacTerminal {
	public partial class ViewController : NSViewController {
		TerminalControl terminalControl;

		public ViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			terminalControl = new TerminalControl (View.Frame);
			View.AddSubview (terminalControl);

			terminalControl.StartShell ();
		}

		public override void ViewDidLayout ()
		{
			base.ViewDidLayout ();
			terminalControl.Frame = View.Frame;
		}

		public override NSObject RepresentedObject {
			get {
				return base.RepresentedObject;
			}
			set {
				base.RepresentedObject = value;
				// Update the view, if already loaded.
			}
		}
	}
}
