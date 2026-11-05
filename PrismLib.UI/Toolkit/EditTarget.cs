using System;

namespace PrismLib.UI.Toolkit
{
    /* PrismLib.LayoutTarget, copied field for field (plus Owner), because PrismLib.UI must not
       reference PrismLib.dll. A mod's bridge converts; see LayoutEditor. Same conventions: screen
       pixels with a bottom-left origin, degrees counter-clockwise, anchors 0-8 from the top-left,
       crop {left, bottom, right, top}. */
    public sealed class EditTarget
    {
        public string Owner, Id, Label;
        public Func<float[]> GetBox;               // {centerX, centerY, width, height}
        public Action<float, float> MoveTo;
        public Func<float> GetScale;
        public Action<float> SetScale;
        public float MinScale = 0.1f, MaxScale = 4f;
        public Func<float> GetRotation;
        public Action<float> SetRotation;
        public Func<int> GetAnchor;
        public Action<int> SetAnchor;
        public Func<bool> GetAnchorClosest;
        public Action<bool> SetAnchorClosest;
        public Func<float[]> GetCrop;
        public Action<float[]> SetCrop;

        internal string Key => Owner + "/" + Id;
    }
}
