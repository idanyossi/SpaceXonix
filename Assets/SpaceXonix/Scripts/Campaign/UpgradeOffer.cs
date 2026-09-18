using System.Collections.Generic;

namespace SpaceXonix.Campaign
{
    /// <summary>Picks the random upgrade choices offered between stages.</summary>
    public static class UpgradeOffer
    {
        /// <summary>
        /// Fills <paramref name="results"/> with up to <paramref name="count"/> distinct upgrades the run can still
        /// take, chosen with the given random source. Fewer are returned only when fewer remain eligible.
        /// </summary>
        public static void Build(IReadOnlyList<UpgradeDefinition> pool, RunUpgradeModel run, int count,
            System.Random random, List<UpgradeDefinition> results)
        {
            results.Clear();
            if (pool == null || run == null || count <= 0) return;
            var eligible = new List<UpgradeDefinition>();
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] != null && run.CanTake(pool[i]) && !eligible.Contains(pool[i])) eligible.Add(pool[i]);
            random ??= new System.Random();
            while (results.Count < count && eligible.Count > 0)
            {
                var index = random.Next(eligible.Count);
                results.Add(eligible[index]);
                eligible.RemoveAt(index);
            }
        }
    }
}
