using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Hd2Qd.AssetCheck
{
    public static partial class Plugin
    {
        static Window open;

        /// <summary>The plugin's window, beside the manager's (owner). host, as the manager gives
        /// every window plugin: "home" (its settings folder: the game table's cache), "data" (the
        /// game's data folder), "lang" (the language tag), "mods" (a Func giving the enabled mods in
        /// load order, as Estimate takes them, each time the window reads them). One window at a
        /// time: opening again brings it forward and reads again.</summary>
        public static void Open(Window owner, Dictionary<string, object> host)
        {
            var mods = (Func<List<Dictionary<string, object>>>)host["mods"];
            string home = (string)host["home"], data = (string)host["data"];
            var lang = Words.Lang(host);
            if (open != null) { open.Activate(); ((View)open.Tag).Read(); return; }
            var v = new View(mods, home, data, lang);
            // the manager makes the window, so its caption matches the main one; a plain window
            // (the system's caption) when it did not hand one over (an older manager)
            Window w;
            if (host.TryGetValue("window", out var mk) && mk is Func<string, FrameworkElement, Window> make) w = make(Title(lang), v.Root);
            else w = new Window
            {
                Title = Title(lang) + " · HD2 Mod Quick Deploy", Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = v.B("Ground900"), Foreground = v.B("Ink"), Content = v.Root, Icon = owner?.Icon,
                FontFamily = owner?.FontFamily ?? SystemFonts.MessageFontFamily,
            };
            w.Width = 1120; w.Height = 820; w.MinWidth = 760; w.MinHeight = 480;
            w.Tag = v;
            w.KeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.Closed += (s, e) => open = null;
            open = w;
            w.Show();
            v.Read();
        }

        /// <summary>The window's content: a summary, then one line per mod, the ones to check
        /// first; a click on a line shows its findings and its biggest resources.</summary>
        sealed class View
        {
            readonly Func<List<Dictionary<string, object>>> mods;
            readonly string home, data;
            readonly string lang;
            public readonly DockPanel Root = new DockPanel { Margin = new Thickness(28, 24, 28, 24) };
            readonly StackPanel head = new StackPanel();
            readonly StackPanel list = new StackPanel();
            readonly HashSet<string> opened = new HashSet<string>();
            bool reading;

            public View(Func<List<Dictionary<string, object>>> mods, string home, string data, string lang)
            {
                this.mods = mods; this.home = home; this.data = data; this.lang = lang;
                DockPanel.SetDock(head, Dock.Top);
                Root.Children.Add(head);
                Root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) });
            }

            public Brush B(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
            Style S(string key) => Application.Current?.TryFindResource(key) as Style;
            /// <summary>A text in the window's language (Lang\&lt;tag&gt;.json); with arguments, a
            /// first argument of 1 picks the key's _1 form where the language has one.</summary>
            string W(string key) => Words.Get(lang, key);
            // an archive category in the window's language when it has one ("Armor" -> "护甲")
            string Category(string c)
            {
                var key = "cat_" + System.Text.RegularExpressions.Regex.Replace(c ?? "", "[^A-Za-z0-9]+", "_").Trim('_');
                var w = W(key);
                return w == key ? c : w;
            }
            string F(string key, params object[] args) => Words.F(lang, key, args);

            public async void Read()
            {
                if (reading) return;
                reading = true;
                Head(W("reading"));
                var bar = new ProgressBar { Height = 6, Maximum = 1, Margin = new Thickness(0, 12, 0, 0), Foreground = B("Yellow"), Background = B("Grid"), BorderThickness = new Thickness(0) };
                head.Children.Add(bar);
                list.Children.Clear();
                Dictionary<string, object> r;
                try
                {
                    var given = mods();
                    var progress = new Progress<double>(p => bar.Value = p);
                    r = await Task.Run(() => Estimate(home, data, given, p => ((IProgress<double>)progress).Report(p)));
                }
                catch (Exception e)
                {
                    Head(F("check_failed", e.Message));
                    reading = false;
                    return;
                }
                reading = false;
                Show(r);
            }

            void Head(string line)
            {
                head.Children.Clear();
                // the caption carries the title; the Read-again button (right), the intro and the
                // status line open the body
                var top = new DockPanel();
                var again = new Button { Content = W("check_again"), Style = S("Ghost"), MinHeight = 36, Padding = new Thickness(16, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top };
                again.Click += (s, e) => Read();
                DockPanel.SetDock(again, Dock.Right);
                top.Children.Add(again);
                top.Children.Add(Small(W("intro"), new Thickness(0, 4, 16, 0)));
                head.Children.Add(top);
                head.Children.Add(new TextBlock { Text = line, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap });
            }

            TextBlock Small(string text, Thickness margin) =>
                new TextBlock { Text = text, FontSize = 12, LineHeight = 16, Foreground = B("Muted"), TextWrapping = TextWrapping.Wrap, Margin = margin };

            static string Mb(long bytes)
            {
                double mb = bytes / 1048576.0;
                if (Math.Abs(mb) >= 1024) return (mb / 1024).ToString("0.00") + " GB";
                if (Math.Abs(mb) >= 1) return mb.ToString(Math.Abs(mb) >= 100 ? "0" : "0.0") + " MB";
                return Math.Round(bytes / 1024.0).ToString("0") + " KB";
            }

            static string N(long n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            string Extra(long bytes) => bytes == 0 ? W("same")
                : bytes > 0 ? "+" + Mb(bytes) : "−" + Mb(-bytes);

            static long L(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is long n ? n : 0;
            static string Str(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as string : null;
            static List<Dictionary<string, object>> Items(Dictionary<string, object> m) => ((List<object>)m["items"]).Cast<Dictionary<string, object>>().ToList();
            static bool Has(Dictionary<string, object> it, string flag) => it.TryGetValue("flags", out var f) && f is List<object> l && l.Contains(flag);
            static List<long> Tris(Dictionary<string, object> it, string chain, string side) =>
                it.TryGetValue("facts", out var f) && f is Dictionary<string, object> facts && facts.TryGetValue(chain, out var c) && c is Dictionary<string, object> cd
                && cd.TryGetValue(side, out var l) && l is List<object> list ? list.Cast<long>().ToList() : new List<long>();

            static int Rank(string level) => level == "many" ? 0 : level == "waste" ? 1 : 2;

            void Show(Dictionary<string, object> r)
            {
                var rows = ((List<object>)r["mods"]).Cast<Dictionary<string, object>>().ToList();
                var shown = rows.Where(m => L(m, "textures") + L(m, "models") > 0)
                    .OrderBy(m => Rank(Str(m, "level"))).ThenByDescending(m => L(m, "extra")).ThenBy(m => Str(m, "name")).ToList();
                int check = shown.Count(m => Str(m, "level") == "many"), waste = shown.Count(m => Str(m, "level") == "waste");
                Head(shown.Count == 0
                    ? W("head_none")
                    : F("head_counts", shown.Count, check, waste, shown.Count - check - waste));
                list.Children.Clear();
                if (shown.Count == 0) return;
                // pictures from outside: the first mod's details open
                if (opened.Count == 0 && Environment.GetEnvironmentVariable("HD2QD_VRAM_OPEN_FIRST") == "1") opened.Add(Str(shown[0], "name"));
                list.Children.Add(Row(null, true));
                foreach (var m in shown)
                {
                    list.Children.Add(Row(m, false));
                    if (opened.Contains(Str(m, "name"))) list.Children.Add(Details(m));
                }
                int none = rows.Count - shown.Count;
                if (none > 0) list.Children.Add(Small(F("others_none", none), new Thickness(0, 12, 0, 0)));
            }

            static readonly GridLength[] Columns =
                { new GridLength(1.1, GridUnitType.Star), new GridLength(110), new GridLength(1, GridUnitType.Star), new GridLength(130), new GridLength(120) };

            (string Text, string Colour) Badge(string level) =>
                level == "many" ? (W("level_many"), "Red") : level == "waste" ? (W("level_waste"), "Yellow") : (W("level_fine"), "Blue");

            FrameworkElement Row(Dictionary<string, object> m, bool header)
            {
                var g = new Grid { MinHeight = header ? 28 : 44 };
                foreach (var c in Columns) g.ColumnDefinitions.Add(new ColumnDefinition { Width = c });
                void Cell(int col, UIElement e) { Grid.SetColumn(e, col); g.Children.Add(e); }
                TextBlock Text(string s, int col, bool right = false) => new TextBlock
                {
                    Text = s, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = header ? 12 : 14, Foreground = header ? B("Muted") : B("Ink"),
                    TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, Margin = new Thickness(col == 0 ? 12 : 0, 0, 12, 0),
                };
                if (header)
                {
                    Cell(0, Text(W("col_mod"), 0)); Cell(1, Text(W("col_status"), 1)); Cell(2, Text(W("col_finding"), 2));
                    Cell(3, Text(W("col_vram"), 3, true)); Cell(4, Text(W("col_counts"), 4, true));
                }
                else
                {
                    var name = Text(Str(m, "name"), 0);
                    if (L(m, "lost") > 0) name.ToolTip = F("lost_tip", L(m, "lost"));
                    Cell(0, name);
                    var (bt, bc) = Badge(Str(m, "level"));
                    Cell(1, new Border
                    {
                        BorderBrush = B(bc), BorderThickness = new Thickness(1), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = bt, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B(bc) },
                    });
                    var f = Findings(m);
                    Cell(2, Text(f.Count > 0 ? f[0].Title : W("no_problems"), 2));
                    var ex = Text(Extra(L(m, "extra")), 3, true);
                    ex.ToolTip = F("vram_tip", Mb(L(m, "max")), Mb(L(m, "game_max")));
                    Cell(3, ex);
                    Cell(4, Text(L(m, "models") + " · " + L(m, "textures"), 4, true));
                }
                var line = new Border { Child = g, BorderBrush = B("Grid"), BorderThickness = new Thickness(0, 0, 0, 1), Background = Brushes.Transparent };
                if (!header)
                {
                    line.Cursor = Cursors.Hand;
                    var key = Str(m, "name");
                    line.MouseEnter += (s, e) => line.Background = B("Band");
                    line.MouseLeave += (s, e) => line.Background = Brushes.Transparent;
                    line.MouseLeftButtonUp += (s, e) =>
                    {
                        int at = list.Children.IndexOf(line);
                        if (opened.Remove(key)) list.Children.RemoveAt(at + 1);
                        else { opened.Add(key); list.Children.Insert(at + 1, Details(m)); }
                    };
                }
                return line;
            }

            // a resource by its name in the name list ("Reflex Sight"); for a path, the game's own
            // (the list has some cut short: "mple_pouch/sample_pouch"); else its number
            string Name(Dictionary<string, object> it)
            {
                var listed = Str(it, "name");
                // a real name first; else the game items it belongs to ("护甲：SR-24 Street Scout"), by
                // the game archives that hold it (the user, 2026-11-03: a character mod's models had none)
                var items = (it.TryGetValue("archives", out var av) ? av as List<object> : null)?.OfType<Dictionary<string, object>>().ToList();
                // "Helmet O-3 Free Spirit" names the same as the game item, whose category is in the
                // window's language: the item then
                if (listed != null && !listed.Contains("/") && (items == null || !items.Any(a => listed.IndexOf(Str(a, "name") ?? "\u0001", StringComparison.OrdinalIgnoreCase) >= 0)))
                    return listed;
                if (items != null && items.Count > 0)
                    return string.Join(W("sep_clause"), items.GroupBy(a => Str(a, "category"))
                        .Select(g => F("archive_named", Category(g.Key), string.Join(" / ", g.Select(a => Str(a, "name"))))));
                var p = listed != null && Str(it, "path") == null ? listed : Str(it, "path") ?? listed;
                return p != null ? System.Text.RegularExpressions.Regex.Replace(p, "^content/", "") : F("model_id", Str(it, "id"));
            }

            /// <summary>What a mod's findings mean, worst first (asset_check_src page.html's words):
            /// (level "red"/"amber"/"minor", title, text).</summary>
            List<(string Level, string Title, string Text)> Findings(Dictionary<string, object> m)
            {
                var items = Items(m);
                var models = items.Where(it => Str(it, "type") == "model" && Str(it, "error") == null).ToList();
                var red = new List<(string, string, string)>();
                var amber = new List<(string, string, string)>();
                var minor = new List<(string, string, string)>();
                var many = models.Where(it => Str(it, "many") != null && (Has(it, "lod_flat") || Has(it, "tris_heavy") || Has(it, "shadow_heavy"))).ToList();
                string Game(List<long> game, Func<List<long>, long> pick) => game.Count > 0 ? F("game_paren", N(pick(game))) : "";
                foreach (var it in many)
                {
                    List<long> g = Tris(it, "game", "mod"), gg = Tris(it, "game", "game"), s = Tris(it, "shadow", "mod"), sg = Tris(it, "shadow", "game");
                    bool enemy = Str(it, "many") == "enemy";
                    red.Add(("red", W(enemy ? "many_enemy" : "many_object"),
                        F("many_text", Name(it), N(g[0]), Game(gg, l => l[0]),
                          g.Count > 1 ? F("many_far", N(g.Last()), Game(gg, l => l.Last())) : "",
                          s.Count > 0 ? F("many_shadow", N(s[0]), Game(sg, l => l[0])) : "")));
                }
                // a finding's title is its flag's key, its text the key with _text
                void Group(string flag, string level, Func<List<Dictionary<string, object>>, string> text)
                {
                    var its = items.Where(it => Has(it, flag) && !many.Contains(it)).ToList();
                    if (its.Count == 0) return;
                    var f = (level, its.Count > 1 ? F("titled_count", W(flag), its.Count) : W(flag), text(its));
                    (level == "red" ? red : level == "amber" ? amber : minor).Add(f);
                }
                Dictionary<string, object> Worst(List<Dictionary<string, object>> its, Func<Dictionary<string, object>, double> f) => its.OrderByDescending(f).First();
                Group("lod_flat", "amber", its =>
                {
                    var w = Worst(its, it => Tris(it, "game", "mod").DefaultIfEmpty(0).Last());
                    List<long> g = Tris(w, "game", "mod"), gg = Tris(w, "game", "game");
                    return F("lod_flat_text", Name(w), N(g.Last()), Game(gg, l => l.Last()));
                });
                Group("shadow_heavy", "amber", its =>
                {
                    var w = Worst(its, it => Tris(it, "shadow", "mod").DefaultIfEmpty(0).First());
                    List<long> s = Tris(w, "shadow", "mod"), sg = Tris(w, "shadow", "game");
                    return F("shadow_heavy_text", Name(w), N(s[0]), Game(sg, l => l[0]));
                });
                Group("tris_heavy", "amber", its =>
                {
                    var w = Worst(its, it => Tris(it, "game", "mod").DefaultIfEmpty(0).First());
                    List<long> g = Tris(w, "game", "mod"), gg = Tris(w, "game", "game");
                    return F("tris_heavy_text", Name(w), N(g[0]), N(gg.DefaultIfEmpty(0).First()));
                });
                Group("lod_missing", "amber", its => W("lod_missing_text"));
                Group("aabb_inflated", "amber", its =>
                {
                    double over = its.Max(it => it.TryGetValue("facts", out var f) && f is Dictionary<string, object> d && d.TryGetValue("aabb_over", out var o) && o is double x ? x : 0);
                    return F("aabb_inflated_text", over.ToString("0.0"));
                });
                Group("light_shadow", "amber", its => W("light_shadow_text"));
                Group("light_volumetric", "amber", its => W("light_volumetric_text"));
                Group("tex_no_mips", "amber", its => F("tex_no_mips_text", its.Count));
                Group("tex_uncompressed", "amber", its => F("tex_uncompressed_text", Mb(its.Sum(it => L(it, "max")))));
                Group("tex_not_streamed", "amber", its => F("tex_not_streamed_text", Mb(its.Sum(it => L(it, "max")))));
                Group("tex_big_resident", "amber", its =>
                {
                    var w = Worst(its, it => L(it, "w") * L(it, "h"));
                    return F("tex_big_resident_text", its.Count, L(w, "w"), L(w, "h"), Mb(its.Sum(it => L(it, "max"))));
                });
                long mem = L(m, "max");
                if (mem >= 300L * 1000 * 1000)
                    amber.Add(("amber", W("high_vram"),
                        F("high_vram_text", Mb(mem), L(m, "game_max") > 0 ? F("game_paren", Mb(L(m, "game_max"))) : "")));
                Group("tex_bigger", "minor", its => F("tex_bigger_text", its.Count));
                Group("sections_added", "minor", its => F("sections_added_text", its.Count));
                Group("stride_wider", "minor", its => F("stride_wider_text", its.Count));
                return red.Concat(amber).Concat(minor).ToList();
            }

            /// <summary>A mod's findings, then its biggest resources.</summary>
            FrameworkElement Details(Dictionary<string, object> m)
            {
                var box = new StackPanel { Margin = new Thickness(28, 8, 12, 16) };
                var findings = Findings(m);
                if (findings.Count == 0)
                    box.Children.Add(Small(W("comparable"), new Thickness(0, 0, 0, 8)));
                foreach (var (level, title, text) in findings.Where(f => f.Level != "minor"))
                {
                    var line = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                    var bar = new Border { Width = 3, Background = B(level == "red" ? "Red" : "Yellow"), Margin = new Thickness(0, 2, 10, 2) };
                    DockPanel.SetDock(bar, Dock.Left);
                    line.Children.Add(bar);
                    var words = new StackPanel();
                    words.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                    words.Children.Add(Small(text, new Thickness(0, 2, 0, 0)));
                    line.Children.Add(words);
                    box.Children.Add(line);
                }
                var minor = findings.Where(f => f.Level == "minor").Select(f => f.Title).ToList();
                if (minor.Count > 0) box.Children.Add(Small(F("minor", string.Join(" · ", minor)), new Thickness(0, 0, 0, 10)));

                var items = Items(m);
                box.Children.Add(new TextBlock { Text = W("largest"), FontSize = 12, Foreground = B("Muted"), Margin = new Thickness(0, 6, 0, 4) });
                foreach (var it in items.Take(20))
                {
                    bool tex = Str(it, "type") == "texture";
                    string what;
                    bool named = Str(it, "name") != null || (it.TryGetValue("archives", out var av) && av is List<object> al && al.Count > 0);
                    if (tex) what = W("texture") + (named ? " " + Name(it) : "")
                                    + (L(it, "w") > 0 ? " " + L(it, "w") + "×" + L(it, "h") + Layers(L(it, "layers")) + " " + Str(it, "format") : "");
                    else
                    {
                        var g = Tris(it, "game", "mod");
                        what = g.Count > 0 ? F("model_tris", Name(it), N(g[0])) : Name(it);
                    }
                    bool inGame = it.TryGetValue("in_game", out var ig) && ig is bool b && b;
                    string game = !inGame ? W("new")
                        : F("game_line", (tex && L(it, "game_w") > 0 ? L(it, "game_w") + "×" + L(it, "game_h") + Layers(L(it, "game_layers")) + " · " : "")
                          + (!tex && Tris(it, "game", "game").Count > 0 ? F("game_tris", N(Tris(it, "game", "game")[0])) : "")
                          + Mb(L(it, "game_max")));
                    var line = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                    var size = new TextBlock { Text = Mb(L(it, "max")), Foreground = B("Ink"), FontSize = 13, MinWidth = 90, TextAlignment = TextAlignment.Right };
                    DockPanel.SetDock(size, Dock.Right);
                    line.Children.Add(size);
                    line.Children.Add(new TextBlock
                    {
                        FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis,
                        Inlines = { new Run(what) { Foreground = B("Ink") }, new Run("  ·  " + game) { Foreground = B("Muted") } },
                    });
                    box.Children.Add(line);
                }
                if (items.Count > 20) box.Children.Add(Small(F("more_not_shown", items.Count - 20), new Thickness(0, 4, 0, 0)));
                return box;
            }

            string Layers(long n) => n > 1 ? F("layers", n) : "";
        }
    }
}
