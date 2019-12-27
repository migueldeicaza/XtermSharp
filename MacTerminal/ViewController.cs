//
// This sample shows how to embed a TerminalView in a ViewController
// and how to wire it up to a bash shell on MacOS.
//
using System;
using AppKit;
using Foundation;
using XtermSharp.Mac;
using XtermSharp;
using ObjCRuntime;
using CoreFoundation;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace MacTerminal {
	public partial class ViewController : NSViewController {
		TerminalView terminalView;

		public ViewController (IntPtr handle) : base (handle)
		{
		}

		int pid, fd;
		byte [] readBuffer = new byte [4*1024];

		static int x;
		void ChildProcessRead (DispatchData data, int error)
		{
			using (var map = data.CreateMap (out var buffer, out var size)) {
				// Faster, but harder to debug:
				// terminalView.Feed (buffer, (int) size);
				//Console.WriteLine ("Read {0} bytes", size);
				if (size == 0) {
					View.Window.Close ();
					return;
				}
				byte [] copy = new byte [(int)size];
				Marshal.Copy (buffer, copy, 0, (int)size);

				System.IO.File.WriteAllBytes ("/tmp/log-" + (x++), copy);
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

		void GetSize (Terminal terminal, ref UnixWindowSize size)
		{
			 size = new UnixWindowSize () { 
				col = (short)terminal.Cols, 
				row = (short)terminal.Rows, 
			 	xpixel = (short)View.Frame.Width, 
		 		ypixel = (short)View.Frame.Height 
 			};
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			terminalView = new TerminalView (View.Frame);
			var t = terminalView.Terminal;
			var size = new UnixWindowSize ();
			GetSize (t, ref size);

			var scrollView = new NSScrollView (View.Frame);
			scrollView.HasVerticalScroller = true;
			scrollView.HasHorizontalScroller = true;
			scrollView.BorderType = NSBorderType.BezelBorder;
			scrollView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
			scrollView.DocumentView = terminalView;

			pid = Pty.ForkAndExec ("/bin/bash", new string [] { "/bin/bash" }, Terminal.GetEnvironmentVariables (), out fd, size);
			DispatchIO.Read (fd, (nuint) readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);

			
			terminalView.UserInput += (byte [] data) => {
				DispatchIO.Write (fd, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
			};
			terminalView.Feed ("Welcome to XtermSharp - NSView frontend!\n");
			terminalView.TitleChanged += (TerminalView sender, string title) => {
				View.Window.Title = title;
			};
			terminalView.SizeChanged += (newCols, newRows) => {
				UnixWindowSize nz = new UnixWindowSize ();
				GetSize (t, ref nz);
				var res = Pty.SetWinSize (fd, ref nz);
			};
			View.AddSubview (scrollView);

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
