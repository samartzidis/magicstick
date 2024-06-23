using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MagicStickUI
{
    public static class KeyboardInputSender
    {
        public const int INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;

        public struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static void SendStringToActiveWindow(string s)
        {
            var inputs = new List<INPUT>();

            foreach (var c in s)
            {
                foreach (var keyUp in new bool[] { false, true })
                {
                    var input = new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
                                dwExtraInfo = GetMessageExtraInfo(),
                            }
                        }
                    };

                    inputs.Add(input);
                }
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendUnicodeToActiveWindow(int unicodeValue)
        {
            var inputs = new List<INPUT>();

            // Get surrogate pairs for the unicode value
            var surrogatePairs = UnicodeToUtf16SurrogatePairs(unicodeValue);

            foreach (var pair in surrogatePairs)
            {
                // Create input for key down event
                var keyDownInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = pair,
                            dwFlags = KEYEVENTF_UNICODE,
                            dwExtraInfo = GetMessageExtraInfo(),
                        }
                    }
                };

                // Create input for key up event
                var keyUpInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = pair,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            dwExtraInfo = GetMessageExtraInfo(),
                        }
                    }
                };

                // Add key down and key up inputs to the list
                inputs.Add(keyDownInput);
                inputs.Add(keyUpInput);
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// The KEYEVENTF_UNICODE flag in the Windows API is designed to send Unicode characters, including complex characters like emojis. 
        /// However, sending emojis or characters outside the Basic Multilingual Plane (BMP), which require more than 16 bits, 
        /// involves the use of surrogate pairs.
        /// This function converts the Unicode code point to UTF-16 surrogate pairs.
        /// </summary>
        /// <param name="codePoint"></param>
        /// <returns></returns>
        private static List<ushort> UnicodeToUtf16SurrogatePairs(int codePoint)
        {
            var surrogatePairs = new List<ushort>();

            if (codePoint >= 0x10000 && codePoint <= 0x10FFFF)
            {
                codePoint -= 0x10000;
                var highSurrogate = (ushort)((codePoint >> 10) + 0xD800);
                var lowSurrogate = (ushort)((codePoint & 0x3FF) + 0xDC00);

                surrogatePairs.Add(highSurrogate);
                surrogatePairs.Add(lowSurrogate);
            }
            else
            {
                // If the code point is within the BMP, just push it directly
                surrogatePairs.Add((ushort)codePoint);
            }

            return surrogatePairs;
        }
    }
}
