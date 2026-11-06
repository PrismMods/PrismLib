using System;

namespace PrismLib.UI.Toolkit
{
    /* Undo and redo for settings, for the whole session and by VALUE: Facet.History, so one undo
       spans Prism windows and Facet-native ones (see Facet.History for the Applying and coalescing
       rules). Labels are translated through Words, as the windows did. */
    public static class History
    {
        /// How long two changes to one control stay a single step.
        public const float CoalesceSeconds = 0.9f;

        /// Entries kept: a guard against something recording in a loop.
        public const int Limit = 500;

        private static readonly Func<string, string> _translate = Words.T;

        /* AfterApply stays this class's own field (mods and SettingsPanel assign it with =), chained
           onto Facet's once; Facet's own refresh is internal and runs first either way (ruling R14).
           ponytail: a Facet-native consumer assigning Facet.History.AfterApply with = drops this hook. */
        static History() { Facet.History.AfterApply += () => { var a = AfterApply; if (a != null) a(); }; }

        /// True while an undo or redo is being applied. Widgets check it and stay quiet.
        public static bool Applying => Facet.History.Applying;

        /// Raised whenever the stacks change, so a toolbar can enable or disable its buttons.
        public static event Action Changed
        {
            add { Facet.History.Changed += value; }
            remove { Facet.History.Changed -= value; }
        }

        /// Called after an undo or redo has been applied, for panels to re-read what they show.
        public static Action AfterApply;

        public static bool CanUndo => Facet.History.CanUndo;
        public static bool CanRedo => Facet.History.CanRedo;
        public static string NextUndo => Facet.History.NextUndo;
        public static string NextRedo => Facet.History.NextRedo;

        /// key groups changes that should coalesce — a setting's id or label.
        public static void Record(string label, string key, Action undo, Action redo)
            => Facet.History.Record(label, key, undo, redo, _translate);

        public static bool Undo() => Facet.History.Undo();

        public static bool Redo() => Facet.History.Redo();

        public static void Clear() => Facet.History.Clear();
    }
}
