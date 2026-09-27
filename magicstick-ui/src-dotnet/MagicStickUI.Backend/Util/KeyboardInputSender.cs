using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MagicStickUI.Backend.Util;

/// <summary>
/// Sends keyboard input to the active window using Windows SendInput (Unicode).
/// Used when the MagicStick device sends send_unicode_char_event to type a character.
/// </summary>
[SupportedOSPlatform("windows")]
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

    /// <summary>
    /// Sends a single Unicode code point to the active window using KEYEVENTF_UNICODE.
    /// Supports code points outside the BMP (surrogate pairs).
    /// </summary>
    public static void SendUnicodeToActiveWindow(int unicodeValue)
    {
        var inputs = new List<INPUT>();
        var surrogatePairs = UnicodeToUtf16SurrogatePairs(unicodeValue);

        foreach (var pair in surrogatePairs)
        {
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

            inputs.Add(keyDownInput);
            inputs.Add(keyUpInput);
        }

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
    }

    /// <summary>
    /// Converts a Unicode code point to UTF-16 surrogate pairs for SendInput.
    /// Code points &gt; 0xFFFF (e.g. emoji) are represented as high + low surrogate.
    /// </summary>
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
            surrogatePairs.Add((ushort)codePoint);
        }

        return surrogatePairs;
    }
}
