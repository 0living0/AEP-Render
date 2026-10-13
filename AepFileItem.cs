using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AepBatchRenderer
{
    internal sealed class AepFileItem
    {
        private static readonly Regex VersionPattern = new Regex(
            @"^(?<group>.+)_(?<tag>[a-zA-Z]+)(?<version>\d+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string FullPath { get; private set; }
        public string FileName { get; private set; }
        public string DisplayName { get; private set; }
        public string GroupKey { get; private set; }
        public int Version { get; private set; }
        public string VersionText { get; private set; }
        public long Length { get; private set; }
        public DateTime ModifiedAt { get; private set; }

        private AepFileItem() { }

        public static AepFileItem FromPath(string path)
        {
            FileInfo info = new FileInfo(path);
            string stem = Path.GetFileNameWithoutExtension(info.Name);
            Match versionMatch = VersionPattern.Match(stem);
            int separator = stem.IndexOf('_');

            string groupKey = stem;
            int version = -1;
            string versionText = "—";
            if (versionMatch.Success)
            {
                groupKey = versionMatch.Groups["group"].Value;
                int parsed;
                if (Int32.TryParse(versionMatch.Groups["version"].Value, out parsed))
                {
                    version = parsed;
                    versionText = versionMatch.Groups["tag"].Value + versionMatch.Groups["version"].Value;
                }
            }

            return new AepFileItem
            {
                FullPath = info.FullName,
                FileName = info.Name,
                DisplayName = separator >= 0 ? stem.Substring(separator + 1) : stem,
                GroupKey = groupKey.ToLowerInvariant(),
                Version = version,
                VersionText = versionText,
                Length = info.Length,
                ModifiedAt = info.LastWriteTime
            };
        }

        public string SizeText
        {
            get
            {
                if (Length < 1024) return Length + " B";
                if (Length < 1024 * 1024) return (Length / 1024d).ToString("0.0") + " KB";
                return (Length / 1024d / 1024d).ToString("0.0") + " MB";
            }
        }
    }
}

