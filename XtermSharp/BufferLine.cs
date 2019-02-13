using System;
namespace XtermSharp {
	public class BufferLine {
		CharData [] data;
		public int Length => data.Length;

		public BufferLine (int cols, CharData? fillCharData, bool isWrapped = false)
		{
			var fill = fillCharData ?? CharData.Null;

			data = new CharData [cols];
			for (int i = 0; i < cols; i++)
				data [i] = fill;	
		}
	}
}
