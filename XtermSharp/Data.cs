using System;

namespace XtermSharp {
	public struct 		CharData {
		public int Attribute;
		public Rune Rune;
		public int Width;
		public int Code;

		const int DefaultAttr = Renderer.DefaultColor << 9 | (256 << 0);

		public static CharData Null = new CharData (DefaultAttr, '\u0200', 1, 0);
		public static CharData WhiteSpace = new CharData (DefaultAttr, ' ', 1, 32);

		public CharData (int attribute, Rune rune, int width, int code)
		{
			Attribute = attribute;
			Rune = rune;
			Width = width;
			Code = code;
		}

		public override string ToString ()
		{
			return $"[CharData (Attr={Attribute},Rune={Rune},W={Width},Code={Code}]";
		}
	}


}
