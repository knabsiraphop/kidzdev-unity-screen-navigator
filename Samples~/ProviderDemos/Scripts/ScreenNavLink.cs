using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.ScreenNavigator.Demo
{
    /// <summary>
    /// Attach to a Button inside a screen (scene panel or prefab). Clicking pushes <see cref="_target"/>.
    /// Routes through <see cref="NavDemoController.Current"/> so prefab buttons need no scene reference.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ScreenNavLink : MonoBehaviour
    {
        [SerializeField] private string _target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => NavDemoController.Current?.Push(_target));
        }
    }
}
