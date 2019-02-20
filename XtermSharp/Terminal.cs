using System;
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

		public Buffer Buffer => buffers.Active;
	}
}
