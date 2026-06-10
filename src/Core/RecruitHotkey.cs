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

            // TEMP M1 render-proof: Ctrl+Shift+Y cycles the recruit through two armor loadouts (A then B).
            // REMOVE in M7 (plan 2026-06-10-cell-progression-v1.md, Task M7.1).
            if (Input.GetKeyDown(KeyCode.Y))
            {
                var geo = Base.Core.GameUtl.CurrentLevel()?.GetComponent<PhoenixPoint.Geoscape.Levels.GeoLevelController>();
                if (geo != null)
                {
                    CellDevDump.CycleArmorOnFirstRecruit(geo);
                }
            }

            // TEMP dev: Ctrl+Shift+U adds ONE level to the first Phase-4 recruit (real auto-unlock-by-level
            // mechanic — grants XP -> level up -> SkillPoints). REMOVE with the other dev keys at cleanup.
            if (Input.GetKeyDown(KeyCode.U))
            {
                var geo = Base.Core.GameUtl.CurrentLevel()?.GetComponent<PhoenixPoint.Geoscape.Levels.GeoLevelController>();
                if (geo != null)
                {
                    CellDevDump.LevelUpFirstRecruit(geo);
                }
            }
        }
    }
}
