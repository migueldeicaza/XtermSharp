using System;
using System.Collections.Generic;
using XtermSharp;
using Xunit;

namespace XtermSharp.Tests.EscTests {
	static class Commands {
		public static int [] ReadCSI (this IResponseReader reader, string final, string prefix = null)
		{
			var code = reader.ReadNext ();
			switch (code) {
			case 27:
				reader.ReadOrDie ((byte)'[');
				break;
			case 0x9b:
				break;
			default:
				throw new Exception ($"Expected CSI, found {code}");
			}

			var pars = new List<int> ();
			var currentParam = "";

			var ch = reader.ReadNextChar ();
			if (!Char.IsDigit (ch) && ch != ';') {
				if (prefix != null && ch == prefix [0]) {
					ch = reader.ReadNextChar ();
				} else {
					throw new Exception ($"unexpected character `{ch}`");
				}
			}

			while (true) {
				if (ch == ';') {
					pars.Add (int.Parse (currentParam));
					currentParam = "";
				} else if (ch >= '0' && ch <= '9') {
					currentParam += ch;
				} else {
					// Read all the final characters, asserting they match.
					while (true) {
						Assert.Equal (ch, final [0]);
						final = final.Substring (1);
						if (final.Length > 0) {
							ch = reader.ReadNextChar ();
						} else {
							break;
						}
					}

					if (currentParam == "") {
						pars.Add (0);
					} else {
						pars.Add (int.Parse (currentParam));
					}

					break;
				}

				ch = reader.ReadNextChar ();
			}

			return pars.ToArray ();
		}

		public static string ReadDCS (this IResponseReader reader)
		{
			var cc = new ControlCodes ();

			var code = reader.ReadNext ();
			switch (code) {
			case 27:
				reader.ReadOrDie ((byte)'P');
				break;
			default:
				throw new Exception ($"Expected DCS, found {code}");
			}


			var result = "";
			while (!result.EndsWith (cc.ST) && !result.EndsWith ((char)(0x9c))) {
				var c = reader.ReadNextChar ();
				result += c;
			}

			if (result.EndsWith (cc.ST)) {
				return result.Substring (0, result.Length - 2);
			} else {
				return result.Substring (0, result.Length - 1);
			}
		}

		static char ReadNextChar (this IResponseReader reader)
		{
			return (char)reader.ReadNext ();
		}

		static void ReadOrDie (this IResponseReader reader, byte expected)
		{
			var ch = reader.ReadNext ();
			if (ch != expected) {
				throw new Exception ($"expected {expected} but read {ch}");
			}
		}
	}
}
