using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A tab strip that keeps its selection by NAME across a rebuild, so a log source appearing does
       not switch which tab you are reading — see Facet.Tabs. */
    public sealed class Tabs
    {
        private readonly Facet.Tabs _tabs;

        public int Selected => _tabs.Selected;
        public string SelectedName => _tabs.SelectedName;

        public Tabs(VisualElement parent, Action<int> onSelect)
        {
            using (PrismHost.Use()) _tabs = new Facet.Tabs(parent, onSelect);
        }

        public VisualElement Element => _tabs.Element;

        public void Rebuild(IEnumerable<string> names)
        {
            using (PrismHost.Use()) _tabs.Rebuild(names);
        }

        public void Select(int index) => _tabs.Select(index);
    }
}
