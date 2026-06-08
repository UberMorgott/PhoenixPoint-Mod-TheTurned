using Base.Defs;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;

namespace TheTurned.Core
{
    /// <summary>
    /// Owns the ONE shared "recruit marker" GameTag used by ALL turned units (the Harmony CheckIsHuman
    /// Postfix keys off it), and creates the per-monster <see cref="ClassTagDef"/>. Both are created
    /// idempotently via stable GUIDs.
    ///
    /// The shared marker keeps the original mod's GUID/name ("TheTurned_RecruitTag") so existing saves
    /// and re-enables stay idempotent.
    /// </summary>
    internal static class Tags
    {
        // Shared across every monster (was ArthronClass.MarkerTag* — preserved verbatim for idempotency).
        private const string MarkerTagGuid = "c3e5a7b9-2d4f-5b6c-9e8a-3f0d1b2c4e6f";
        internal const string MarkerTagName = "TheTurned_RecruitTag";

        /// <summary>The single shared marker tag (created on first access via <see cref="EnsureMarker"/>).</summary>
        internal static GameTagDef RecruitMarkerTag { get; private set; }

        /// <summary>Idempotently get-or-create the shared marker GameTag.</summary>
        internal static GameTagDef EnsureMarker(DefRepository repo)
        {
            if (RecruitMarkerTag != null)
            {
                return RecruitMarkerTag;
            }
            if (repo == null)
            {
                return null;
            }
            if (repo.GetDef(MarkerTagGuid) is GameTagDef existing)
            {
                RecruitMarkerTag = existing;
                return existing;
            }
            GameTagDef tag = repo.CreateDef<GameTagDef>(MarkerTagGuid);
            if (tag != null)
            {
                tag.name = MarkerTagName;
                tag.ResourcePath = "Defs/GameTags/" + MarkerTagName;
            }
            RecruitMarkerTag = tag;
            return tag;
        }

        /// <summary>Idempotently get-or-create a per-monster <see cref="ClassTagDef"/>.</summary>
        internal static ClassTagDef EnsureClassTag(DefRepository repo, ITurnedMonster monster)
        {
            if (repo == null || monster == null)
            {
                return null;
            }
            if (repo.GetDef(monster.ClassTagGuid) is ClassTagDef existing)
            {
                return existing;
            }
            ClassTagDef tag = repo.CreateDef<ClassTagDef>(monster.ClassTagGuid);
            if (tag != null)
            {
                tag.name = monster.ClassTagName;
                tag.ResourcePath = "Defs/GameTags/Classes/" + monster.ClassTagName;
            }
            return tag;
        }

        /// <summary>Look up the per-monster class tag (already created by <see cref="EnsureClassTag"/>).</summary>
        internal static ClassTagDef GetClassTag(DefRepository repo, ITurnedMonster monster)
        {
            if (repo == null || monster == null)
            {
                return null;
            }
            return repo.GetDef(monster.ClassTagGuid) as ClassTagDef;
        }
    }
}
