using UnityEngine;

namespace PrismLib.UI
{
    /* Backs ScrubLabel-style "drag a number to change it" controls. Step() reports the same
       pixel units as Unity's own PointerEventData.delta.x, so a caller can use it as a drop-in
       replacement with no rescaling.

       The name promises more than this does: true "cursor warps back at the screen edge"
       needs an OS cursor-warp call (SetCursorPos / CGWarpMouseCursorPosition / XWarpPointer),
       and Unity's own built-in substitute — CursorLockMode.Locked, read back through
       Input.GetAxis("Mouse X") — is scaled by a project's configured mouse sensitivity, which a
       mod has no way to read, so it would silently drag at the wrong speed instead of matching
       the pixel-for-pixel path it is meant to replace. Shipping a native P/Invoke call this
       assembly cannot be run against before release risks a hard crash (a bad native pointer
       does not throw a catchable exception) on the one platform correctness here matters most.
       So this does the safe two-thirds: it hides the cursor for the drag (purely cosmetic, zero
       risk) and tracks Input.mousePosition directly (exact pixels, no sensitivity guesswork).
       At a screen edge the cursor simply stops, same as the caller's own non-infinite fallback —
       which every call site already treats as an acceptable degraded path. */
    public sealed class InfiniteDrag
    {
        private Vector3 _last;
        private bool _active;

        public void Begin()
        {
            _last = Input.mousePosition;
            _active = true;
            Cursor.visible = false;
        }

        public float Step()
        {
            if (!_active) return 0f;
            Vector3 now = Input.mousePosition;
            float dx = now.x - _last.x;
            _last = now;
            return dx;
        }

        public void End()
        {
            _active = false;
            Cursor.visible = true;
        }
    }
}
