using UnityEngine;

namespace TheLastKing.Menu
{
    public class ResetNetModeOnMenu : MonoBehaviour
    {
        private void Awake() => NetRuntime.Mode = NetMode.None;
    }
}
