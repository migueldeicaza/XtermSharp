using System;
using AppKit;
using Foundation;
using XtermSharp.Mac;
using XtermSharp;
using ObjCRuntime;
using CoreFoundation;
using System.Runtime.InteropServices;

namespace MacTerminal {
	public partial class ViewController : NSViewController {
		TerminalView terminalView;

		public ViewController (IntPtr handle) : base (handle)
		{
		}

		int pid, fd;
		byte [] readBuffer = new byte [4*1024];

		void ChildProcessRead (DispatchData data, int error)
		{
			using (var map = data.CreateMap (out var buffer, out var size)) {
				// Faster, but harder to debug:
				// terminalView.Feed (buffer, (int) size);
				byte [] copy = new byte [(int)size];
				Marshal.Copy (buffer, copy, 0, (int)size);
				//System.IO.File.WriteAllBytes ("/tmp/log2", copy);
				terminalView.Feed (copy);
			}
			DispatchIO.Read (fd, (nuint)readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);
		}

		void ChildProcessWrite (DispatchData left, int error)
		{
			if (error != 0) {
				throw new Exception ("Error writing data to child");
			}
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			pid = Pty.Fork ("/bin/bash", new string [] { "/bin/bash" }, new string [] { "TERM=xterm-color" }, out fd);
			DispatchIO.Read (fd, (nuint) readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);

			terminalView = new TerminalView (View.Frame);
			terminalView.UserInput += (byte [] data) => {
				DispatchIO.Write (fd, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
			};
			terminalView.Feed ("Welcome to XtermSharp - NSView frontend!\n");
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
