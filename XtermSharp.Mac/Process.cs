using System;

namespace XtermSharp.Mac {
	/// <summary>
	/// An abstract class that represents some process that a terminal can represent
	/// </summary>
	public abstract class Process {
		/// <summary>
		/// Gets a value indicating whether the process is running
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// This event is raised when the process starts
		/// </summary>
		public event Action OnStarted;

		/// <summary>
		/// This event is raised when the process exits
		/// </summary>
		public event Action OnExited;

		/// <summary>
		/// This event is raised when the process emits data and should be sent to the terminal
		/// </summary>
		public event Action<byte []> OnData;

		/// <summary>
		/// Notifies the subshell that the terminal emited some data, eg a response to device status
		/// </summary>
		public abstract void NotifyDataEmitted (string txt);

		/// <summary>
		/// Notifies the subshell that the size has changed
		/// </summary>
		public abstract void NotifySizeChanged (int newCols, int newRows, nfloat width, nfloat height);

		/// <summary>
		/// Notfies the subshell that the user entered some data
		/// </summary>
		public abstract void NotifyUserInput (byte [] data);

		protected void OnStart ()
		{
			// TODO: throw error if already started
			OnStarted?.Invoke ();
			IsRunning = true;
		}

		protected void OnStop ()
		{
			IsRunning = false;
			OnExited?.Invoke ();
		}

		protected void SendOnData (byte [] data)
		{
			OnData?.Invoke (data);
		}
	}
}
