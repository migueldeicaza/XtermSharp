using System;

namespace XtermSharp {
	public struct CharData {
		public int Attribute;
		public Rune Rune;
		public int Width;
		public int Code;

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
