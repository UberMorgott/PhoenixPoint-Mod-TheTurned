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
