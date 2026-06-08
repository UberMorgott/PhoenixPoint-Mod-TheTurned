using UnityEngine;

namespace TheTurned
{
    /// <summary>
    /// Legacy-input poller attached to ModGO. Phoenix Point is Unity 2019.4.x with the
    /// legacy input module active, so UnityEngine.Input.GetKeyDown works.
    /// Fires once per press of Ctrl+Shift+T; the geoscape guard lives in ArthronRecruiter.
    /// </summary>
    public class RecruitHotkey : MonoBehaviour
    {
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.T))
            {
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift)
            {
                return;
            }

            ArthronRecruiter.RecruitOne();
        }
    }
}
