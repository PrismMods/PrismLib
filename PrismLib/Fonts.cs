using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PrismLib
{
    /* Shared font packs.

       Fonts live here rather than in a mod because they are big and every mod wants the same
       ones: a Korean family runs to tens of megabytes, and Bismuth and Sapphire both restyle
       game text. Installed once under PrismLib, a pack is visible to all of them — the
       alternative is each mod downloading its own copy of the same 30 MB.

       This is in PrismLib.dll rather than PrismLib.UI because the shared INSTALL is the point,
       and because none of it touches Unity: it is a feed, a download, a hash and a zip. Turning
       the files into TMP_FontAssets is the mod's job, and each mod already knows how.

       Every call blocks. Threading belongs to the caller — the mods already drain background
       work on their own update loop, and a library that spawned its own threads would be
       fighting them for when the result lands. */
    public static class Fonts
    {
        /* A plain file in the repo rather than the releases API: the API is rate limited per IP
           and this list changes about once a year. Same reasoning as the PrismLib feed. */
        private const string Feed = "https://raw.githubusercontent.com/PrismMods/PrismLib/main/fonts.json";
        private const int NetTimeoutMs = 30000;

        public sealed class Pack
        {
            public string Id;        // folder name, and the key the mods use
            public string Name;      // display name
            public string Note;      // "9 weights · Latin + Korean"
            public string Size;      // "5.3 MB", as written in the feed — no client-side maths
            public string Url;
            public string Sha256;    // optional; skipped when the feed omits it
            public string Preview;   // optional PNG, so a mod can show the face before downloading
        }

        /// Last fetched catalogue. Empty until Fetch succeeds.
        public static Pack[] Catalogue { get; private set; } = new Pack[0];

        /* Beside PrismLib.dll, which the bootstrapper puts in <mods root>/PrismLib/. Derived
           from this assembly's own location rather than passed in by a mod, so two mods can
           never disagree about where the shared fonts are. */
        public static string Dir
        {
            get
            {
                try
                {
                    string home = Path.GetDirectoryName(
                        new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
                    return Path.Combine(home, "Fonts");
                }
                catch { return null; }
            }
        }

        public static string PackDir(string id)
        {
            string d = Dir;
            return d == null || string.IsNullOrEmpty(id) ? null : Path.Combine(d, id);
        }

        public static bool IsInstalled(string id)
        {
            try
            {
                string d = PackDir(id);
                return d != null && Directory.Exists(d) && Directory.GetFiles(d).Length > 0;
            }
            catch { return false; }
        }

        /// Every installed font file, for a mod that wants to register them all.
        public static string[] FontFiles()
        {
            try
            {
                string d = Dir;
                if (d == null || !Directory.Exists(d)) return new string[0];
                var found = new List<string>();
                foreach (string f in Directory.GetFiles(d, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".ttf" || ext == ".otf" || ext == ".ttc" || ext == ".otc") found.Add(f);
                }
                found.Sort(StringComparer.OrdinalIgnoreCase);
                return found.ToArray();
            }
            catch { return new string[0]; }
        }

        // ── Catalogue ──────────────────────────────────────────────────────

        public static bool Fetch(out string error)
        {
            error = null;
            try
            {
                byte[] raw = Get(Feed);
                if (raw == null) { error = "could not reach the font list"; return false; }
                Catalogue = Parse(System.Text.Encoding.UTF8.GetString(raw));
                Prism.Log("PrismLib: font catalogue has " + Catalogue.Length + " pack(s)");
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        /* Hand-rolled, like the PrismLib feed's parser: a JSON dependency is the only thing that
           would stop this assembly being plain BCL. The shape is flat, so splitting the packs
           array on braces and reading string fields out of each object is enough. */
        private static Pack[] Parse(string json)
        {
            var packs = new List<Pack>();
            int arr = json.IndexOf("\"packs\"", StringComparison.OrdinalIgnoreCase);
            if (arr < 0) return packs.ToArray();

            foreach (Match m in Regex.Matches(json.Substring(arr), "\\{[^{}]*\\}"))
            {
                string o = m.Value;
                string id = Field(o, "id"), url = Field(o, "url");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url)) continue;
                packs.Add(new Pack
                {
                    Id = id,
                    Name = Field(o, "name") ?? id,
                    Note = Field(o, "note") ?? "",
                    Size = Field(o, "size") ?? "",
                    Url = url,
                    Sha256 = Field(o, "sha256"),
                    Preview = Field(o, "preview"),
                });
            }
            return packs.ToArray();
        }

        private static string Field(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        // ── Install / remove ───────────────────────────────────────────────

        public static bool Install(Pack pack, out string error)
        {
            error = null;
            if (pack == null || string.IsNullOrEmpty(pack.Url)) { error = "no pack"; return false; }
            string dest = PackDir(pack.Id);
            if (dest == null) { error = "no install directory"; return false; }

            string tmp = null;
            try
            {
                byte[] zip = Get(pack.Url);
                if (zip == null) { error = "download failed"; return false; }

                /* Only when the feed supplies one. A hash is worth having — this writes files a
                   mod then loads — but a pack added without one should still install rather than
                   be silently unavailable. */
                if (!string.IsNullOrEmpty(pack.Sha256) && !HashMatches(zip, pack.Sha256))
                {
                    error = "checksum mismatch";
                    Prism.Log("PrismLib: font pack '" + pack.Id + "' failed its checksum");
                    return false;
                }

                tmp = Path.Combine(Path.GetTempPath(), "prism-font-" + pack.Id + ".zip");
                File.WriteAllBytes(tmp, zip);

                Directory.CreateDirectory(dest);
                using (var archive = ZipFile.OpenRead(tmp))
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;   // directory
                        string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                        if (ext != ".ttf" && ext != ".otf" && ext != ".ttc" && ext != ".otc"
                            && ext != ".txt" && ext != ".md") continue;
                        /* entry.Name, never FullName: a pack is a flat set of font files whatever
                           shape it was zipped in, and flattening is also what stops a "../.."
                           entry escaping the install directory. */
                        entry.ExtractToFile(Path.Combine(dest, entry.Name), true);
                    }

                Prism.Log("PrismLib: font pack '" + pack.Id + "' installed to " + dest);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Prism.Log("PrismLib: font pack '" + pack.Id + "' install failed: " + e.Message);
                return false;
            }
            finally
            {
                if (tmp != null) { try { File.Delete(tmp); } catch { } }
            }
        }

        public static bool Remove(string id, out string error)
        {
            error = null;
            try
            {
                string d = PackDir(id);
                if (d != null && Directory.Exists(d)) Directory.Delete(d, true);
                Prism.Log("PrismLib: font pack '" + id + "' removed");
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// A pack's preview image, cached beside the packs so it is fetched once.
        public static string PreviewFile(Pack pack)
        {
            if (pack == null || string.IsNullOrEmpty(pack.Preview)) return null;
            try
            {
                string d = Dir;
                if (d == null) return null;
                string cache = Path.Combine(d, "previews");
                Directory.CreateDirectory(cache);
                string file = Path.Combine(cache, pack.Id + ".png");
                if (File.Exists(file)) return file;

                byte[] png = Get(pack.Preview);
                if (png == null || png.Length == 0) return null;
                File.WriteAllBytes(file, png);
                return file;
            }
            catch { return null; }
        }

        // ── Net ────────────────────────────────────────────────────────────

        private static bool HashMatches(byte[] data, string expected)
        {
            using (var sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""),
                    expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /* Same shape as the bootstrapper's fetch, and for the same reason: Mono's default
           protocol list predates GitHub dropping everything below TLS 1.2, and an unset timeout
           on a stalled socket hangs whatever thread the caller put this on. */
        private static byte[] Get(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = req.ReadWriteTimeout = NetTimeoutMs;
                req.UserAgent = "PrismLib";
                using (var res = req.GetResponse())
                using (var s = res.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    var buf = new byte[16384];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Prism.Log("PrismLib: fetch failed (" + url + "): " + e.Message);
                return null;
            }
        }
    }
}
