using UnityEngine.UI;

namespace Game.Input.UINavigation
{
    public abstract class SelectableAccessor : Selectable
    {
        public static Selectable[] allSelectablesArrayNonAlloc => s_Selectables;
    }
}