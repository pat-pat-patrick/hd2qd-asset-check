using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hd2Qd.AssetCheck;
using Xunit;

namespace Hd2Qd.AssetCheck.Tests
{
    /// <summary>The VRAM estimate plugin (plugins\Vram) through the manager: a game data folder
    /// made here (bundles.nxa and bundles.00.nxa, one chunk LZ4, one stored), mods in the library,
    /// and the estimate the `vram` command and the plugin's window show. Checked against the real
    /// game on 2026-11-02: its table of 17232 textures and 8019 models equals Mod Asset Check's
    /// (make_table.js), every size and format.</summary>
    public class AssetCheck : IDisposable
    {
        readonly string Root = Path.Combine(Path.GetTempPath(), "hd2qd_ac_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        readonly string Home, Data;
        // the enabled mods in load order, as the manager hands them: {name, archives}
        readonly List<Dictionary<string, object>> mods = new List<Dictionary<string, object>>();
        const string A = "9ba626afa44a3aa3";

        public AssetCheck()
        {
            Home = Path.Combine(Root, "home");
            Data = Path.Combine(Root, "game", "data");
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(Data);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        const ulong Tex = 0xCD4238C6A0C69E32, Unit = 0xE0A48D0BE9A7453F, Material = 0xEAC0B497876ADEDF;
        const ulong T1 = 0x1111, T2 = 0x2222, U1 = 0x3333, N1 = 0x4444, M1 = 0x5555;

        /// <summary>One resource of a GPU archive: its type and id, its resident and streamed GPU
        /// bytes, and for a texture its size, DXGI format, array size and whether it is a cube.</summary>
        struct G
        {
            public ulong Type, Id;
            public uint Res, Str;
            public int W, H, Fmt, Arr, Mips;
            public bool Cube;
        }

        static G Texture(ulong id, uint res, uint str, int w, int h, int fmt, int arr = 1, bool cube = false, int mips = 10) =>
            new G { Type = Tex, Id = id, Res = res, Str = str, W = w, H = h, Fmt = fmt, Arr = arr, Cube = cube, Mips = mips };

        static G Model(ulong id, uint res) => new G { Type = Unit, Id = id, Res = res };

        /// <summary>A patch archive whose rows carry GPU sizes; a texture's main bytes are its
        /// header (3 u32, 15 mip records of 12 bytes) and a DDS header with its DX10 part.</summary>
        static byte[] GpuArchive(params G[] rs)
        {
            var b = new List<byte>();
            var head = new byte[0x48];
            BitConverter.GetBytes(0xF0000011u).CopyTo(head, 0);
            BitConverter.GetBytes(1u).CopyTo(head, 4);
            BitConverter.GetBytes((uint)rs.Length).CopyTo(head, 8);
            b.AddRange(head);
            b.AddRange(new byte[32]);
            var bodies = rs.Select(r =>
            {
                if (r.Type != Tex) return new byte[16];
                var m = new byte[192 + 148];
                Encoding.ASCII.GetBytes("DDS ").CopyTo(m, 192);
                BitConverter.GetBytes((uint)r.H).CopyTo(m, 192 + 12);
                BitConverter.GetBytes((uint)r.W).CopyTo(m, 192 + 16);
                BitConverter.GetBytes((uint)r.Mips).CopyTo(m, 192 + 28);
                Encoding.ASCII.GetBytes("DX10").CopyTo(m, 192 + 84);
                BitConverter.GetBytes((uint)r.Fmt).CopyTo(m, 192 + 128);
                BitConverter.GetBytes(r.Cube ? 4u : 0u).CopyTo(m, 192 + 136);
                BitConverter.GetBytes((uint)r.Arr).CopyTo(m, 192 + 140);
                return m;
            }).ToList();
            ulong at = (ulong)(0x48 + 32 + 80 * rs.Length);
            for (int i = 0; i < rs.Length; i++)
            {
                var row = new byte[80];
                BitConverter.GetBytes(rs[i].Id).CopyTo(row, 0);
                BitConverter.GetBytes(rs[i].Type).CopyTo(row, 8);
                BitConverter.GetBytes(at).CopyTo(row, 16);
                BitConverter.GetBytes((uint)bodies[i].Length).CopyTo(row, 56);
                BitConverter.GetBytes(rs[i].Str).CopyTo(row, 60);
                BitConverter.GetBytes(rs[i].Res).CopyTo(row, 64);
                b.AddRange(row);
                at += (ulong)bodies[i].Length;
            }
            foreach (var x in bodies) b.AddRange(x);
            return b.ToArray();
        }

        /// <summary>An LZ4 block of literals only (what a decoder must take as a block).</summary>
        static byte[] Literals(byte[] data)
        {
            var o = new List<byte>();
            int n = data.Length;
            o.Add((byte)(Math.Min(n, 15) << 4));
            if (n >= 15) { int left = n - 15; while (left >= 255) { o.Add(255); left -= 255; } o.Add((byte)left); }
            o.AddRange(data);
            return o.ToArray();
        }

        /// <summary>A DSAR file of chunks: (unpacked bytes, stored LZ4 or as they are).</summary>
        static byte[] Dsar(params (byte[] Data, bool Lz4)[] chunks)
        {
            var stored = chunks.Select(c => c.Lz4 ? Literals(c.Data) : c.Data).ToList();
            var b = new byte[0x20 + 0x20 * chunks.Length];
            Encoding.ASCII.GetBytes("DSAR").CopyTo(b, 0);
            BitConverter.GetBytes((uint)chunks.Length).CopyTo(b, 8);
            ulong uo = 0, co = (ulong)b.Length;
            for (int i = 0; i < chunks.Length; i++)
            {
                int o = 0x20 + i * 0x20;
                BitConverter.GetBytes(uo).CopyTo(b, o);
                BitConverter.GetBytes(co).CopyTo(b, o + 8);
                BitConverter.GetBytes((uint)chunks[i].Data.Length).CopyTo(b, o + 16);
                BitConverter.GetBytes((uint)stored[i].Length).CopyTo(b, o + 20);
                b[o + 24] = (byte)(chunks[i].Lz4 ? 3 : 2);
                uo += (ulong)chunks[i].Data.Length;
                co += (ulong)stored[i].Length;
            }
            return b.Concat(stored.SelectMany(s => s)).ToArray();
        }

        /// <summary>The game's data folder with one package, the archive of the given resources:
        /// bundles.nxa names it and where it sits in bundles.00.nxa, whose first chunk is LZ4.</summary>
        void GameData(params G[] rs)
        {
            var package = GpuArchive(rs);
            int cut = package.Length / 3;
            File.WriteAllBytes(Path.Combine(Data, "bundles.00.nxa"),
                Dsar((package.Take(cut).ToArray(), true), (package.Skip(cut).ToArray(), false)));
            var bc = new byte[0x18 + 0x18 + 64 + 0x10];
            BitConverter.GetBytes(1u).CopyTo(bc, 0x0C);
            BitConverter.GetBytes(1u).CopyTo(bc, 0x10);
            int noff = 0x30, ioff = 0x30 + 32;
            BitConverter.GetBytes((ulong)package.Length).CopyTo(bc, 0x18);
            BitConverter.GetBytes((uint)noff).CopyTo(bc, 0x18 + 8);
            BitConverter.GetBytes(1u).CopyTo(bc, 0x18 + 12);
            BitConverter.GetBytes((uint)ioff).CopyTo(bc, 0x18 + 16);
            Encoding.ASCII.GetBytes("9ba626afa44a3aa3").CopyTo(bc, noff);
            BitConverter.GetBytes(0UL).CopyTo(bc, ioff);         // at 0 of the package
            BitConverter.GetBytes(0u).CopyTo(bc, ioff + 8);       // at 0 of the bundle
            bc[ioff + 0x0f] = 0;                                  // bundles.00.nxa
            var nxa = Path.Combine(Data, "bundles.nxa");
            File.WriteAllBytes(nxa, Dsar((bc, false)));
            File.SetLastWriteTimeUtc(nxa, DateTime.UtcNow.AddSeconds(stamp++));
        }

        int stamp;

        static readonly G[] Vanilla =
        {
            Texture(T1, 1000, 3000, 512, 256, 98),
            Texture(T2, 600, 0, 128, 128, 28, 1, true),
            Model(U1, 500),
            new G { Type = Material, Id = M1, Res = 99 },
        };

        /// <summary>A mod with one patch archive (and its empty companions), enabled last.</summary>
        string AddMod(string name, params G[] rs)
        {
            var folder = Path.Combine(Root, "mods", name);
            Directory.CreateDirectory(folder);
            var archive = Path.Combine(folder, A + ".patch_0");
            File.WriteAllBytes(archive, GpuArchive(rs));
            foreach (var ext in new[] { ".gpu_resources", ".stream" }) File.WriteAllBytes(archive + ext, new byte[0]);
            mods.Add(new Dictionary<string, object> { ["name"] = name, ["archives"] = new List<object> { archive } });
            return folder;
        }

        Dictionary<string, object> Host() => new Dictionary<string, object>
        {
            ["home"] = Home, ["data"] = Data, ["zh"] = false,
            ["mods"] = (Func<List<Dictionary<string, object>>>)(() => mods),
        };

        /// <summary>What the plugin's command prints.</summary>
        string Command(params string[] args)
        {
            var o = new StringWriter();
            Assert.Equal(0, Plugin.Run(args[0], args.Skip(1).ToArray(), o, Host()));
            return o.ToString();
        }

        static class Json
        {
            public static List<object> List(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
            public static long Long(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is long n ? n : 0;
            public static string Str(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as string : null;
            public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        }

        static Dictionary<string, object> ModOf(Dictionary<string, object> r, string name) =>
            Json.List(r, "mods").Cast<Dictionary<string, object>>().Single(m => Json.Str(m, "name") == name);

        Dictionary<string, object> Estimate() => Plugin.Estimate(Home, Data, mods, null);

        /// <summary>What the manager reads of it: a window plugin of Api 1, its names and its command.</summary>
        [Fact]
        public void The_plugin_says_its_kind_names_and_command()
        {
            Assert.Equal(1, Plugin.Api);
            Assert.Equal("window", Plugin.Kind);
            Assert.Equal("资源检查", Plugin.Title(true));
            Assert.Equal("Asset check", Plugin.Title(false));
            Assert.NotNull(typeof(Plugin).GetMethod("Open"));
            Assert.Equal("asset-check", Plugin.Commands[0][0]);
        }

        [Fact]
        public void An_LZ4_back_reference_repeats_what_came_before()
        {
            var lz4 = typeof(Plugin).Assembly.GetType("Hd2Qd.AssetCheck.Lz4", true).GetMethod("Decode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // "abcd", then 8 bytes from 4 back (overlapping: it copies what it writes), then "x"
            var block = new byte[] { 0x44, (byte)'a', (byte)'b', (byte)'c', (byte)'d', 4, 0, 0x10, (byte)'x' };
            Assert.Equal("abcdabcdabcdx", Encoding.ASCII.GetString((byte[])lz4.Invoke(null, new object[] { block, 13 })));
            var e = Assert.Throws<TargetInvocationException>(() => lz4.Invoke(null, new object[] { block, 14 }));
            Assert.IsType<InvalidDataException>(e.InnerException);
        }

        [Fact]
        public void The_game_table_reads_textures_and_models_and_is_kept_until_the_game_changes()
        {
            GameData(Vanilla);
            AddMod("Probe", Texture(T1, 1, 0, 1, 1, 28));
            var probe = ModOf(Estimate(), "Probe");
            var t1 = Json.List(probe, "items").Cast<Dictionary<string, object>>().Single();
            Assert.Equal(4000L, Json.Long(t1, "game_max"));
            Assert.Equal(512L, Json.Long(t1, "game_w"));
            Assert.Equal(256L, Json.Long(t1, "game_h"));
            Assert.Equal("BC7", Json.Str(t1, "game_format"));
            Assert.True(File.Exists(Path.Combine(Home, "vram_game_table.bin")));

            // the cache answers while bundles.nxa is the same: the bundle itself is not read
            File.Delete(Path.Combine(Data, "bundles.00.nxa"));
            Assert.Equal(4000L, Json.Long(ModOf(Estimate(), "Probe"), "game_max"));

            // a game patch (bundles.nxa changes): read again
            GameData(Texture(T1, 2000, 3000, 1024, 512, 98), Model(U1, 500));
            t1 = Json.List(ModOf(Estimate(), "Probe"), "items").Cast<Dictionary<string, object>>().Single();
            Assert.Equal(5000L, Json.Long(t1, "game_max"));
            Assert.Equal(1024L, Json.Long(t1, "game_w"));
        }

        [Fact]
        public void A_cube_texture_counts_its_six_faces()
        {
            GameData(Vanilla);
            AddMod("Sky", Texture(T2, 900, 0, 256, 256, 28, 1, true), Texture(N1, 50, 0, 64, 64, 28, 2));
            var items = Json.List(ModOf(Estimate(), "Sky"), "items").Cast<Dictionary<string, object>>().ToList();
            var cube = items.Single(i => Json.Str(i, "id") == T2.ToString("x16"));
            Assert.Equal(6L, Json.Long(cube, "layers"));
            Assert.Equal(6L, Json.Long(cube, "game_layers"));
            Assert.Equal(2L, Json.Long(items.Single(i => Json.Str(i, "id") == N1.ToString("x16")), "layers"));
        }

        [Fact]
        public void Each_mod_counts_what_it_adds_and_a_later_mod_takes_what_it_overrides()
        {
            GameData(Vanilla);
            AddMod("First", Texture(T1, 5000, 0, 1024, 1024, 28), Texture(N1, 700, 0, 256, 256, 28));
            AddMod("Second", Texture(T1, 6000, 0, 1024, 1024, 28), Model(U1, 300));
            AddMod("Scripts only", new G { Type = Material, Id = M1, Res = 10 });
            var r = Estimate();

            var first = ModOf(r, "First");
            Assert.Equal(1L, Json.Long(first, "lost"));            // T1 loses to Second
            Assert.Equal(1L, Json.Long(first, "textures"));
            Assert.Equal(1L, Json.Long(first, "new"));
            Assert.Equal(700L, Json.Long(first, "max"));
            Assert.Equal(0L, Json.Long(first, "game_max"));
            Assert.Equal(700L, Json.Long(first, "extra"));

            var second = ModOf(r, "Second");
            Assert.Equal(0L, Json.Long(second, "lost"));
            Assert.Equal(6300L, Json.Long(second, "max"));
            Assert.Equal(4500L, Json.Long(second, "game_max"));    // T1 4000 + U1 500
            Assert.Equal(1800L, Json.Long(second, "extra"));
            Assert.Equal(1L, Json.Long(second, "models"));
            var items = Json.List(second, "items").Cast<Dictionary<string, object>>().ToList();
            Assert.Equal(T1.ToString("x16"), Json.Str(items[0], "id"));   // the most added first
            Assert.Equal("RGBA8", Json.Str(items[0], "format"));

            var none = ModOf(r, "Scripts only");
            Assert.Equal(0L, Json.Long(none, "textures") + Json.Long(none, "models"));

            var total = Json.Obj(r, "total");
            Assert.Equal(7000L, Json.Long(total, "max"));
            Assert.Equal(2500L, Json.Long(total, "extra"));
        }

        static List<string> FlagsOf(Dictionary<string, object> mod, ulong id) =>
            Json.List(mod, "items").Cast<Dictionary<string, object>>().Single(i => Json.Str(i, "id") == id.ToString("x16"))["flags"] is List<object> l
                ? l.Cast<string>().OrderBy(x => x).ToList() : new List<string>();

        /// <summary>The texture rules of asset_cost_scan.py (models' rules: checked against
        /// Mod Asset Check's core.js on 8 real mods, 745 resources, 0 differences, 2026-11-02).</summary>
        [Fact]
        public void Badly_built_textures_are_found_and_well_built_ones_are_not()
        {
            GameData(Vanilla);
            AddMod("Textures",
                Texture(T1, 1400000, 0, 1024, 1024, 98),                 // the game streams its copy; this one stays: not streamed, bigger
                Texture(N1, 4194304, 0, 1024, 1024, 28, mips: 1),        // new, RGBA8, one mip: uncompressed, no mipmaps
                Texture(0x6666, 22369621, 0, 4096, 4096, 98),             // new, 4096 and not streamed: very large and resident
                Texture(T2, 600, 0, 128, 128, 28, 1, true));              // as the game's: nothing
            var m = ModOf(Estimate(), "Textures");
            Assert.Equal(new[] { "tex_bigger", "tex_not_streamed" }, FlagsOf(m, T1));
            Assert.Equal(new[] { "tex_no_mips", "tex_uncompressed" }, FlagsOf(m, N1));
            Assert.Equal(new[] { "tex_big_resident" }, FlagsOf(m, 0x6666));
            Assert.Empty(FlagsOf(m, T2));
            Assert.Equal("waste", Json.Str(m, "level"));
            Assert.Equal(1L, Json.Long(Json.Obj(m, "flags"), "tex_not_streamed"));
        }

        [Fact]
        public void A_mod_with_nothing_wrong_is_fine()
        {
            GameData(Vanilla);
            AddMod("Plain", Texture(T1, 1000, 3000, 512, 256, 98));
            Assert.Equal("fine", Json.Str(ModOf(Estimate(), "Plain"), "level"));
        }

        [Fact]
        public void The_command_lists_the_mods_most_added_first()
        {
            GameData(Vanilla);
            AddMod("Small", Texture(N1, 2 * 1048576, 0, 64, 64, 28));
            AddMod("Big", Texture(T1, 30 * 1048576, 0, 4096, 4096, 28));
            var said = Command("asset-check");
            Assert.True(said.IndexOf("Big", StringComparison.Ordinal) < said.IndexOf("Small", StringComparison.Ordinal), said);
            Assert.Contains("not measured in game", said);
            Assert.Contains("+30.0 MB", said);
        }
    }
}
