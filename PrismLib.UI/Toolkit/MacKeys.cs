using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PrismLib.UI.Toolkit
{
    /* macOS: while Option is held Unity's legacy Input drops some letters' keydowns, and which ones
       depends on the key (seen in-game on Unity 6: under Option+Shift, D and S arrived while K and P
       never did — a scan of every GetKeyDown saw only LeftAlt and LeftShift). So on a Mac a key's
       edge comes from the window server instead,
       which sees the physical key whatever the keyboard layout composes from it. Elsewhere these are
       plain Input calls.

       Down needs polling EVERY frame — an edge is a change since the frame before, and a skipped
       frame drops it rather than inventing one. Both respect InputGate like the Input calls they
       stand in for, and read nothing while the game is unfocused: the HID state is global, so a key
       typed into another app would otherwise reach the game. */
    public static class MacKeys
    {
        public static readonly bool IsMac =
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

        /// The frame `key` went down.
        public static bool Down(KeyCode key)
        {
            if (!IsMac) return Input.GetKeyDown(key);
            State s;
            if (!_keys.TryGetValue(key, out s)) _keys[key] = s = new State();
            int frame = Time.frameCount;
            if (s.Frame != frame)
            {
                bool held = MacHeld(key);
                s.Down = held && !s.Held && s.Frame == frame - 1;
                s.Held = held;
                s.Frame = frame;
            }
            return s.Down && !InputGate.Blocking;
        }

        /// Unity's state, or the window server's — the clock Down reads, so a chord whose keys all
        /// land in one frame still lines up (Unity can see a modifier a frame late).
        public static bool Held(KeyCode key) => Input.GetKey(key) || (IsMac && MacHeld(key) && !InputGate.Blocking);

        private sealed class State { public int Frame = -2; public bool Held, Down; }
        private static readonly Dictionary<KeyCode, State> _keys = new Dictionary<KeyCode, State>();

        private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
        [DllImport(AppServices)] private static extern bool CGEventSourceKeyState(int state, ushort key);
        [DllImport(AppServices)] private static extern ulong CGEventSourceFlagsState(int state);
        private const int HidSystemState = 1;
        private static bool _broken;

        private static bool MacHeld(KeyCode key)
        {
            bool held;
            return Application.isFocused && TryPhysical(key, out held) && held;
        }

        /* The window server's state for `key` with no InputGate and no focus check — for what must see
           the physical keyboard whatever a menu is doing: a key viewer, right-hand modifiers Unity folds
           into the left ones. False when there is no answer (not a Mac, an unmapped key, the call
           failed), and `held` is then false. Same table and semantics as Quartz's
           HookInput.TryMacKeyState, which it replaces. */
        public static bool TryPhysical(KeyCode key, out bool held)
        {
            held = false;
            if (!IsMac || _broken) return false;
            try
            {
                ulong mask;
                ushort vk;
                // Per side for modifiers: CGEventSourceKeyState answers per modifier, the NX_DEVICE*KEYMASK flag bits per key.
                if (Masks.TryGetValue(key, out mask)) held = (CGEventSourceFlagsState(HidSystemState) & mask) != 0UL;
                else if (Codes.TryGetValue(key, out vk)) held = CGEventSourceKeyState(HidSystemState, vk);
                else return false;
                return true;
            }
            catch (Exception e) { _broken = true; Ui.Log("MacKeys: window-server key state unavailable — " + e.Message); return false; }
        }

        private static readonly Dictionary<KeyCode, ulong> Masks = new Dictionary<KeyCode, ulong>
        {
            { KeyCode.LeftControl, 0x0001 }, { KeyCode.LeftShift, 0x0002 }, { KeyCode.RightShift, 0x0004 },
            { KeyCode.LeftCommand, 0x0008 }, { KeyCode.RightCommand, 0x0010 }, { KeyCode.LeftAlt, 0x0020 },
            { KeyCode.RightAlt, 0x0040 }, { KeyCode.RightControl, 0x2000 },
        };

        // kVK_* (ANSI positions). No CapsLock: the window server reports lock state, not the key. No
        // Backslash: non-US layouts never report it under 0x2A.
        private static readonly Dictionary<KeyCode, ushort> Codes = new Dictionary<KeyCode, ushort>
        {
            { KeyCode.A, 0x00 }, { KeyCode.S, 0x01 }, { KeyCode.D, 0x02 }, { KeyCode.F, 0x03 }, { KeyCode.H, 0x04 },
            { KeyCode.G, 0x05 }, { KeyCode.Z, 0x06 }, { KeyCode.X, 0x07 }, { KeyCode.C, 0x08 }, { KeyCode.V, 0x09 },
            { KeyCode.B, 0x0B }, { KeyCode.Q, 0x0C }, { KeyCode.W, 0x0D }, { KeyCode.E, 0x0E }, { KeyCode.R, 0x0F },
            { KeyCode.Y, 0x10 }, { KeyCode.T, 0x11 }, { KeyCode.Alpha1, 0x12 }, { KeyCode.Alpha2, 0x13 },
            { KeyCode.Alpha3, 0x14 }, { KeyCode.Alpha4, 0x15 }, { KeyCode.Alpha6, 0x16 }, { KeyCode.Alpha5, 0x17 },
            { KeyCode.Equals, 0x18 }, { KeyCode.Alpha9, 0x19 }, { KeyCode.Alpha7, 0x1A }, { KeyCode.Minus, 0x1B },
            { KeyCode.Alpha8, 0x1C }, { KeyCode.Alpha0, 0x1D }, { KeyCode.RightBracket, 0x1E }, { KeyCode.O, 0x1F },
            { KeyCode.U, 0x20 }, { KeyCode.LeftBracket, 0x21 }, { KeyCode.I, 0x22 }, { KeyCode.P, 0x23 },
            { KeyCode.Return, 0x24 }, { KeyCode.L, 0x25 }, { KeyCode.J, 0x26 }, { KeyCode.Quote, 0x27 }, { KeyCode.K, 0x28 },
            { KeyCode.Semicolon, 0x29 }, { KeyCode.Comma, 0x2B }, { KeyCode.Slash, 0x2C },
            { KeyCode.N, 0x2D }, { KeyCode.M, 0x2E }, { KeyCode.Period, 0x2F }, { KeyCode.Tab, 0x30 }, { KeyCode.Space, 0x31 },
            { KeyCode.BackQuote, 0x32 }, { KeyCode.Backspace, 0x33 }, { KeyCode.Escape, 0x35 },
            { KeyCode.KeypadPeriod, 0x41 }, { KeyCode.KeypadMultiply, 0x43 }, { KeyCode.KeypadPlus, 0x45 },
            { KeyCode.KeypadDivide, 0x4B }, { KeyCode.KeypadEnter, 0x4C }, { KeyCode.KeypadMinus, 0x4E },
            { KeyCode.KeypadEquals, 0x51 }, { KeyCode.Keypad0, 0x52 }, { KeyCode.Keypad1, 0x53 }, { KeyCode.Keypad2, 0x54 },
            { KeyCode.Keypad3, 0x55 }, { KeyCode.Keypad4, 0x56 }, { KeyCode.Keypad5, 0x57 }, { KeyCode.Keypad6, 0x58 },
            { KeyCode.Keypad7, 0x59 }, { KeyCode.Keypad8, 0x5B }, { KeyCode.Keypad9, 0x5C },
            { KeyCode.F1, 0x7A }, { KeyCode.F2, 0x78 }, { KeyCode.F3, 0x63 }, { KeyCode.F4, 0x76 }, { KeyCode.F5, 0x60 },
            { KeyCode.F6, 0x61 }, { KeyCode.F7, 0x62 }, { KeyCode.F8, 0x64 }, { KeyCode.F9, 0x65 }, { KeyCode.F10, 0x6D },
            { KeyCode.F11, 0x67 }, { KeyCode.F12, 0x6F }, { KeyCode.F13, 0x69 }, { KeyCode.F14, 0x6B }, { KeyCode.F15, 0x71 },
            { KeyCode.Insert, 0x72 }, { KeyCode.Home, 0x73 }, { KeyCode.PageUp, 0x74 }, { KeyCode.Delete, 0x75 },
            { KeyCode.End, 0x77 }, { KeyCode.PageDown, 0x79 }, { KeyCode.LeftArrow, 0x7B }, { KeyCode.RightArrow, 0x7C },
            { KeyCode.DownArrow, 0x7D }, { KeyCode.UpArrow, 0x7E },
        };
    }
}
