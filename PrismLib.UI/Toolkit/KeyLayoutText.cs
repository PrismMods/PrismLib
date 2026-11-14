using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /* The key layout editor's label ids and their English fallbacks. A mod maps each id to its own
       translation; an id it does not map shows the English. BCL only, so tools/PrismLibTest.cs
       compiles this file directly and checks it offline. */
    public static class KeyLayoutText
    {
        public const string File = "kle.file", Edit = "kle.edit", Insert = "kle.insert", Arrange = "kle.arrange", View = "kle.view",
            Layouts = "kle.layouts", NewLayout = "kle.new-layout", Presets = "kle.presets",
            Undo = "kle.undo", Redo = "kle.redo", Cut = "kle.cut", Copy = "kle.copy", Paste = "kle.paste", Duplicate = "kle.duplicate",
            Delete = "kle.delete", CopyStyle = "kle.copy-style", PasteStyle = "kle.paste-style", SelectAll = "kle.select-all",
            Rebind = "kle.rebind", RebindInOrder = "kle.rebind-order",
            ToFront = "kle.to-front", ToBack = "kle.to-back", AlignLeft = "kle.align-left", AlignRight = "kle.align-right",
            AlignTop = "kle.align-top", AlignBottom = "kle.align-bottom", SpreadH = "kle.spread-h", SpreadV = "kle.spread-v",
            Hide = "kle.hide", Show = "kle.show",
            ZoomIn = "kle.zoom-in", ZoomOut = "kle.zoom-out", ZoomReset = "kle.zoom-reset", Fit = "kle.fit",
            Snap = "kle.snap", Grid = "kle.grid", Guides = "kle.guides",
            Hands = "kle.hands", Feet = "kle.feet", Rename = "kle.rename", HideFeet = "kle.hide-feet", MoveLayout = "kle.move-layout",
            DeleteConfirm = "kle.delete-confirm", Cancel = "kle.cancel",
            Switcher = "kle.switcher", Hidden = "kle.hidden", NewFromPreset = "kle.new-from-preset", EditLayout = "kle.edit-layout",
            ExportDm = "kle.export-dm", CanvasHint = "kle.canvas-hint";

        // Id, English, in declaration order.
        private static readonly string[] Pairs =
        {
            File, "File", Edit, "Edit", Insert, "Insert", Arrange, "Arrange", View, "View",
            Layouts, "Layouts", NewLayout, "New Layout Tab", Presets, "Presets",
            Undo, "Undo", Redo, "Redo", Cut, "Cut", Copy, "Copy", Paste, "Paste", Duplicate, "Duplicate",
            Delete, "Delete", CopyStyle, "Copy Style", PasteStyle, "Paste Style", SelectAll, "Select All",
            Rebind, "Rebind Key", RebindInOrder, "Rebind Keys in Order",
            ToFront, "Bring to Front", ToBack, "Send to Back", AlignLeft, "Align Left", AlignRight, "Align Right",
            AlignTop, "Align Top", AlignBottom, "Align Bottom", SpreadH, "Spread Horizontally", SpreadV, "Spread Vertically",
            Hide, "Hide", Show, "Show",
            ZoomIn, "Zoom In", ZoomOut, "Zoom Out", ZoomReset, "Actual Size", Fit, "Fit",
            Snap, "Snap", Grid, "Grid", Guides, "Guides",
            Hands, "Hands", Feet, "Feet", Rename, "Rename", HideFeet, "Hide Foot Keys", MoveLayout, "Move Layout",
            DeleteConfirm, "Delete this layout?", Cancel, "Cancel",
            Switcher, "Key Viewer Layouts", Hidden, "Hidden", NewFromPreset, "New from Preset", EditLayout, "Edit",
            ExportDm, "Export as DM Note Config",
            CanvasHint, "Pan with the wheel or a right or middle drag. Arrow keys move the selection.",
        };

        private static readonly Dictionary<string, string> EnglishOf = new Dictionary<string, string>();
        private static readonly List<string> IdList = new List<string>();

        static KeyLayoutText()
        {
            for (int i = 0; i < Pairs.Length; i += 2) { IdList.Add(Pairs[i]); EnglishOf[Pairs[i]] = Pairs[i + 1]; }
        }

        /// The English fallback; the id itself when unknown.
        public static string English(string id)
        {
            string s;
            return id != null && EnglishOf.TryGetValue(id, out s) ? s : id;
        }

        /// Every id, in declaration order.
        public static IReadOnlyList<string> Ids => IdList;
    }
}
