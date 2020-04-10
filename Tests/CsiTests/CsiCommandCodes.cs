namespace XtermSharp {
	/// <summary>
	/// Defines the set of known Csi command codes
	/// TODO: maybe move this to Xterm proper
	/// </summary>
	static class CsiCommandCodes {
		/// <summary>
		/// Wraparound
		/// </summary>
		public const int DECAWM = 7;
		/// <summary>
		/// ReverseWraparound
		/// </summary>
		public const int ReverseWraparound = 45;
		/// <summary>
		/// Margin Mode
		/// </summary>
		public const int DECLRMM = 69;


		public const int DeviceStatus = 6;
	}
}
