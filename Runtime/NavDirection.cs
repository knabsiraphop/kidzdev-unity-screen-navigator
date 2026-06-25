namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// Direction of a navigation transition, passed to <see cref="INavTransition"/> so an
    /// implementation can animate forward and backward moves differently (e.g. slide left vs right).
    /// </summary>
    public enum NavDirection
    {
        /// <summary>A screen is being pushed onto the stack (or a replace). The new screen covers the old one.</summary>
        Forward,

        /// <summary>A screen is being popped off the stack. The screen underneath is revealed.</summary>
        Backward,
    }
}
