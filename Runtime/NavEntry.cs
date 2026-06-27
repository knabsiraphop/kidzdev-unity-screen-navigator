namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>One slot on the navigation stack: the key, its resolved screen, and the push payload.</summary>
    internal sealed class NavEntry<TKey>
    {
        public readonly TKey Key;
        public readonly INavScreen Screen;
        public readonly object Arg;

        public NavEntry(TKey key, INavScreen screen, object arg)
        {
            Key = key;
            Screen = screen;
            Arg = arg;
        }
    }
}
