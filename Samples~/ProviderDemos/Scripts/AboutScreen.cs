using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.ScreenNavigator.Demo
{
    /// <summary>
    /// A <see cref="NavPanel"/> subclass that fills its body label from the <see cref="OnPushed"/>
    /// lifecycle hook — proving the navigator drives the screen regardless of how it was loaded.
    /// </summary>
    public sealed class AboutScreen : NavPanel
    {
        [SerializeField] private Text _bodyLabel;

        protected override void OnPushed(object arg)
        {
            if (_bodyLabel != null)
                _bodyLabel.text = "This text was set by the\nOnPushed() lifecycle hook.";
        }
    }
}
