using System;

using AppKit;
using Foundation;
using XtermSharp.Mac;

namespace MacTerminal {
	public partial class ViewController : NSViewController {
		TerminalView terminalView;

		public ViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			terminalView = new TerminalView (View.Frame);
			terminalView.Feed ("Hello world from the terminal!");
			terminalView.TitleChanged += (TerminalView sender, string title) => {
				View.Window.Title = title;
			};
			View.AddSubview (terminalView);

		}

		public override void ViewDidLayout ()
		{
			base.ViewDidLayout ();
			terminalView.Frame = View.Frame;
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
