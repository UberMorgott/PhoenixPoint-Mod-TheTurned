using System.Collections.Generic;
using TheTurned.Monsters.Arthron;

namespace TheTurned.Core
{
    /// <summary>
    /// Holds every registered <see cref="ITurnedMonster"/>. Core iterates this list to build classes and
    /// to map recruit hotkeys. Adding a monster = one line in <see cref="RegisterDefaults"/> (plus the
    /// monster's own folder under src/Monsters).
    /// </summary>
    internal static class MonsterRegistry
    {
        private static readonly List<ITurnedMonster> _monsters = new List<ITurnedMonster>();

        /// <summary>All registered monsters.</summary>
        internal static IReadOnlyList<ITurnedMonster> All => _monsters;

        /// <summary>Register the built-in monsters. Idempotent (clears first), called from OnModEnabled.</summary>
        internal static void RegisterDefaults()
        {
            _monsters.Clear();
            Register(new ArthronMonster());
        }

        internal static void Register(ITurnedMonster monster)
        {
            if (monster != null && !_monsters.Contains(monster))
            {
                _monsters.Add(monster);
            }
        }
    }
}
