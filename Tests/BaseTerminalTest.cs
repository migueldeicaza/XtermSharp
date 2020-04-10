using System;
using System.Collections.Generic;
using System.Globalization;
using XtermSharp;
using XtermSharp.CommandExtensions;
using XtermSharp.Tests.EscTests;
using Xunit;

namespace XtermSharp.Tests {
	public abstract class BaseTerminalTest : IDisposable {
		TerminalDelegate terminalDelegate;
		int gNextId;

		public BaseTerminalTest ()
		{
			this.terminalDelegate = new TerminalDelegate ();
			this.Terminal = new Terminal (this.terminalDelegate);
		}

		public Terminal Terminal { get; private set; }

		public void Dispose ()
		{
			this.terminalDelegate = null;
			this.Terminal = null;
		}

		public int[] ReadCsiResponse(string final)
		{
			return ((IResponseReader)this.Terminal.Delegate).ReadCSI (final);
		}

		public void AssertScreenCharsInRectEqual ((int left, int top, int right, int bottom) rect, string text)
		{
			var pid = ++gNextId;

			this.Terminal.csiDECRQCRA (pid, 0, rect.top, rect.left, rect.bottom, rect.right);
			var response = ((IResponseReader)this.Terminal.Delegate).ReadDCS ();

			string pidStr = pid.ToString ();

			Assert.StartsWith (pidStr, response);

			response = response.Substring (pidStr.Length);
			Assert.StartsWith ("!~", response);

			response = response.Substring (2);

			if (int.TryParse (response, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int checksum)) {
				int expectedChecksum = 0;
				foreach (var ch in text) {
					expectedChecksum += (int)ch;
				}

				Assert.Equal (expectedChecksum, checksum);
			} else {
				Assert.True (false, "did not receive a valid checksum");
			}
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
