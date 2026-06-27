namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Controls what a navigator does when a Push/Pop/Replace is requested while a transition
    /// is already in flight.
    /// </summary>
    public enum NavQueuePolicy
    {
        /// <summary>
        /// The incoming request is dropped (and logged when logging is enabled). The in-flight
        /// transition continues unchanged.
        /// <para>
        /// This is the default and the safe choice for touch UI: a double-tap on a button cannot
        /// push the same screen twice, and back-button mashing cannot pop past the intended depth
        /// during an animation.
        /// </para>
        /// </summary>
        DropWhileBusy,

        /// <summary>
        /// Incoming requests are appended to a FIFO queue and drained one at a time after the
        /// current transition settles. Every operation runs in order; none are dropped.
        /// <para>
        /// Use this when each operation is individually meaningful and must not be lost — e.g. a
        /// scripted sequence that pushes several screens in a row.
        /// </para>
        /// </summary>
        Queue,
    }
}
