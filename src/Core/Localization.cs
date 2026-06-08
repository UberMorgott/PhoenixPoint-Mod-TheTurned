using I2.Loc;
using System;
using System.IO;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// CSV localization loader mirroring the Officer mod's Helper.AddLocalizationFromCSV. Resolves
    /// Assets\Localization from the mod's entry directory and imports new terms into the I2 source.
    /// No-op when no file is supplied / present (the mod currently ships no CSV).
    /// </summary>
    internal static class Localization
    {
        private static string LocalizationDirectory
        {
            get
            {
                string modDir = TheTurnedMain.Main?.Instance?.Entry?.Directory;
                return string.IsNullOrEmpty(modDir) ? null : Path.Combine(modDir, "Assets", "Localization");
            }
        }

        /// <summary>Import terms from a CSV under Assets\Localization. Safe to call when the file is absent.</summary>
        internal static void AddLocalizationFromCSV(string localizationFileName, string category = null)
        {
            if (string.IsNullOrEmpty(localizationFileName))
            {
                return;
            }
            try
            {
                string dir = LocalizationDirectory;
                if (string.IsNullOrEmpty(dir))
                {
                    return;
                }
                string filePath = Path.Combine(dir, localizationFileName);
                if (!File.Exists(filePath))
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Localization CSV not found (skipping): {filePath}");
                    return;
                }
                string csv = File.ReadAllText(filePath);
                if (!csv.EndsWith("\n"))
                {
                    csv += "\n";
                }
                LanguageSourceData source = category == null
                    ? LocalizationManager.Sources[0]
                    : LocalizationManager.Sources.First(s => s.GetCategories().Contains(category));
                if (source == null)
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] No language source with category {category} found.");
                    return;
                }
                int before = source.mTerms.Count;
                source.Import_CSV(string.Empty, csv, eSpreadsheetUpdateMode.AddNewTerms, ',');
                LocalizationManager.LocalizeAll(true);
                int after = source.mTerms.Count;
                TheTurnedMain.Main?.Logger?.LogInfo(
                    $"[TheTurned] Added {after - before} localization terms from {localizationFileName}.");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Localization import failed: {e}");
            }
        }
    }
}
