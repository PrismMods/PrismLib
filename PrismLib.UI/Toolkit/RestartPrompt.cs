using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* "Restart now?" once a mod has installed an update. One card however many mods update:
       PrismLib.UI is loaded once per game, so a second Request adds its name to the open card.

       The remembered choice belongs to PrismLib (Config `update.restart`, also in Prism · Settings)
       and crosses as delegates, because this half cannot read PrismLib.dll. Without PrismLib there is
       nowhere to remember it, so the toggle is left out and the prompt always asks.

       Restart goes back through Steam rather than re-running the binary: mod loaders hang off Steam's
       launch options (MelonLoader's macOS wrapper), and a bare re-exec would come back without them.
       Steam will not start a game it still sees running, so a helper waits for this process to exit
       first — and gives up after a minute, so a quit the game cancels (the editor's unsaved-changes
       prompt) can never turn into a surprise relaunch later. */
    public static class RestartPrompt
    {
        public const int Ask = 0, Restart = 1, Later = 2;

        private const string SteamUrl = "steam://rungameid/977950";

        private static Surface _surface;
        private static Label _body;
        private static VisualElement _rememberRow;
        private static bool _remember;
        private static Action<int> _write;
        private static readonly List<string> _mods = new List<string>();

        /// Wine/Proton runs the Windows build with no PowerShell worth the name, and the helper is
        /// what makes a restart possible — without it, ask nothing and let the mod's toast say it.
        public static bool CanRestart
        {
            get
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH"))
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX")))
                    return false;
                var p = Application.platform;
                return p == RuntimePlatform.OSXPlayer || p == RuntimePlatform.WindowsPlayer
                    || p == RuntimePlatform.LinuxPlayer;
            }
        }

        /// A mod finished installing an update. Main thread. read/write are the remembered choice
        /// (Ask/Restart/Later); pass null for both when PrismLib.dll is unavailable.
        public static void Request(string mod, Func<int> read, Action<int> write)
        {
            if (!CanRestart) return;
            int choice = Ask;
            try { if (read != null) choice = read(); } catch { }
            if (choice == Later) return;
            if (choice == Restart) { RestartNow(); return; }

            if (!string.IsNullOrEmpty(mod) && !_mods.Contains(mod)) _mods.Add(mod);
            _write = write;
            if (_surface == null && !Build()) return;
            _body.text = string.Join(" and ", _mods.ToArray()) + " updated. Restart the game now to load the new version?";
            _rememberRow.style.display = _write != null ? DisplayStyle.Flex : DisplayStyle.None;
            _surface.Visible = true;
        }

        private static bool Build()
        {
            _surface = new Surface("PrismRestartPrompt", Layer.Modal);
            if (_surface.Root == null) { _surface = null; return false; }
            _surface.EscapeHandler = () => { Choose(Later); return true; };

            // Dims and swallows clicks: a modal the world behind it can still be clicked through isn't one.
            var dim = new VisualElement();
            dim.style.position = Position.Absolute;
            dim.style.left = 0f; dim.style.right = 0f; dim.style.top = 0f; dim.style.bottom = 0f;
            dim.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            dim.style.alignItems = Align.Center;
            dim.style.justifyContent = Justify.Center;
            dim.pickingMode = PickingMode.Position;
            _surface.Root.Add(dim);

            var card = Ui.Card(dim);
            card.style.width = 440f;
            Ui.Text("Restart to finish updating", card, Tokens.FontSizeTitle);
            _body = Ui.Text("", card);
            _body.style.whiteSpace = WhiteSpace.Normal;
            _body.style.marginTop = Tokens.Gap;
            _body.style.marginBottom = Tokens.Gap;

            _remember = false;
            _rememberRow = Widgets.Toggle(card, "Remember my choice", false, v => _remember = v,
                "Change it later in Prism · Settings").parent;

            var buttons = Ui.Row(card);
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = Tokens.Gap;
            Ui.Btn("Later", () => Choose(Later), buttons);
            var restart = Ui.Btn("Restart now", () => Choose(Restart), buttons);
            restart.style.marginRight = 0f;
            Ui.Highlight(restart, true);
            return true;
        }

        private static void Choose(int choice)
        {
            if (_remember && _write != null)
            {
                try { _write(choice); } catch (Exception e) { Ui.Log("RestartPrompt: remember failed: " + e.Message); }
            }
            if (_surface != null) _surface.Visible = false;
            _mods.Clear();
            if (choice == Restart) RestartNow();
        }

        /// Quit and come back through Steam. Quits only once the helper is running, so a failure
        /// leaves the game up rather than closing it for good.
        public static void RestartNow()
        {
            int pid;
            try { pid = Process.GetCurrentProcess().Id; }
            catch (Exception e) { Ui.Log("RestartPrompt: no pid: " + e.Message); return; }

            ProcessStartInfo psi;
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -WindowStyle Hidden -Command \""
                    + "$p = Get-Process -Id " + pid + " -ErrorAction SilentlyContinue; "
                    + "if ($p -and -not $p.WaitForExit(60000)) { exit }; "
                    + "Start-Sleep -Seconds 1; Start-Process '" + SteamUrl + "'\"");
            }
            else
            {
                string open = Application.platform == RuntimePlatform.OSXPlayer
                    ? "open '" + SteamUrl + "'"
                    : "xdg-open '" + SteamUrl + "' || steam '" + SteamUrl + "'";
                psi = new ProcessStartInfo("/bin/sh", "-c \""
                    + "i=0; while kill -0 " + pid + " 2>/dev/null; do i=$((i+1)); [ $i -gt 120 ] && exit 0; sleep 0.5; done; "
                    + "sleep 1; " + open + "\"");
            }
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            // The loader's injection env: MelonLoader's arm64 dylib kills arm64e system binaries
            // (dyld exit 134), so the helper would die after the game had already quit.
            var injected = new List<string>();
            foreach (string k in psi.EnvironmentVariables.Keys)
                if (k.StartsWith("DYLD_", StringComparison.OrdinalIgnoreCase) || k.Equals("LD_PRELOAD", StringComparison.OrdinalIgnoreCase))
                    injected.Add(k);
            foreach (var k in injected) psi.EnvironmentVariables.Remove(k);
            try { Process.Start(psi); }
            catch (Exception e) { Ui.Log("RestartPrompt: helper failed, not quitting: " + e.Message); return; }
            Ui.Log("RestartPrompt: restarting through Steam");
            Application.Quit();
        }
    }
}
