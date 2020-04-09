using System;
using System.Collections.Generic;
using XtermSharp;

namespace XtermSharp.Tests {
	public abstract class BaseTerminalTest : IDisposable {
		TerminalDelegate terminalDelegate;

		public BaseTerminalTest ()
		{
			this.terminalDelegate = new TerminalDelegate ();
			this.Terminal = new Terminal (this.terminalDelegate);
			this.Commander = new TerminalCommands (this.Terminal);
		}

		public Terminal Terminal { get; private set; }

		public TerminalCommands Commander { get; private set; }

		public void Dispose ()
		{
			this.terminalDelegate = null;
			this.Terminal = null;
			this.Commander = null;
		}

		class TerminalDelegate : SimpleTerminalDelegate, IResponseReader {
			readonly List<byte> responseBuffer = new List<byte> ();

			public override void Send (byte [] data)
			{
				responseBuffer.AddRange (data);
			}

			public byte ReadNext ()
			{
				if (responseBuffer.Count > 0) {
					var result = responseBuffer [0];
					responseBuffer.RemoveAt (0);
					return result;
				}

				return 0;
			}
		}
	}
}
