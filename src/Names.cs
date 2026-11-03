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
        public static string ListFile(string home, string name = "friendlynames.txt")
        {
            var env = Environment.GetEnvironmentVariable("HD2SDK_HASHLISTS");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, name))) return Path.Combine(env, name);
            var downloaded = Path.Combine(home ?? "", "resource_names", name);
            if (home != null && File.Exists(downloaded)) return downloaded;
            try
            {
                var blender = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blender Foundation", "Blender");
                if (!Directory.Exists(blender)) return null;
                return Directory.GetDirectories(blender)
                    .Select(v => Path.Combine(v, "scripts", "addons", "HD2SDK-CommunityEdition", "hashlists", name))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>HD2SDK's names of the game's archives (archivehashes.json: category, archive hash,
        /// name), looked for where the name list is; by archive hash.</summary>
        public static Dictionary<ulong, (string Category, string Name)> Archives(string home)
        {
            var d = new Dictionary<ulong, (string, string)>();
            var file = ListFile(home, "archivehashes.json");
            if (file == null) return d;
            try
            {
                if (new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(file)) is Dictionary<string, object> all)
                    foreach (var cat in all)
                        if (cat.Value is Dictionary<string, object> archives)
                            foreach (var a in archives)
                                if (a.Value is string n && n.Length > 0 && ulong.TryParse(a.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) && !d.ContainsKey(h))
                                    d[h] = (cat.Key, n);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException) { }
            return d;
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
