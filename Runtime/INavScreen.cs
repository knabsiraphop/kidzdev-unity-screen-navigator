using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// A navigable view managed by a navigator — typically a UI panel, but it can be anything with
    /// a <see cref="GameObject"/> root.
    /// <para>
    /// A screen is <b>visually passive</b>: it does not animate itself. The navigator manages its
    /// active state and an injected <see cref="INavTransition"/> animates the move. The screen only
    /// receives lifecycle callbacks so it can load/bind data, subscribe to events, and release them.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Lifecycle order for a push of B over A:
    /// <c>B.OnPushed(arg)</c> → <c>A.OnCovered()</c> → transition → (A deactivated).<br/>
    /// For a pop of B back to A:
    /// <c>A.OnRevealed()</c> → <c>B.OnPopped()</c> → transition → (B deactivated and released).
    /// </remarks>
    public interface INavScreen
    {
        /// <summary>
        /// The screen's root GameObject. The navigator toggles its active state and the transition
        /// animates it. Must not be <c>null</c>.
        /// </summary>
        GameObject Root { get; }

        /// <summary>
        /// Called once when the screen is pushed onto the stack, before its enter transition.
        /// Bind incoming data from <paramref name="arg"/> here.
        /// </summary>
        /// <param name="arg">Optional payload passed to <c>PushAsync</c>/<c>ReplaceAsync</c>; may be <c>null</c>.</param>
        void OnPushed(object arg);

        /// <summary>Called when this screen becomes the top again because the screen above it was popped.</summary>
        void OnRevealed();

        /// <summary>Called when another screen is pushed over this one (it is no longer the top), before it is covered.</summary>
        void OnCovered();

        /// <summary>
        /// Called once when the screen is popped off the stack, before its exit transition.
        /// Unsubscribe events and release transient state here.
        /// </summary>
        void OnPopped();

        /// <summary>
        /// Called when a back request reaches this screen while it is the top. Return <c>true</c> to
        /// consume the back press (e.g. close an inline dialog) and stop the navigator from popping;
        /// return <c>false</c> to let the navigator pop this screen.
        /// </summary>
        bool OnBackPressed();
    }
}
