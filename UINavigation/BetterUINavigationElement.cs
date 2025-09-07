using UnityEngine;

namespace Game.Input.UINavigation
{
    [DisallowMultipleComponent]
    public class BetterUINavigationElement : MonoBehaviour
    {
        public int AutoSelectPriority = 0;
        public bool IgnoreFromAutoSelection = false;
    }
}