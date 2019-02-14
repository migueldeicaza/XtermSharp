using System;
using System.Collections.Generic;

namespace XtermSharp {
	public class BufferLine {
		Dictionary<int, string> combined;
		CharData [] data;
		public int Length => data.Length;

		public BufferLine (int cols, CharData? fillCharData, bool isWrapped = false)
		{
			var fill = fillCharData ?? CharData.Null;

			data = new CharData [cols];
			for (int i = 0; i < cols; i++)
				data [i] = fill;	
		}

		public CharData this [int idx] {
			get => data [idx];
			set {
				data [idx] = value;
			}
		}

		public int GetWidth (int index)	=> data [index].Width;

		public void InsertCells (int pos, int n, CharData fillCharData)
		{
			var len = Length;
			pos = pos % len;
			if (n < len - pos) {
				for (var i = len - pos - n - 1; i >= 0; --i)
					this [pos + n + i] = this [pos + i];
				for (var i = 0; i < n; i++)
					this [pos + i] = fillCharData;
			} else {
				for (var i = pos; i < len; ++i)
					this [i] = fillCharData;
			}
		}

		public void DeleteCells (int pos, int n, CharData fillCharData)
		{
			var len = Length;
			pos %= len;
			if (n < len - pos) {
				for (var i = 0; i < len - pos - n; ++i)
					this [pos + i] = this [pos + n + i];
				for (var i = len - n; i < len; ++i)
					this [i] = fillCharData;
			} else {
				for (var i = pos; i < len; ++i)
					this [i] = fillCharData;
			}
		}
	}
}
