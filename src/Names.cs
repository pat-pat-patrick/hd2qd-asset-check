using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Hd2Qd.AssetCheck
{
    /// <summary>Resource names ("Helmet O-3 Free Spirit", or a path) from HD2SDK's
    /// friendlynames.txt (lines "decimal-id name"), looked for where the manager looks: in
    /// HD2SDK_HASHLISTS, else the list the Resource names plugin downloads into the settings
    /// folder, else the newest Blender's HD2SDK add-on. None found: no names, the ids stay.</summary>
    static class Names
    {
        public static string ListFile(string home)
        {
            var env = Environment.GetEnvironmentVariable("HD2SDK_HASHLISTS");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "friendlynames.txt"))) return Path.Combine(env, "friendlynames.txt");
            var downloaded = Path.Combine(home ?? "", "resource_names", "friendlynames.txt");
            if (home != null && File.Exists(downloaded)) return downloaded;
            try
            {
                var blender = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blender Foundation", "Blender");
                if (!Directory.Exists(blender)) return null;
                return Directory.GetDirectories(blender)
                    .Select(v => Path.Combine(v, "scripts", "addons", "HD2SDK-CommunityEdition", "hashlists", "friendlynames.txt"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>The names, by resource id; the first name a list gives an id is kept.</summary>
        public static Dictionary<ulong, string> Load(string home)
        {
            var d = new Dictionary<ulong, string>();
            var file = ListFile(home);
            if (file == null) return d;
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    int sp = line.IndexOf(' ');
                    if (sp <= 0) continue;
                    var name = line.Substring(sp + 1).Trim();
                    if (name.Length > 0 && ulong.TryParse(line.Substring(0, sp), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && !d.ContainsKey(id))
                        d[id] = name;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            return d;
        }
    }
}
