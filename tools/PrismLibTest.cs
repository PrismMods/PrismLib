using System;
using System.Collections.Generic;
using PrismLib;

// Offline check for the arbitration rules. The game cannot be scripted, so this is where the
// claim/key/search logic is allowed to fail loudly.
//   mcs -r:PrismLib/bin/Release/PrismLib.dll -out:/tmp/t.exe tools/PrismLibTest.cs && mono /tmp/t.exe
static class PrismLibTest
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _fail++;
    }

    static int Main()
    {
        Prism.Log = _ => { };
        var a = Prism.Register("Sapphire", new Version(1, 0, 0));
        var b = Prism.Register("Bismuth", new Version(2, 3, 0));

        Console.WriteLine("registry");
        Check(ReferenceEquals(a, Prism.Register("Sapphire", new Version(9, 9, 9))), "re-registering returns the same handle");
        Version v;
        Check(Prism.IsLoaded("bismuth", out v) && v == new Version(2, 3, 0), "IsLoaded is case-insensitive and reports the version");
        Check(!Prism.IsLoaded("Quartz", out v) && v == null, "an absent mod reports false");

        Console.WriteLine("claims");
        string denied = null;
        Claims.Denied += (asker, key, owner) => denied = asker + "/" + key + "/" + owner;
        var c1 = a.ClaimState(StateKey.Autoplay, "toggle");
        Check(c1 != null, "a free key is granted");
        Check(b.ClaimState(StateKey.Autoplay, "trainer") == null, "a held key is denied to the second asker");
        Check(denied == "Bismuth/Autoplay/Sapphire", "the denial names asker, key and owner");
        Check(a.ClaimState(StateKey.Autoplay, "again") != null, "the owner may re-claim what it holds");
        Check(a.OwnsState(StateKey.Autoplay) && !b.OwnsState(StateKey.Autoplay), "OwnsState follows the owner");
        c1.Release();
        c1.Release();                                                   // releasing twice must not free someone else's later claim
        Check(Claims.OwnerOf(StateKey.Autoplay) == null, "release frees the key");
        var c2 = b.ClaimState(StateKey.Autoplay, "trainer");
        Check(c2 != null && Claims.OwnerOf(StateKey.Autoplay) == "Bismuth", "the key is grantable again after release");
        c1.Release();
        Check(Claims.OwnerOf(StateKey.Autoplay) == "Bismuth", "a stale Claim cannot release the new owner's key");
        using (a.ClaimState(StateKey.EditorCamera, "path preview")) { }
        Check(Claims.OwnerOf(StateKey.EditorCamera) == null, "using() releases on scope exit");
        a.ClaimState(StateKey.PathLock, "lock");
        a.ClaimState(StateKey.EditorHud, "hide");
        Claims.ReleaseAll("Sapphire");
        Check(Claims.OwnerOf(StateKey.PathLock) == null && Claims.OwnerOf(StateKey.EditorHud) == null, "ReleaseAll drops every key one mod held");
        Check(Claims.OwnerOf(StateKey.Autoplay) == "Bismuth", "ReleaseAll leaves other mods alone");

        Check(Claims.OwnerOf(StateKey.InputCapture) == null, "nobody holds the keyboard by default");
        using (b.ClaimState(StateKey.InputCapture, "panel open"))
            Check(Claims.OwnerOf(StateKey.InputCapture) == "Bismuth" && !a.OwnsState(StateKey.InputCapture),
                  "a keyboard grab is visible to the mod that must stand down");

        /* The debug window aggregates EVERY mod's sources, so a second copy would show the same
           text twice. Both mods poll the same hotkey; the claim decides which one draws. */
        var dbg = a.ClaimState(StateKey.DebugPanel, "hotkey");
        Check(dbg != null && b.ClaimState(StateKey.DebugPanel, "hotkey") == null,
              "only one mod can own the shared debug window");
        dbg.Release();
        Check(b.ClaimState(StateKey.DebugPanel, "hotkey") != null,
              "the other mod can take it once the owner is gone");
        Claims.ReleaseAll("Bismuth");

        Console.WriteLine("keys");
        Keys.KeyName = i => "K" + i;
        KeyBinding clashA = null, clashB = null;
        Keys.Conflict += (n, o) => { clashA = n; clashB = o; };
        a.BindKey("pan", 97, KeyMods.None, "Pan");
        Check(clashA == null, "the first binding is not a conflict");
        b.BindKey("auto", 97, KeyMods.None, "Autoplay");
        Check(clashA != null && clashA.Owner == "Bismuth" && clashB.Owner == "Sapphire", "an overlap reports newcomer and incumbent");
        clashA = null;
        a.BindKey("pan", 97, KeyMods.Shift, "Pan");                     // same id, rebound: replaces, no self-conflict
        Check(clashA == null, "a modifier makes it a different key");
        int n97 = 0;
        foreach (var k in Keys.Wanting(97, KeyMods.None, "Bismuth")) n97++;
        Check(n97 == 0, "the rebind left nothing of Sapphire's on plain K97");
        int pairs = 0;
        foreach (var p in Keys.Conflicts()) pairs++;
        Check(pairs == 0, "Conflicts() is empty once the overlap is rebound");
        Check(a.BindKey("edit", 117, KeyMods.Ctrl | KeyMods.Alt, "Edit").ToString() == "Sapphire:edit [Ctrl+Alt+K117]", "ToString spells the modifiers via KeyName");

        Console.WriteLine("settings");
        bool stored = false;
        a.DescribeSettings(new[] {
            new SettingEntry { Id = "editor.effectiveBpm", Kind = SettingKind.Bool, Label = "Effective BPM",
                               Page = "Features", Group = "Editor", Keywords = new[] { "tempo" },
                               Get = () => stored, Set = x => stored = (bool)x },
            new SettingEntry { Id = "editor.gridSnap", Kind = SettingKind.Float, Label = "Grid snap",
                               Page = "Features", Group = "Editor", Order = 1, Get = () => 0.5 },
            new SettingEntry { Id = "broken", Get = null },             // no getter: unrenderable, dropped
        });
        b.DescribeSettings(new[] {
            new SettingEntry { Id = "ui.scale", Kind = SettingKind.Float, Label = "UI scale", Get = () => 1.0 },
        });
        Check(Count(Settings.Of("Sapphire")) == 2, "an entry without a getter is dropped");
        Check(Count(Settings.Search(null)) == 3, "an empty query returns everything from every mod");
        var hits = new List<KeyValuePair<string, SettingEntry>>(Settings.Search("bpm"));
        Check(hits.Count == 1 && hits[0].Key == "Sapphire", "search finds one setting and names its owner");
        Check(Count(Settings.Search("tempo")) == 1, "a keyword matches when the label does not");
        Check(Count(Settings.Search("sc")) == 1, "search spans mods");
        hits = new List<KeyValuePair<string, SettingEntry>>(Settings.Search("editor"));
        Check(hits.Count == 2 && hits[0].Value.Id == "editor.effectiveBpm", "an id hit outranks a group hit");
        hits[0].Value.Set(true);
        Check(stored && (bool)hits[0].Value.Get(), "Get/Set round-trip through the owning mod");
        a.DescribeSettings(new SettingEntry[0]);
        Check(Count(Settings.Of("Sapphire")) == 0 && Count(Settings.Search(null)) == 1, "re-describing replaces only that mod's entries");

        Console.WriteLine("diagnostics");
        int reads = 0;
        a.AddLog("SapphireLog", () => { reads++; return new[] { "one", "two" }; }, "/tmp/s.txt");
        b.AddLog("BismuthLog", () => new[] { "x" }, null);
        a.AddFields("Level", () => new[] { new KeyValuePair<string, string>("bpm", "120") });
        Check(Count(Diagnostics.Logs) == 2 && Count(Diagnostics.Fields) == 1, "sources register per mod");
        Check(reads == 0, "a source is pulled, not pushed — nothing read until someone looks");
        foreach (var l in Diagnostics.Logs) if (l.Owner == "Sapphire") Count(l.Tail());
        Check(reads == 1, "reading a source calls its delegate");
        a.AddLog("SapphireLog", () => new[] { "replaced" }, null);
        Check(Count(Diagnostics.Logs) == 2, "re-registering one name replaces rather than duplicates");
        Diagnostics.Unregister("Sapphire");
        Check(Count(Diagnostics.Logs) == 1 && Count(Diagnostics.Fields) == 0, "a mod going away takes its sources");

        Console.WriteLine(_fail == 0 ? "ALL PASS" : _fail + " FAILED");
        return _fail == 0 ? 0 : 1;
    }

    static int Count<T>(IEnumerable<T> e) { int n = 0; foreach (var _ in e) n++; return n; }
}
