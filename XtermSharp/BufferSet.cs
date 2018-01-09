using System;
namespace XtermSharp {

	/// <summary>
	/// The BufferSet represents the set of two buffers used by xterm terminals (normal and alt) and
 	/// provides also utilities for working with them.
	/// </summary>
	public class BufferSet {
		public Buffer Normal { get; private set; }
		public Buffer Alt { get; private set; }
		public Buffer Active { get; private set; }

		public BufferSet (ITerminal terminal)
		{
			Normal = new Buffer (terminal, hasScrollback: true);
			Normal.FillViewportRows ();

			// The alt buffer should never have scrollback.
			// See http://invisible-island.net/xterm/ctlseqs/ctlseqs.html#h2-The-Alternate-Screen-Buffer
			Alt = new Buffer (terminal, hasScrollback: false);

			Active = Normal;
			SetupTabStops ();
		}

		/// <summary>
		/// Raised when a buffer is activated, the parameters is the buffer that was activated.
		/// </summary>
		public event Action<Buffer> Activated;

		/// <summary>
		/// Sets the normal Buffer of the BufferSet as its currently active Buffer
		/// </summary>
		public void ActivateNormalBuffer ()
		{
			// The alt buffer should always be cleared when we switch to the normal
			// buffer. This frees up memory since the alt buffer should always be new
			// when activated.

			Alt.Clear ();

			Active = Normal;
			if (Activated != null)
				Activated (Normal);
		}

		/// <summary>
		/// Sets the alt Buffer of the BufferSet as its currently active Buffer
		/// </summary>
		public void ActivateAltBuffert ()
		{
			// Since the alt buffer is always cleared when the normal buffer is
			// activated, we want to fill it when switching to it.

			Alt.FillViewportRows ();
			Active = Alt;
			if (Activated != null)
				Activated (Alt);
		}

		/// <summary>
		/// Resizes both normal and alt buffers, adjusting their data accordingly.
		/// </summary>
		/// <returns>The resize.</returns>
		/// <param name="newColumns">The new number of columns.</param>
		/// <param name="newRows">The new number of rows.</param>
		public void Resize (int newColumns, int newRows)
		{
			Normal.Resize (newColumns, newRows);
			Alt.Resize (newColumns, newRows);
		}

		/// <summary>
		/// Setups the tab stops.
		/// </summary>
		/// <param name="index">The index to start setting up tab stops from, or -1 to do it from the start.</param>
		public void SetupTabStops (int index = -1)
		{
			Normal.SetupTabStops (index);
			Alt.SetupTabStops (index);
		}

	}
}
