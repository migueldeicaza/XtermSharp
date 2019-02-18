using System;
namespace Application {

	interface IDcsHandler {
		void Hook (string collect, int [] parameters, int flag);
		void Put (int [] data, int start, int end);
		void Unhook ();
	}

	// Dummy DCS Handler as defaulta fallback
	public class DscDummy : IDcsHandler {
		public void Hook (string collect, int [] parameters, int flag) { }
		public void Put (int [] data, int start, int end) { }
		public void Unhook () { }
	}

	public enum ParserState {
		Ground,
		Escape,
		EscapeIntermediate,
		CsiEntry,
		CsiParam,
		CsiIntermediate,
		CsiIgnore,
		SosPmApcString,
		OscString,
		DcsEntry,
		DcsParam,
		DcsIgnore,
		DcsIntermediate,
		DcsPassthrough
	}

	public class ParsingState {
		/// <summary>
		/// Position in Parse String
		/// </summary>
		public int Position;
		/// <summary>
		/// Actual character code
		/// </summary>
		public int Code;
		/// <summary>
		/// Current Parser State
		/// </summary>
		public ParserState CurrentState;
		/// <summary>
		/// Print buffer start index (-1 for not set)
		/// </summary>
		int Print;
		/// <summary>
		///  Buffer start index (-1 for not set)
		/// </summary>
		int Dcs;
		/// <summary>
		/// Osc string buffer
		/// </summary>
		int Osc;
		/// <summary>
		/// Collect buffer with intermediate characters
		/// </summary>
		string Collect;
		/// <summary>
		/// Parameters buffer
		/// </summary>
		int [] Parameters;
		// should abort (default: false)
		bool Abort;

	}

	//
	// EscapeSequenceParser.
	// This class implements the ANSI/DEC compatible parser described by
	// Paul Williams (https://vt100.net/emu/dec_ansi_parser).
	// To implement custom ANSI compliant escape sequences it is not needed to
	// alter this parser, instead consider registering a custom handler.
	// For non ANSI compliant sequences change the transition table with
	// the optional `transitions` contructor argument and
	// reimplement the `parse` method.
	// NOTE: The parameter element notation is currently not supported.
	// TODO: implement error recovery hook via error handler return values
	// 
	public class EscapeSequenceParser {


		enum ParserAction {
			Ignore,
			Error,
			Print,
			Execute,
			OscStart,
			OscPut,
			OscEnd,
			CsiDispatch,
			Param,
			Collect,
			EscDispatch,
			Clear,
			DcsHook,
			DcsPut,
			DcsUnhook
		}

		class TransitionTable {
			// data is packed like this:
			// currentState << 8 | characterCode  -->  action << 4 | nextState
			byte [] table;

			public TransitionTable (int length)
			{
				table = new byte [length];
			}

			public void Add (int code, ParserState state, ParserAction action, ParserState next = -1)
			{
				table [state << 8 | code] = action << 4 | (next == -1) ? state : next);
			}

			public void Add (int [] codes, ParserState state, ParserAcction action, ParserState next = -1)
			{
				foreach (var c in codes)
					Add (c, state, action, next);
			}
		}
		const int [] PRINTABLES = r (0x20, 0x7f);
		const int [] EXECUTABLES = r (0x00, 0x19).Concat (r (0x1c, 0x20)).ToArray ();

		static int [] r (int low, int high)
		{
			var c = high - low;
			var arr = new int [c];
			while (c-- > 0)
				arr [c] = --high;
			return arr;
		}

		static TransitionTable BuildVt500TransitionTable ()
		{
			const int NonAsciiPrintable = 0xa0;
			var table = new TransitionTable (4095);
			var states = r (ParserState.Ground, ParserState.DcsPassthrough + 1);

			// table with default transition
		    	for (var state in states) {
				for (var code = 0; code <= NonAsciiPrintable; ++code)
					table.Add (code, state, ParserAction.Error, ParserState.Ground);
			}
			// printables
			table.Add (PRINTABLES, ParserState.Ground, ParserAction.Print, ParserState.Ground);

			// global anwyhere rules
		    	for (var state in states) {
				table.Add (new { 0x18, 0x1a, 0x99, 0x9a }, state, ParserAction.Execute, ParserState.Ground);
				table.Add (r (0x80, 0x90), state, ParserAction.Execute, ParserState.Ground);
				table.Add (r (0x90, 0x98), state, ParserAction.Execute, ParserState.Ground);
				table.Add (0x9c, state, ParserAction.Ignore, ParserState.Ground); // ST as terminator
				table.Add (0x1b, state, ParserAction.Clear, ParserState.Escape);  // ESC
				table.Add (0x9d, state, ParserAction.OscStart, ParserState.OscString);  // OSC
				table.Add (new { 0x98, 0x9e, 0x9f }, state, ParserAction.Ignore, ParserState.SosPmApcString);
				table.Add (0x9b, state, ParserAction.Clear, ParserState.CsiEntry);  // CSI
				table.Add (0x90, state, ParserAction.Clear, ParserState.DcsEntry);  // DCS
			}

			// rules for executable and 0x7f
			table.Add (EXECUTABLES, ParserState.Ground, ParserAction.Execute, ParserState.Ground);
			table.Add (EXECUTABLES, ParserState.Escape, ParserAction.Execute, ParserState.Escape);
			table.Add (0x7f, ParserState.Escape, ParserAction.Ignore, ParserState.Escape);
			table.Add (EXECUTABLES, ParserState.OscString, ParserAction.Ignore, ParserState.OscString);
			table.Add (EXECUTABLES, ParserState.CsiEntry, ParserAction.Execute, ParserState.CsiEntry);
			table.Add (0x7f, ParserState.CsiEntry, ParserAction.Ignore, ParserState.CsiEntry);
			table.Add (EXECUTABLES, ParserState.CsiParam, ParserAction.Execute, ParserState.CsiParam);
			table.Add (0x7f, ParserState.CsiParam, ParserAction.Ignore, ParserState.CsiParam);
			table.Add (EXECUTABLES, ParserState.CsiIgnore, ParserAction.Execute, ParserState.CsiIgnore);
			table.Add (EXECUTABLES, ParserState.CsiIntermediate, ParserAction.Execute, ParserState.CsiIntermediate);
			table.Add (0x7f, ParserState.CsiIntermediate, ParserAction.Ignore, ParserState.CsiIntermediate);
			table.Add (EXECUTABLES, ParserState.EscapeIntermediate, ParserAction.Execute, ParserState.EscapeIntermediate);
			table.Add (0x7f, ParserState.EscapeIntermediate, ParserAction.Ignore, ParserState.EscapeIntermediate);
			// osc
			table.Add (0x5d, ParserState.Escape, ParserAction.Oscstart, ParserState.OscString);
			table.Add (PRINTABLES, ParserState.OscString, ParserAction.Oscput, ParserState.OscString);
			table.Add (0x7f, ParserState.OscString, ParserAction.Oscput, ParserState.OscString);
			table.Add (new { 0x9c, 0x1b, 0x18, 0x1a, 0x07 }, ParserState.OscString, ParserAction.Oscend, ParserState.Ground);
			table.Add (r (0x1c, 0x20), ParserState.OscString, ParserAction.Ignore, ParserState.OscString);
			// sos/pm/apc does nothing
			table.Add (new { 0x58, 0x5e, 0x5f }, ParserState.Escape, ParserAction.Ignore, ParserState.SosPmApcString);
			table.Add (PRINTABLES, ParserState.SosPmApcString, ParserAction.Ignore, ParserState.SosPmApcString);
			table.Add (EXECUTABLES, ParserState.SosPmApcString, ParserAction.Ignore, ParserState.SosPmApcString);
			table.Add (0x9c, ParserState.SosPmApcString, ParserAction.Ignore, ParserState.Ground);
			table.Add (0x7f, ParserState.SosPmApcString, ParserAction.Ignore, ParserState.SosPmApcString);
			// csi entries
			table.Add (0x5b, ParserState.Escape, ParserAction.Clear, ParserState.CsiEntry);
			table.Add (r (0x40, 0x7f), ParserState.CsiEntry, ParserAction.Csidispatch, ParserState.Ground);
			table.Add (r (0x30, 0x3a), ParserState.CsiEntry, ParserAction.Param, ParserState.CsiParam);
			table.Add (0x3b, ParserState.CsiEntry, ParserAction.Param, ParserState.CsiParam);
			table.Add (new { 0x3c, 0x3d, 0x3e, 0x3f }, ParserState.CsiEntry, ParserAction.Collect, ParserState.CsiParam);
			table.Add (r (0x30, 0x3a), ParserState.CsiParam, ParserAction.Param, ParserState.CsiParam);
			table.Add (0x3b, ParserState.CsiParam, ParserAction.Param, ParserState.CsiParam);
			table.Add (r (0x40, 0x7f), ParserState.CsiParam, ParserAction.Csidispatch, ParserState.Ground);
			table.Add (new { 0x3a, 0x3c, 0x3d, 0x3e, 0x3f }, ParserState.CsiParam, ParserAction.Ignore, ParserState.CsiIgnore);
			table.Add (r (0x20, 0x40), ParserState.CsiIgnore, ParserAction.Ignore, ParserState.CsiIgnore);
			table.Add (0x7f, ParserState.CsiIgnore, ParserAction.Ignore, ParserState.CsiIgnore);
			table.Add (r (0x40, 0x7f), ParserState.CsiIgnore, ParserAction.Ignore, ParserState.Ground);
			table.Add (0x3a, ParserState.CsiEntry, ParserAction.Ignore, ParserState.CsiIgnore);
			table.Add (r (0x20, 0x30), ParserState.CsiEntry, ParserAction.Collect, ParserState.CsiIntermediate);
			table.Add (r (0x20, 0x30), ParserState.CsiIntermediate, ParserAction.Collect, ParserState.CsiIntermediate);
			table.Add (r (0x30, 0x40), ParserState.CsiIntermediate, ParserAction.Ignore, ParserState.CsiIgnore);
			table.Add (r (0x40, 0x7f), ParserState.CsiIntermediate, ParserAction.Csidispatch, ParserState.Ground);
			table.Add (r (0x20, 0x30), ParserState.CsiParam, ParserAction.Collect, ParserState.CsiIntermediate);
			// escIntermediate
			table.Add (r (0x20, 0x30), ParserState.Escape, ParserAction.Collect, ParserState.EscapeIntermediate);
			table.Add (r (0x20, 0x30), ParserState.EscapeIntermediate, ParserAction.Collect, ParserState.EscapeIntermediate);
			table.Add (r (0x30, 0x7f), ParserState.EscapeIntermediate, ParserAction.Escdispatch, ParserState.Ground);
			table.Add (r (0x30, 0x50), ParserState.Escape, ParserAction.Escdispatch, ParserState.Ground);
			table.Add (r (0x51, 0x58), ParserState.Escape, ParserAction.Escdispatch, ParserState.Ground);
			table.Add (new { 0x59, 0x5a, 0x5c }, ParserState.Escape, ParserAction.Escdispatch, ParserState.Ground);
			table.Add (r (0x60, 0x7f), ParserState.Escape, ParserAction.Escdispatch, ParserState.Ground);
			// dcs entry
			table.Add (0x50, ParserState.Escape, ParserAction.Clear, ParserState.DcsEntry);
			table.Add (EXECUTABLES, ParserState.DcsEntry, ParserAction.Ignore, ParserState.DcsEntry);
			table.Add (0x7f, ParserState.DcsEntry, ParserAction.Ignore, ParserState.DcsEntry);
			table.Add (r (0x1c, 0x20), ParserState.DcsEntry, ParserAction.Ignore, ParserState.DcsEntry);
			table.Add (r (0x20, 0x30), ParserState.DcsEntry, ParserAction.Collect, ParserState.DcsIntermediate);
			table.Add (0x3a, ParserState.DcsEntry, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (r (0x30, 0x3a), ParserState.DcsEntry, ParserAction.Param, ParserState.DcsParam);
			table.Add (0x3b, ParserState.DcsEntry, ParserAction.Param, ParserState.DcsParam);
			table.Add (new { 0x3c, 0x3d, 0x3e, 0x3f }, ParserState.DcsEntry, ParserAction.Collect, ParserState.DcsParam);
			table.Add (EXECUTABLES, ParserState.DcsIgnore, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (r (0x20, 0x80), ParserState.DcsIgnore, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (r (0x1c, 0x20), ParserState.DcsIgnore, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (EXECUTABLES, ParserState.DcsParam, ParserAction.Ignore, ParserState.DcsParam);
			table.Add (0x7f, ParserState.DcsParam, ParserAction.Ignore, ParserState.DcsParam);
			table.Add (r (0x1c, 0x20), ParserState.DcsParam, ParserAction.Ignore, ParserState.DcsParam);
			table.Add (r (0x30, 0x3a), ParserState.DcsParam, ParserAction.Param, ParserState.DcsParam);
			table.Add (0x3b, ParserState.DcsParam, ParserAction.Param, ParserState.DcsParam);
			table.Add (new { 0x3a, 0x3c, 0x3d, 0x3e, 0x3f }, ParserState.DcsParam, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (r (0x20, 0x30), ParserState.DcsParam, ParserAction.Collect, ParserState.DcsIntermediate);
			table.Add (EXECUTABLES, ParserState.DcsIntermediate, ParserAction.Ignore, ParserState.DcsIntermediate);
			table.Add (0x7f, ParserState.DcsIntermediate, ParserAction.Ignore, ParserState.DcsIntermediate);
			table.Add (r (0x1c, 0x20), ParserState.DcsIntermediate, ParserAction.Ignore, ParserState.DcsIntermediate);
			table.Add (r (0x20, 0x30), ParserState.DcsIntermediate, ParserAction.Collect, ParserState.DcsIntermediate);
			table.Add (r (0x30, 0x40), ParserState.DcsIntermediate, ParserAction.Ignore, ParserState.DcsIgnore);
			table.Add (r (0x40, 0x7f), ParserState.DcsIntermediate, ParserAction.Dcshook, ParserState.DcsPassthrough);
			table.Add (r (0x40, 0x7f), ParserState.DcsParam, ParserAction.Dcshook, ParserState.DcsPassthrough);
			table.Add (r (0x40, 0x7f), ParserState.DcsEntry, ParserAction.Dcshook, ParserState.DcsPassthrough);
			table.Add (EXECUTABLES, ParserState.DcsPassthrough, ParserAction.Dcsput, ParserState.DcsPassthrough);
			table.Add (PRINTABLES, ParserState.DcsPassthrough, ParserAction.Dcsput, ParserState.DcsPassthrough);
			table.Add (0x7f, ParserState.DcsPassthrough, ParserAction.Ignore, ParserState.DcsPassthrough);
			table.Add (new { 0x1b, 0x9c }, ParserState.DcsPassthrough, ParserAction.Dcsunhook, ParserState.Ground);
			table.Add (NonAsciiPrintable, ParserState.OscString, ParserAction.Oscput, ParserState.OscString);

			returm table;
		}

		protected delegate bool CsiHandler (int [] parameters, string collect);
		protected delegate bool OscHandler (string data);

		// Handler lookup container
		protected Dictionary<string, CsiHandler> CsiHandlers;
		protected Dictionary<string, OscHandler> OscHandlers;
		protected Dictionary<string, Action> ExecuteHandlers;
		protected Dictionary<string, Action> EscHandlers;
		protected Dictionary<string, Action> DscHandlers;
		protected IDcsHandler ActiveDcsHandler;
		protected Action<ParsingState> ErrorHandler;

		public ParserState initialState, currentState;

		// Fallacb handlers
		protected Action<int [], int, int> PrintHandlerFallback = (data, start, end) => { }
  		protected Action<int> ExecuteHandlerFallback = (code) => { };
		protected Action<string, int [], int> CsiHandlerFallback => (collect, parameters, flag) = {};
		protected Action<string, int> EscHandlerFallback => (collect, int flag) = {};
		protected Action<int, string> OscHandlerFallback = (identifier, data) => { };
		protected IDcsHandler DcsHandlerFallback = new DcsDummy ();
  		protected Func<ParsingState, ParsingState> ErrorHandlerFallback = (state) => state;

		// buffers over several parser calls
		string _osc;
		int [] _params;
		string _collect;
		Action<int [], int, int> printHandler = (data, start, end) => { };

		TransitionTable table;
		public EscapeSequenceParser ()
		{
			table = BuildVt500TransitionTable ();x
		}
	}
}
