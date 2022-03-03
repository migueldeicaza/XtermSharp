//
// This sample shows how to embed a TerminalView in a ViewController
// and how to wire it up to a bash shell on MacOS.
//
using System;
using AppKit;
using Foundation;
using ObjCRuntime;
using XtermSharp.Mac;

namespace MacTerminal {
	public partial class ViewController : NSViewController {
		ProcessTerminalView terminalControl;
		LocalProcess process;

		public ViewController (NativeHandle handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			process = new LocalProcess ();
			process.OnExited += () => {
				View.Window.Close ();
			};

			terminalControl = new ProcessTerminalView (View.Bounds);
			terminalControl.Process = process;

			terminalControl.TitleChanged += TerminalControl_TitleChanged;
			View.AddSubview (terminalControl);

			process.Start ();
		}

		private void TerminalControl_TitleChanged (string obj)
		{
			View.Window.Title = obj ?? string.Empty;
		}

		public override void ViewDidLayout ()
		{
			base.ViewDidLayout ();
			terminalControl.Frame = View.Bounds;
			terminalControl.NeedsLayout = true;
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
