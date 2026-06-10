using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TheTurned.Core
{
    /// <summary>Phase-4 (mutoid-style progression) global state + guard.</summary>
    internal static class Phase4
    {
        internal const string TftvModId = "phoenixrising.tftv";

        /// <summary>DEV: every Phase-4 recruit progression cell clickable/buyable at level 1
        /// (see <see cref="DevUnlockPatch"/>). Flip false for release.
        /// Currently FALSE to test the real auto-unlock-by-level gate (cell N at level N) + SP purchase; use the
        /// Ctrl+Shift+U dev hotkey to level a recruit and watch cells unlock one per level.</summary>
        internal const bool DevUnlockAllLevels = false;

        /// <summary>REV-2 2-row in-panel cell layout (top=5 SP/level Primary track, bottom=Mutagen Personal).
        /// When true, the recruit is routed to the HUMAN ability-track container (gate NOT OR'd into
        /// _hasPandoranProgression) with a 5-level LevelProgressionDef, instead of the mutoid popup container.
        /// M-PROBE de-risks this; M-LAYOUT/M-CELLS formalize it. Flip false to revert to the REV-1 mutoid path.
        /// REJECTED after M-PROBE in-game: the human container broke the clean МУТОИДЫ look (1-7 tabs, 3 rows,
        /// lost DNA cell + mutoid skin). Staying on the mutoid container (clean 2-row purple layout) -> false.</summary>
        internal const bool TwoRowCellLayout = false;

        /// <summary>True only when TFTV resolved as dependency. All Phase-4 features gate on this.</summary>
        internal static bool Enabled { get; private set; }

        private static bool _initialized;

        internal static void Init(TheTurnedMain main)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            try
            {
                Enabled = main != null && main.Dependencies != null
                    && main.Dependencies.Any(d => d != null && d.ID == TftvModId);
            }
            catch (Exception e)
            {
                Enabled = false;
                TheTurnedMain.LogWarn("[TheTurned] Phase4.Init dependency probe failed: " + e.Message);
            }
            if (!Enabled)
            {
                TheTurnedMain.LogWarn("[TheTurned] TFTV not detected; Phase-4 progression disabled, falling back to Phase-2/3 fixed track.");
            }
        }

        /// <summary>Any marked turned recruit (shared marker tag, all monster classes) + Phase-4 on.</summary>
        internal static bool IsPhase4Recruit(GeoCharacter character)
        {
            if (!Enabled || character == null)
            {
                return false;
            }
            TacCharacterDef template = character.TemplateDef;
            return template != null && template.Data != null
                && Tags.RecruitMarkerTag != null
                && template.Data.GameTags != null
                && template.Data.GameTags.Contains(Tags.RecruitMarkerTag);
        }

        /// <summary>Deterministic GUID for new Phase-4 defs (same MD5 recipe as ArthronArms.DeriveGuid, different prefix).</summary>
        internal static Guid DeriveGuid(string seed)
        {
            using (MD5 md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("TheTurned.Phase4:" + seed)));
            }
        }
    }
}
