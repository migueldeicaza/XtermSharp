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

		void GetSize (Terminal terminal, ref MacWinSize size)
		{
			 size = new MacWinSize () { 
				col = (short)terminal.Cols, 
				row = (short)terminal.Rows, 
			 	xpixel = (short)View.Frame.Width, 
		 		ypixel = (short)View.Frame.Height 
 			};
		}

		public string [] GetEnvironmentVariables ()
		{
			var l = new List<string> ();
			l.Add ("TERM=xterm-color");

						// Without this, tools like "vi" produce sequences that are not UTF-8 friendly
			l.Add ("LANG=en_US.UTF-8");
			var env = Environment.GetEnvironmentVariables ();
			foreach (var x in new [] { "LOGNAME", "USER", "DISPLAY", "LC_TYPE", "USER", "HOME", "PATH" })
				if (env.Contains (x))
					l.Add ($"{x}={env [x]}");
			return l.ToArray ();
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			terminalView = new TerminalView (View.Frame);
			var t = terminalView.Terminal;
			var size = new MacWinSize ();
			GetSize (t, ref size);

			pid = Pty.Fork ("/bin/bash", new string [] { "/bin/bash" }, GetEnvironmentVariables (), out fd, size);
			DispatchIO.Read (fd, (nuint) readBuffer.Length, DispatchQueue.CurrentQueue, ChildProcessRead);

			
			terminalView.UserInput += (byte [] data) => {
				DispatchIO.Write (fd, DispatchData.FromByteBuffer (data), DispatchQueue.CurrentQueue, ChildProcessWrite);
			};
			terminalView.Feed ("Welcome to XtermSharp - NSView frontend!\n");
			terminalView.TitleChanged += (TerminalView sender, string title) => {
				View.Window.Title = title;
			};
			terminalView.SizeChanged += (newCols, newRows) => {
				MacWinSize nz = new MacWinSize ();
				GetSize (t, ref nz);
				var res = Pty.SetWinSize (fd, ref nz);
				Console.WriteLine (res);
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
