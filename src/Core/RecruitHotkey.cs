using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Legacy-input poller attached to ModGO. Phoenix Point is Unity 2019.4.x with the legacy input
    /// module active, so UnityEngine.Input.GetKeyDown works. Iterates the registered monsters and, on
    /// Ctrl+Shift+&lt;monster.RecruitKey&gt;, recruits that monster. The geoscape guard lives in
    /// <see cref="TurnedRecruiter"/>.
    /// </summary>
    public class RecruitHotkey : MonoBehaviour
    {
        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift)
            {
                return;
            }

            var monsters = MonsterRegistry.All;
            for (int i = 0; i < monsters.Count; i++)
            {
                ITurnedMonster m = monsters[i];
                if (Input.GetKeyDown(m.RecruitKey))
                {
                    TurnedRecruiter.RecruitMonster(m);
                }
            }
        }
    }
}
