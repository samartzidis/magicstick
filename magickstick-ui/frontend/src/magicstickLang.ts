import { autocompletion, CompletionContext, CompletionResult } from '@codemirror/autocomplete';
import { HighlightStyle, syntaxHighlighting } from '@codemirror/language';
import { Extension, RangeSetBuilder } from '@codemirror/state';
import { Decoration, DecorationSet, EditorView, ViewPlugin, ViewUpdate } from '@codemirror/view';
import { tags as t } from '@lezer/highlight';

// MagicStick syntax highlighting style
const magicstickHighlightStyle = HighlightStyle.define([
    // Comments (green)
    { tag: t.comment, color: '#008000' },

    // Function keywords (blue, bold)
    { tag: t.keyword, color: '#0000ff', fontWeight: 'bold' },

    // Control flow keywords (blue, bold)
    { tag: t.controlKeyword, color: '#0000ff', fontWeight: 'bold' },

    // System variables (magenta, bold)
    { tag: t.variableName, color: '#ff00ff', fontWeight: 'bold' },

    // HID Key constants (brown, bold)
    { tag: t.literal, color: '#8b4513', fontWeight: 'bold' },

    // Consumer constants (orange, bold)
    { tag: t.string, color: '#ff8c00', fontWeight: 'bold' },

    // Parentheses (bold)
    { tag: t.paren, fontWeight: 'bold' },

    // Colon (magenta, bold)
    { tag: t.punctuation, color: '#ff00ff', fontWeight: 'bold' },

    // Numbers
    { tag: t.number, color: '#000000' },

    // Default text
    { tag: t.name, color: '#000000' }
]);

// MagicStick keywords and patterns
const MAGICSTICK_KEYWORDS = [
    // Function keywords
    'get_key', 'set_key', 'find_key', 'set_mod', 'set_consumer_report',
    'ch_key', 'ch_key_ex', 'send_unicode', 'send_key',
    // Control flow keywords
    'end', 'label', 'goto'
];

const MAGICSTICK_VARIABLES = [
    'res', 'mod', 'apple_key_fn', 'apple_key_eject', 'apple_key_touch_id', 'apple_key_lock'
];

const MAGICSTICK_HID_KEYS = [
    'HID_KEY_NONE', 'HID_KEY_A', 'HID_KEY_B', 'HID_KEY_C', 'HID_KEY_D', 'HID_KEY_E', 'HID_KEY_F', 'HID_KEY_G', 'HID_KEY_H', 'HID_KEY_I', 'HID_KEY_J', 'HID_KEY_K', 'HID_KEY_L', 'HID_KEY_M', 'HID_KEY_N', 'HID_KEY_O', 'HID_KEY_P', 'HID_KEY_Q', 'HID_KEY_R', 'HID_KEY_S', 'HID_KEY_T', 'HID_KEY_U', 'HID_KEY_V', 'HID_KEY_W', 'HID_KEY_X', 'HID_KEY_Y', 'HID_KEY_Z',
    'HID_KEY_1', 'HID_KEY_2', 'HID_KEY_3', 'HID_KEY_4', 'HID_KEY_5', 'HID_KEY_6', 'HID_KEY_7', 'HID_KEY_8', 'HID_KEY_9', 'HID_KEY_0',
    'HID_KEY_ENTER', 'HID_KEY_ESCAPE', 'HID_KEY_BACKSPACE', 'HID_KEY_TAB', 'HID_KEY_SPACE', 'HID_KEY_MINUS', 'HID_KEY_EQUAL',
    'HID_KEY_BRACKET_LEFT', 'HID_KEY_BRACKET_RIGHT', 'HID_KEY_BACKSLASH', 'HID_KEY_EUROPE_1', 'HID_KEY_SEMICOLON', 'HID_KEY_APOSTROPHE', 'HID_KEY_GRAVE',
    'HID_KEY_COMMA', 'HID_KEY_PERIOD', 'HID_KEY_SLASH', 'HID_KEY_CAPS_LOCK',
    'HID_KEY_F1', 'HID_KEY_F2', 'HID_KEY_F3', 'HID_KEY_F4', 'HID_KEY_F5', 'HID_KEY_F6', 'HID_KEY_F7', 'HID_KEY_F8', 'HID_KEY_F9', 'HID_KEY_F10', 'HID_KEY_F11', 'HID_KEY_F12',
    'HID_KEY_PRINT_SCREEN', 'HID_KEY_SCROLL_LOCK', 'HID_KEY_PAUSE', 'HID_KEY_INSERT', 'HID_KEY_HOME', 'HID_KEY_PAGE_UP', 'HID_KEY_DELETE', 'HID_KEY_END', 'HID_KEY_PAGE_DOWN',
    'HID_KEY_ARROW_RIGHT', 'HID_KEY_ARROW_LEFT', 'HID_KEY_ARROW_DOWN', 'HID_KEY_ARROW_UP', 'HID_KEY_NUM_LOCK',
    'HID_KEY_KEYPAD_DIVIDE', 'HID_KEY_KEYPAD_MULTIPLY', 'HID_KEY_KEYPAD_SUBTRACT', 'HID_KEY_KEYPAD_ADD', 'HID_KEY_KEYPAD_ENTER',
    'HID_KEY_KEYPAD_1', 'HID_KEY_KEYPAD_2', 'HID_KEY_KEYPAD_3', 'HID_KEY_KEYPAD_4', 'HID_KEY_KEYPAD_5', 'HID_KEY_KEYPAD_6', 'HID_KEY_KEYPAD_7', 'HID_KEY_KEYPAD_8', 'HID_KEY_KEYPAD_9', 'HID_KEY_KEYPAD_0', 'HID_KEY_KEYPAD_DECIMAL',
    'HID_KEY_EUROPE_2', 'HID_KEY_APPLICATION', 'HID_KEY_POWER', 'HID_KEY_KEYPAD_EQUAL',
    'HID_KEY_F13', 'HID_KEY_F14', 'HID_KEY_F15', 'HID_KEY_F16', 'HID_KEY_F17', 'HID_KEY_F18', 'HID_KEY_F19', 'HID_KEY_F20', 'HID_KEY_F21', 'HID_KEY_F22', 'HID_KEY_F23', 'HID_KEY_F24',
    'HID_KEY_EXECUTE', 'HID_KEY_HELP', 'HID_KEY_MENU', 'HID_KEY_SELECT', 'HID_KEY_STOP', 'HID_KEY_AGAIN', 'HID_KEY_UNDO', 'HID_KEY_CUT', 'HID_KEY_COPY', 'HID_KEY_PASTE', 'HID_KEY_FIND',
    'HID_KEY_MUTE', 'HID_KEY_VOLUME_UP', 'HID_KEY_VOLUME_DOWN', 'HID_KEY_LOCKING_CAPS_LOCK', 'HID_KEY_LOCKING_NUM_LOCK', 'HID_KEY_LOCKING_SCROLL_LOCK', 'HID_KEY_KEYPAD_COMMA', 'HID_KEY_KEYPAD_EQUAL_SIGN',
    'HID_KEY_KANJI1', 'HID_KEY_KANJI2', 'HID_KEY_KANJI3', 'HID_KEY_KANJI4', 'HID_KEY_KANJI5', 'HID_KEY_KANJI6', 'HID_KEY_KANJI7', 'HID_KEY_KANJI8', 'HID_KEY_KANJI9',
    'HID_KEY_LANG1', 'HID_KEY_LANG2', 'HID_KEY_LANG3', 'HID_KEY_LANG4', 'HID_KEY_LANG5', 'HID_KEY_LANG6', 'HID_KEY_LANG7', 'HID_KEY_LANG8', 'HID_KEY_LANG9',
    'HID_KEY_ALTERNATE_ERASE', 'HID_KEY_SYSREQ_ATTENTION', 'HID_KEY_CANCEL', 'HID_KEY_CLEAR', 'HID_KEY_PRIOR', 'HID_KEY_RETURN', 'HID_KEY_SEPARATOR', 'HID_KEY_OUT', 'HID_KEY_OPER', 'HID_KEY_CLEAR_AGAIN', 'HID_KEY_CRSEL_PROPS', 'HID_KEY_EXSEL',
    'HID_KEY_CONTROL_LEFT', 'HID_KEY_SHIFT_LEFT', 'HID_KEY_ALT_LEFT', 'HID_KEY_GUI_LEFT', 'HID_KEY_CONTROL_RIGHT', 'HID_KEY_SHIFT_RIGHT', 'HID_KEY_ALT_RIGHT', 'HID_KEY_GUI_RIGHT'
];

const MAGICSTICK_MODIFIERS = [
    'KEYBOARD_MODIFIER_LEFTCTRL', 'KEYBOARD_MODIFIER_LEFTSHIFT', 'KEYBOARD_MODIFIER_LEFTALT', 'KEYBOARD_MODIFIER_LEFTGUI',
    'KEYBOARD_MODIFIER_RIGHTCTRL', 'KEYBOARD_MODIFIER_RIGHTSHIFT', 'KEYBOARD_MODIFIER_RIGHTALT', 'KEYBOARD_MODIFIER_RIGHTGUI'
];

const MAGICSTICK_CONSUMER = [
    'HID_USAGE_CONSUMER_CONTROL', 'HID_USAGE_CONSUMER_POWER', 'HID_USAGE_CONSUMER_RESET', 'HID_USAGE_CONSUMER_SLEEP',
    'HID_USAGE_CONSUMER_BRIGHTNESS_INCREMENT', 'HID_USAGE_CONSUMER_BRIGHTNESS_DECREMENT', 'HID_USAGE_CONSUMER_WIRELESS_RADIO_CONTROLS', 'HID_USAGE_CONSUMER_WIRELESS_RADIO_BUTTONS', 'HID_USAGE_CONSUMER_WIRELESS_RADIO_LED', 'HID_USAGE_CONSUMER_WIRELESS_RADIO_SLIDER_SWITCH',
    'HID_USAGE_CONSUMER_PLAY_PAUSE', 'HID_USAGE_CONSUMER_SCAN_NEXT', 'HID_USAGE_CONSUMER_SCAN_PREVIOUS', 'HID_USAGE_CONSUMER_STOP', 'HID_USAGE_CONSUMER_VOLUME', 'HID_USAGE_CONSUMER_MUTE',
    'HID_USAGE_CONSUMER_BASS', 'HID_USAGE_CONSUMER_TREBLE', 'HID_USAGE_CONSUMER_BASS_BOOST', 'HID_USAGE_CONSUMER_VOLUME_INCREMENT', 'HID_USAGE_CONSUMER_VOLUME_DECREMENT',
    'HID_USAGE_CONSUMER_BASS_INCREMENT', 'HID_USAGE_CONSUMER_BASS_DECREMENT', 'HID_USAGE_CONSUMER_TREBLE_INCREMENT', 'HID_USAGE_CONSUMER_TREBLE_DECREMENT',
    'HID_USAGE_CONSUMER_AL_CONSUMER_CONTROL_CONFIGURATION', 'HID_USAGE_CONSUMER_AL_EMAIL_READER', 'HID_USAGE_CONSUMER_AL_CALCULATOR', 'HID_USAGE_CONSUMER_AL_LOCAL_BROWSER',
    'HID_USAGE_CONSUMER_AC_SEARCH', 'HID_USAGE_CONSUMER_AC_HOME', 'HID_USAGE_CONSUMER_AC_BACK', 'HID_USAGE_CONSUMER_AC_FORWARD', 'HID_USAGE_CONSUMER_AC_STOP', 'HID_USAGE_CONSUMER_AC_REFRESH', 'HID_USAGE_CONSUMER_AC_BOOKMARKS', 'HID_USAGE_CONSUMER_AC_PAN'
];

// Autocomplete completions with descriptions
const MAGICSTICK_COMPLETIONS = [
    // Keywords with descriptions
    { label: 'get_key', type: 'function', info: 'get_key(position). Get currently pressed key code at specified key position [1-5]. Returns one of the HID_KEY_* constants.' },
    { label: 'set_key', type: 'function', info: 'set_key(position, value). Set key code at specified key position [1-5], or [0] for the next free position. \'value\' is one of the HID_KEY_* constants. Returns 1 for success, 0 for error.' },
    { label: 'find_key', type: 'function', info: 'find_key(value). Find the position where the specified key is pressed. \'value\' is one of the HID_KEY_* constants. Returns the position [1-5], or [0] if not pressed.' },
    { label: 'set_mod', type: 'function', info: 'set_mod(value). Set currently pressed modifier value.' },
    { label: 'set_consumer_report', type: 'function', info: 'set_consumer_report(value). Set a HID consumer report. Allowed values are one of the HID_USAGE_CONSUMER_*' },
    { label: 'ch_key', type: 'function', info: 'ch_key(from, to). Replace key code \'from\' (if pressed) with key code \'to\'.' },
    { label: 'ch_key_ex', type: 'function', info: 'ch_key_ex(mod_from, key_from, mod_to, key_to). Replace key code \'key_from\' (if pressed) with key code \'key_to\' by also matching modifier \'mod_from\' and replacing with \'mod_to\'.' },
    { label: 'send_unicode', type: 'function', info: 'send_unicode(value). Send a Unicode code point (character) by specifying its decimal value. value: a *decimal* Unicode code point value' },
    { label: 'send_key', type: 'function', info: 'send_key(mod, value). Asynchronously sends an independent key stroke as a background operation. mod: a mix of the KEYBOARD_MODIFIER_* constants or 0 for no modifiers. value: one of the HID_KEY_* constants.' },
    { label: 'end', type: 'keyword', info: 'Special \'end\' label.' },
    { label: 'label', type: 'keyword', info: 'Define a label.' },
    { label: 'goto', type: 'keyword', info: 'Go to a defined label.' },

    // System variables
    { label: 'res', type: 'variable', info: 'Holds the evaluation result of the current action.' },
    { label: 'mod', type: 'variable', info: 'The currently pressed modifier value. Will return one, or a mix (bitwise OR) of the KEYBOARD_MODIFIER_* constants.' },
    { label: 'apple_key_fn', type: 'variable', info: '1 when the Apple \'fn\' key is pressed, 0 otherwise.' },
    { label: 'apple_key_eject', type: 'variable', info: '1 when the Apple \'Eject\' key is pressed, 0 otherwise.' },
    { label: 'apple_key_touch_id', type: 'variable', info: '1 when the Apple \'Touch ID\' key is pressed, 0 otherwise.' },
    { label: 'apple_key_lock', type: 'variable', info: '1 when the Apple \'Lock\' key is pressed, 0 otherwise.' },
];

// Add all HID_KEY_* constants
MAGICSTICK_HID_KEYS.forEach(key => {
    MAGICSTICK_COMPLETIONS.push({ label: key, type: 'constant', info: `HID key constant: ${key}` });
});

// Add all KEYBOARD_MODIFIER_* constants
MAGICSTICK_MODIFIERS.forEach(modifier => {
    MAGICSTICK_COMPLETIONS.push({ label: modifier, type: 'constant', info: `Keyboard modifier: ${modifier}` });
});

// Add all HID_USAGE_CONSUMER_* constants
MAGICSTICK_CONSUMER.forEach(consumer => {
    MAGICSTICK_COMPLETIONS.push({ label: consumer, type: 'constant', info: `Consumer usage: ${consumer}` });
});

// MagicStick autocomplete function
function magicstickCompletions(context: CompletionContext): CompletionResult | null {
    const word = context.matchBefore(/\w*/);
    if (!word) return null;

    if (word.from === word.to && !context.explicit) return null;

    const options = MAGICSTICK_COMPLETIONS.filter(completion =>
        completion.label.toLowerCase().includes(word.text.toLowerCase())
    );

    return {
        from: word.from,
        options: options
    };
}

// Create decorations for different types
const keywordDecoration = Decoration.mark({ class: 'cm-keyword' });
const variableDecoration = Decoration.mark({ class: 'cm-variable' });
const hidKeyDecoration = Decoration.mark({ class: 'cm-hid-key' });
const modifierDecoration = Decoration.mark({ class: 'cm-modifier' });
const consumerDecoration = Decoration.mark({ class: 'cm-consumer' });
const commentDecoration = Decoration.mark({ class: 'cm-comment' });

// MagicStick highlighting plugin
const magicstickHighlightingPlugin = ViewPlugin.fromClass(class {
    decorations: DecorationSet;

    constructor(view: EditorView) {
        this.decorations = this.buildDecorations(view);
    }

    update(update: ViewUpdate) {
        if (update.docChanged || update.viewportChanged) {
            this.decorations = this.buildDecorations(update.view);
        }
    }

    buildDecorations(view: EditorView): DecorationSet {
        const builder = new RangeSetBuilder<Decoration>();
        const text = view.state.doc.toString();

        // Split into lines and process each line
        const lines = text.split('\n');
        let offset = 0;

        for (const line of lines) {
            // Skip comments (lines starting with #)
            if (line.trim().startsWith('#')) {
                builder.add(offset, offset + line.length, commentDecoration);
                offset += line.length + 1; // +1 for newline
                continue;
            }

            // Process words in the line
            const words = line.split(/\b/);
            let wordOffset = offset;

            for (const word of words) {
                const trimmedWord = word.trim();

                if (MAGICSTICK_KEYWORDS.includes(trimmedWord)) {
                    builder.add(wordOffset, wordOffset + word.length, keywordDecoration);
                } else if (MAGICSTICK_VARIABLES.includes(trimmedWord)) {
                    builder.add(wordOffset, wordOffset + word.length, variableDecoration);
                } else if (MAGICSTICK_HID_KEYS.includes(trimmedWord)) {
                    builder.add(wordOffset, wordOffset + word.length, hidKeyDecoration);
                } else if (MAGICSTICK_MODIFIERS.includes(trimmedWord)) {
                    builder.add(wordOffset, wordOffset + word.length, modifierDecoration);
                } else if (MAGICSTICK_CONSUMER.includes(trimmedWord)) {
                    builder.add(wordOffset, wordOffset + word.length, consumerDecoration);
                }

                wordOffset += word.length;
            }

            offset += line.length + 1; // +1 for newline
        }

        return builder.finish();
    }
}, {
    decorations: v => v.decorations
});

// MagicStick language definition
export function magicstick(): Extension {
    return [
        syntaxHighlighting(magicstickHighlightStyle),
        magicstickHighlightingPlugin,
        autocompletion({
            override: [magicstickCompletions]
        })
    ];
}