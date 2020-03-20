namespace XtermSharp {

	/// <summary>
	/// 
	/// </summary>
	public interface ITerminalDelegate {
		/// <summary>
		/// 
		/// </summary>
		void ShowCursor (Terminal source);

		/// <summary>
		/// This event is raised when the title of the terminal has been changed
		/// </summary>
		void SetTerminalTitle (Terminal source, string title);

		/// <summary>
		/// This event is triggered from the engine, when the request to resize the window is received from an escape sequence.
		/// </summary>
		void SizeChanged (Terminal source);

		/// <summary>
		/// Used to respond to the client running on the other end.  This information should be sent to the remote end or subshell.
		/// </summary>
		void Send (byte [] data);
	}

	/// <summary>
	/// A simple ITerminalDelegate that does nothing
	/// </summary>
	public class SimpleTerminalDelegate : ITerminalDelegate {
		public void Send (byte [] data)
		{
		}

		public virtual void SetTerminalTitle (Terminal source, string title)
		{

		}

		public virtual void ShowCursor (Terminal source)
		{
		}

		public virtual void SizeChanged (Terminal source)
		{
		}
	}
}
