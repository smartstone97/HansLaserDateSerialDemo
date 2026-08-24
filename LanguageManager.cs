using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HansLaserDateSerialDemo
{
    internal sealed class LanguageOption
    {
        public LanguageOption(string cultureName, string displayName)
        {
            CultureName = cultureName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string CultureName { get; }

        public string DisplayName { get; }
    }

    internal static class LanguageManager
    {
        private const string LanguageConfigFile = "language.config";
        private const string NeutralCultureName = "en";

        public static string CurrentCultureName
        {
            get
            {
                CultureInfo culture = Resources.Culture;
                return culture == null || string.IsNullOrWhiteSpace(culture.Name)
                    ? NeutralCultureName
                    : culture.Name;
            }
        }

        public static void ApplySavedLanguage()
        {
            ApplyCulture(ReadSavedCultureName());
        }

        public static void SaveAndApply(string cultureName)
        {
            string normalized = NormalizeCultureName(cultureName);
            File.WriteAllText(GetLanguageConfigPath(), normalized);
            ApplyCulture(normalized);
        }

        public static List<LanguageOption> GetAvailableLanguages()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NeutralCultureName
            };

            AddResourceCultures(names, AppDomain.CurrentDomain.BaseDirectory);
            string sourceDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AddResourceCultures(names, sourceDirectory);
            AddProjectResxCultures(names, AppDomain.CurrentDomain.BaseDirectory);

            return names
                .Select(CreateOption)
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void ApplyCulture(string cultureName)
        {
            string normalized = NormalizeCultureName(cultureName);
            CultureInfo culture = string.Equals(normalized, NeutralCultureName, StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(normalized);

            Resources.Culture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }

        private static string ReadSavedCultureName()
        {
            string path = GetLanguageConfigPath();
            if (!File.Exists(path))
                return NeutralCultureName;

            try
            {
                return NormalizeCultureName(File.ReadAllText(path).Trim());
            }
            catch (CultureNotFoundException)
            {
                return NeutralCultureName;
            }
        }

        private static string NormalizeCultureName(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return NeutralCultureName;

            string trimmed = cultureName.Trim();
            if (string.Equals(trimmed, NeutralCultureName, StringComparison.OrdinalIgnoreCase))
                return NeutralCultureName;

            return CultureInfo.GetCultureInfo(trimmed).Name;
        }

        private static string GetLanguageConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LanguageConfigFile);
        }

        private static void AddResourceCultures(ISet<string> names, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            foreach (string resourceFile in Directory.GetFiles(directory, "*.resources.dll", SearchOption.AllDirectories))
            {
                if (!string.Equals(Path.GetFileName(resourceFile), assemblyName + ".resources.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cultureName = Path.GetFileName(Path.GetDirectoryName(resourceFile));
                AddCultureName(names, cultureName);
            }
        }

        private static void AddProjectResxCultures(ISet<string> names, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            foreach (string resxFile in Directory.GetFiles(directory, "Resources.*.resx", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileNameWithoutExtension(resxFile);
                const string prefix = "Resources.";
                if (fileName.Length <= prefix.Length || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                AddCultureName(names, fileName.Substring(prefix.Length));
            }
        }

        private static void AddCultureName(ISet<string> names, string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return;

            try
            {
                names.Add(CultureInfo.GetCultureInfo(cultureName).Name);
            }
            catch (CultureNotFoundException)
            {
            }
        }

        private static LanguageOption CreateOption(string cultureName)
        {
            CultureInfo culture = string.Equals(cultureName, NeutralCultureName, StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo(NeutralCultureName)
                : CultureInfo.GetCultureInfo(cultureName);

            return new LanguageOption(culture.Name, culture.NativeName);
        }
    }
}
