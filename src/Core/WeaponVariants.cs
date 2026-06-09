using Base.Defs;
using PhoenixPoint.Tactical.Entities.DamageKeywords;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Shared "clone a weapon + append one status damage keyword" primitive for the Phase-4 claw and
    /// head/spray rows. One responsibility: get-or-create an idempotent WeaponDef variant.
    /// </summary>
    internal static class WeaponVariants
    {
        /// <summary>
        /// Idempotently clone <paramref name="source"/> and append one
        /// <c>DamageKeywordPair { keywordDefName, keywordValue }</c> to the clone's payload.
        /// Returns null (with a warn) when the keyword def or the clone cannot be produced.
        ///
        /// UNSHARE VERIFICATION (decompile + TFTV real source):
        ///  - DefRepository.CreateDef(id, original) with matching type uses Object.Instantiate
        ///    (Base.Defs\DefRepository.cs:263) — Unity serialization DEEP-copies [Serializable]
        ///    plain-class fields, and DamagePayload is [Serializable]
        ///    (PhoenixPoint.Tactical.Entities\DamagePayload.cs:21), so the clone gets its OWN
        ///    DamagePayload + its own List&lt;DamageKeywordPair&gt;. BaseDef.UnshareMembers() is an
        ///    empty virtual and is not even called on the same-type path.
        ///  - TFTV ships code relying on exactly this: Helper.CreateDefFromClone wraps
        ///    Repo.CreateDef (Helper.cs:149) and callers mutate the clone's DamagePayload in place
        ///    right after cloning (TFTVDefsInjectedOnlyOnce.cs:4949-4952) without touching the source.
        ///  - Defensive re-wrap of the list below is therefore belt-and-braces only: it guards the
        ///    (unobserved) list-object-sharing case at the cost of one allocation.
        /// </summary>
        internal static WeaponDef GetOrCreateWeaponVariant(DefRepository repo, WeaponDef source,
            string guidSeed, string cloneName, string keywordDefName, float keywordValue)
        {
            if (repo == null || source == null)
            {
                return null;
            }
            string guid = Phase4.DeriveGuid(guidSeed).ToString();
            if (repo.GetDef(guid) is WeaponDef existing)
            {
                return existing;
            }
            DamageKeywordDef keyword = repo.GetAllDefs<DamageKeywordDef>()
                .FirstOrDefault(d => d != null && d.name == keywordDefName);
            if (keyword == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] weapon variant '{cloneName}': keyword '{keywordDefName}' not found — variant skipped");
                return null;
            }
            WeaponDef clone = repo.CreateDef<WeaponDef>(guid, source);
            if (clone == null || clone.DamagePayload == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] weapon variant '{cloneName}': clone failed (clone={(clone == null ? "null" : "ok")}) — variant skipped");
                return null;
            }
            clone.name = cloneName;
            // Defensive list re-wrap (see UNSHARE VERIFICATION above), then append the new pair.
            clone.DamagePayload.DamageKeywords =
                new List<DamageKeywordPair>(clone.DamagePayload.DamageKeywords ?? new List<DamageKeywordPair>());
            clone.DamagePayload.DamageKeywords.Add(new DamageKeywordPair
            {
                DamageKeywordDef = keyword,
                Value = keywordValue
            });
            return clone;
        }
    }
}
