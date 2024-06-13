[![magicstick-logo](magicstick-logo.png)](https://github.com/samartzidis/magicstick.io)
###### Apple Keyboard USB Adapter for PC

# User Manual

![model](model.png)

| Label | Description |
| -------- | ------- |
| **A** | **Keyboard** connection, USB Type-A port. |
| **B** | **PC** connection, micro-USB port. |
| **C** | **BOOTSEL**/**RESET** button. |
| **D** | Operation **LED** |

## Connecting a Keyboard

### Wired Connection

This is the simplest mode of connection and your keyboard should just work by plugging it into the USB-A female port (the bigger USB port) of the device labelled **A** in the diagram. You will need a **USB-A to Lightning** cable.

The micro-USB connection labelled **B** must be connected to one of your PC's USB ports.

### Bluetooth Connection

#### Connecting the Newer A1644, A2450, A2449 Keyboards

Connecting any of these keyboards is pretty straightforward. 

1. Remove any keyboard currently plugged-in to magicstick.io.
2. Turn the keyboard off and then on again. 

   ![](20230928230341.png)

3. Plug the magicstick.io device to a USB port and keep it close to the keyboard. magicstick.io should discover the keyboard and pair with it. There is no PIN code entry required.

#### Connecting the Older [A1314](https://en.wikipedia.org/wiki/Apple_Wireless_Keyboard#/media/File:Apple-wireless-keyboard-aluminum-2007.jpg) Keyboard

The older A1314 keyboard has a little bit more complicated pairing process. 

**Important:** Make sure that the magicstick.io device is first **reset to factory settings**, so that it holds no previous paired keyboard data in its internal memory, by following [these steps](#Factory-Resetting-the-Device).

1. Remove any keyboard currently plugged-in to magicstick.io.
2. Turn the A1314 keyboard off by constantly pressing the right side button for a few seconds. You will see the green keyboard led powering off in a fading out fashion.
3. Turn the A1314 keyboard on by constantly pressing the right side button for a few seconds. Keep pressing it until the green led starts flashing. The keyboard is now in discovery mode.
4. (Unplug if plugged-in and) plug-in again your magicstick.io device to a PC USB port. magicstick.io will try to discover the keyboard. When the magicstick.io LED starts flashing non-stop, the keyboard is discovered and pairing has started. Immediately type **0000** (that is, four zeros) on the keyboard and press <kbd>Enter</kbd>.
5. The keyboard should be now paired and connected.
If this process fails repeat from step 1.

## The magicstick-ui Windows Utility

![alt text](Untitled-1.png)

The magicstick-ui utility allows you to monitor the keyboard's connection status, monitor the battery level (both when wired or in Bluetooth), as well as to change the keyboard's special keys configuration and default keymap. You can also use it for permanently turning the magicstick.io Bluetooth chip on or off, for instance if you are in a very high IT security work environment.

Download the latest magicstick-ui msi installer version from the [releases](https://github.com/samartzidis/magicstick.io/releases) page.

## LED Status Reference

The LED is located at the diagram position marked **D**. The following table summarizes the various LED flashing states of the device:

| LED Status | Meaning |
|------------|---------|
| LED is **on**. | A keyboard is connected via wired or Bluetooth connection. |
| LED is **off**. | Device malfunction. |
| LED is **flashing** non-stop. | Bluetooth has initiated **pairing** mode. Depending on the keyboard model you may need to enter **0000** and press <kbd>Enter</kbd> on the keyboard to complete pairing or just wait, see [Connecting a Keyboard](#Connecting-a-Keyboard) for details. |
| **1 flash** and a pause. | **IDLE**. magicstick.io is operational but no keyboard is connected via wire connection or Bluetooth. |
| **2 flashes** and a pause. | **Bluetooth** **CONNECTING**. magicstick.io Bluetooth is trying to connect to an already paired keyboard via Bluetooth. |
| **3 flashes** and a pause. | **Bluetooth** **INQUIRING**. magicstick.io Bluetooth is in inquiry (aka discovery) mode trying to discover and pair with a suitable keyboard nearby. |

## Keymap

When you connect your keyboard for the first time, this is the default keymap:

| Input Key(s)  | Output Key    |
| --- | --- |
| <kbd>Left Ctrl</kbd>  | <kbd>Fn</kbd> |
| <kbd>Fn</kbd> | <kbd>Left Ctrl</kbd> |
| <kbd>⏏︎ Eject</kbd> or <kbd>🔒 Lock</kbd> | <kbd>Del</kbd> |
| <kbd>⌘ Cmd</kbd>  | <kbd>Alt</kbd>    |
| <kbd>⌥ Alt/Option</kbd>  | <kbd>Cmd</kbd>    |
| <kbd>Fn</kbd> + <kbd>[F1]</kbd> | Brightness Down |
| <kbd>Fn</kbd> + <kbd>[F2]</kbd> | Brightness Up |
| <kbd>Fn</kbd> + <kbd>[F6]</kbd> | Sleep (Windows OS) |
| <kbd>Fn</kbd> + <kbd>[F7]</kbd> ... <kbd>[F12]</kbd> | Multimedia Keys</kbd> |
| <kbd>Fn</kbd> + <kbd>Return</kbd>   | <kbd>Insert</kbd> |
| <kbd>Fn</kbd> + <kbd>⌫</kbd>    | <kbd>Del</kbd>    |
| <kbd>Fn</kbd> + <kbd>P</kbd>    | <kbd>Print Screen</kbd> |
| <kbd>Fn</kbd> + <kbd>S</kbd>    | <kbd>Scroll Lock</kbd> |
| <kbd>Fn</kbd> + <kbd>B</kbd>    | <kbd>Pause/Break</kbd> |
| <kbd>Fn</kbd> + <kbd>&uarr;</kbd>   | <kbd>Page Up</kbd> |
| <kbd>Fn</kbd> + <kbd>&darr;</kbd>   | <kbd>Page Down</kbd> |
| <kbd>Fn</kbd> + <kbd>&larr;</kbd>   | <kbd>Home</kbd>   |
| <kbd>Fn</kbd> + <kbd>&rarr;</kbd>   | <kbd>End</kbd>    |

## Keymap Programming

> **Note:** This is an advanced feature and mostly suited to people with a programming background. If you do not feel that you have programming skills you may have difficulty in getting things right or you may even render the device slow and unresponsive if done something terribly wrong. If that happens there is always the [reset](#Factory-Resetting-the-Device) option.

The magicstick.io keymap is programmable via custom rules. This allows you to: 
- Physical key remapping. 
- Remap keys to target the majority of the HID Keyboard scan codes as per USB HID Usage Tables specification 1.12, under the Keyboard/Keypad and Consumer Pages, totalling 200+ of keys and functions.
- Program keys for typing extended ASCII characters, unicode characters and emojis.

To access the default key map, right-click on the utility icon and select Keymap to open the keymap editor.

![](image-1.png)

You will then see the keymap rules editor showing the current default rules: 

![alt text](image-10.png)

A keymap rule can be one of the following 3:

1. **label** [label name]
2. **goto** [label name]
3. [**expression**] **:** [goto **label** if expression evaluates to true] **:** [goto **label** if expression evaluates to false]

   _or_

   [**expression**] **:** [goto **label** if expression evaluates to true]

   _or_

   [**expression**]

(1) A **label** rule defines a place/anchor in the program. The label name can be a word consisting of alphanumeric characters and underscores but starting with an underscore or a letter. E.g. **lbl_1**, **_lbl1**, **lastlbl**, etc.

(2) A **goto** rule tells the rules engine to jump to a particular label location in the list, by label name.

(3) An expression rule, executes and evaluates the result of an expression. If the expression result is true (i.e. any number except 0) it jumps to the rules list location specified by [goto **label** if expression evaluates to true]. If the result is false (equals 0), it jumps to the rules list location specified by [goto **label** if expression evaluates to false]. The goto sections are optional and if they are missing, execution will just continue with the next rule in the list until the end of the list.

Below is a further explanation of the default rules:

![alt text](rules1.drawio.png)


### Physical Key Remapping

magicstick.io supports the remapping of a physical key via keymap rules. For instance, for swapping the blue and red circled keys.

![](20230928220051.png)

The default keymap already includes the following 2 rules using the **ch_key** function:

```
ch_key(HID_KEY_EUROPE_2, HID_KEY_GRAVE):end
ch_key(HID_KEY_GRAVE, HID_KEY_EUROPE_2):end
```

You can remove these two rules if you would prefer to not swap these keys.

#### Deleting all the Remapped Keys

To delete all the remapped keys one-off, you can:
1. Click the **Load Default** button in the keymap editor to load the default keymap.
Or
2. Reset the device to factory settings by following [these steps](#Factory-Resetting-the-Device).

#### Remapping of Special Keys

##### Swap Fn-Ctrl

This can be easily done in **Settings**.

##### Swap Alt-Cmd

This can be easily done in **Settings** by selecting:

![alt text](image-2.png)

Alternatively, you can code the rules in the key map editor. This will allow you more fine-grained control, such as to only swap the Left or the Right Alt-Cmd keys, etc.

Rule to swap left <kbd>⌥ Alt/Option</kbd> with left <kbd>⌘ Cmd</kbd>:

```
(mod & KEYBOARD_MODIFIER_LEFTALT) && set_mod((mod & ~KEYBOARD_MODIFIER_LEFTALT) | KEYBOARD_MODIFIER_LEFTGUI)
```

The above rule says if the pressed modifiers match the **KEYBOARD_MODIFIER_LEFTALT**, then remove the **KEYBOARD_MODIFIER_LEFTALT** and add the **KEYBOARD_MODIFIER_LEFTGUI**.

Here is a detailed breakdown of the above rule expression:
![alt text](image-5.png)

Rule to swap left <kbd>⌘ Cmd</kbd> with left <kbd>⌥ Alt/Option</kbd>:
```
(mod & KEYBOARD_MODIFIER_LEFTGUI) && set_mod((mod & ~KEYBOARD_MODIFIER_LEFTGUI) | KEYBOARD_MODIFIER_LEFTALT)
```
The above rule says if the pressed key(s) modifiers match the **KEYBOARD_MODIFIER_LEFTGUI**, then remove the **KEYBOARD_MODIFIER_LEFTGUI** and add the **KEYBOARD_MODIFIER_LEFTALT**.

You can add 2 similar rules to swap the **KEYBOARD_MODIFIER_RIGHTALT** and **KEYBOARD_MODIFIER_RIGHTGUI** keys.

### Emulating Numeric Keypad Number Keys

Emulating these keys can be useful for entering Alt-codes under Windows, that require the use of a numeric keypad, which the Apple Magic keyboard does not have.

The following set of rules shows how to map the <kbd>Fn</kbd> + <kbd>0 - 9</kbd> key combinations to: Numeric Keypad keys <kbd>0 - 9</kbd>:

```
ch_key(HID_KEY_1, HID_KEY_KEYPAD_1):end
ch_key(HID_KEY_2, HID_KEY_KEYPAD_2):end
ch_key(HID_KEY_3, HID_KEY_KEYPAD_3):end
ch_key(HID_KEY_4, HID_KEY_KEYPAD_4):end
ch_key(HID_KEY_5, HID_KEY_KEYPAD_5):end
ch_key(HID_KEY_6, HID_KEY_KEYPAD_6):end
ch_key(HID_KEY_7, HID_KEY_KEYPAD_7):end
ch_key(HID_KEY_8, HID_KEY_KEYPAD_8):end
ch_key(HID_KEY_9, HID_KEY_KEYPAD_9):end
ch_key(HID_KEY_0, HID_KEY_KEYPAD_0):end
```

These rules must be entered after the "label lbl_fn_on" line and before the "goto end" line, so that they are taken into consideration when <kbd>Fn</kbd> is pressed.

### Entering Unicode Characters and Emojis

Please note that this currently only works in Windows, as it relies on the magicstick-ui utility. Also, the program that you are typing in to must have Unicode support (e.g. Windows WordPad or Microsoft Word).

For entering **Unicode** characters, you need to know the decimal Unicode point value of the character. You can use [these tables](https://www.quackit.com/character_sets/unicode/versions/unicode_9.0.0/) for that and take the number from the **Decimal** column value (without the other characters around it).

The following example shows how to program the key shortcut <kbd>Fn</kbd> + <kbd>2</kbd> to type the **€** character:

```
find_key(HID_KEY_2) && send_unicode(8364):end
```

The following example shows how to program the key shortcut <kbd>Fn</kbd> + <kbd>3</kbd> to type the **£** character:

```
find_key(HID_KEY_3) && send_unicode(8356):end
```

You will need to add both of the above rules after the "label lbl_fn_on" line and before the "goto end" line so that they are activated when <kbd>Fn</kbd> is pressed, as seen in lines 23-24:
![alt text](image-8.png)

You can also type Emojis, for example say that you would like to map <kbd>Fn</kbd> + <kbd>Y</kbd> to 👍 and <kbd>Fn</kbd> + <kbd>N</kbd> to 👎:

```
find_key(HID_KEY_Y) && send_unicode(128077):end
find_key(HID_KEY_N) && send_unicode(128078):end
```

This is a more complex example that would allow you to map: 
<kbd>Fn</kbd> + ![alt text](image-9.png) to Unicode character ≠
<kbd>Fn</kbd> + <kbd>Shift</kbd> + ![alt text](image-9.png) to Unicode character ±

```
!mod && find_key(HID_KEY_EQUAL) && send_unicode(8800):end
(mod & KEYBOARD_MODIFIER_LEFTSHIFT) && find_key(HID_KEY_EQUAL) && send_unicode(177):end
```

Note how we also need to consult the **mod** keystroke modifiers (flags) variable value to check whether the <kbd>Shift</kbd> key is pressed or not.

## Firmware Updates

#### Firmware Update Using the magicstick-ui Utility
This is the recommended way as it is easier than the manual one but you need to have access to a Windows PC to run magicstick-ui.

1. Right-click on the magicstick-ui tray icon and select: _Check for updates_. 

2. If a new update is found, you will get a confirmation dialog asking to update. Accept, and the upgrade will start and complete automatically. 

   ![](20230927211852.png)

3. The device will automatically reboot to the updated version.

#### Manual Firmware Update
This _"brute force"_ method is useful if you have no access to a Windows PC or if for any reason the device had been previously flashed with a bad, non-working firmware (bricked).

1. To download the firmware for your magicstick.io device, you need your device's serial number. On **Windows**, use the magicstick-ui utility to retrieve it. On **Linux**, you can find it by typing this command in a terminal: ```upower -d```
The content in the red box is the device's serial number.

   ![](20230928223602.png)

So in that case, the serial number starts with E66 and finishes with 32.

2. Download the latest magicstick.io firmware by opening the following link in your browser, but by first replacing the word SERIAL with your actual serial number: ```https://magicstick-app.azurewebsites.net/api/download/SERIAL/magicstick-latest.uf2``` 
(E.g. ```https://magicstick-app.azurewebsites.net/api/download/E66138468234AA31/magicstick-latest.uf2```)
3. Enter magicstick.io into [BOOTSEL mode](#Entering-into-BOOTSEL-Mode). 
4. Once magicstick.io enters BOOTSEL mode, a new **File Explorer** window will open in your desktop, titled: **RPI-RP2**. If this window does n't open automatically, you can still open it manually in **File Explorer**:

   ![](20230927212729.png)
   
   ![](20230927212434.png)

6. Copy the downloaded **magicstick-latest.uf2** firmware file there. Once the copy completes, your magicstick.io device will automatically restart running the new firmware.

## Entering into BOOTSEL Mode

BOOTSEL is a special device mode that allows you to write new firmware to it. You should only need to do this to run a manual firmware update or downgrade.

To enter into BOOTSEL mode, unplug magicstick.io and then plug it in while the bootsel button is constantly pressed. This will enter the magicstick.io into bootsel mode.

## Factory Resetting the Device

You can reset your device's internal memory (programmed keys, Bluetooth pairing etc.) by following these steps:

Unplug the device. Plug it in and as soon as the green LED turns on (it is important to wait until it turns on before you press), press the BOOTSEL button and keep pressing it until the LED starts flashing. Now release the BOOTSEL button. The device's memory will be wiped out and the device will reboot.

It is important to press the BOOTSEL button **after** the LED turns on. If you press it before, the device will enter into BOOTSEL mode instead of resetting, which is not what you want in this case.

As soon as the device resets back to factory settings, it will lose all key remapping information, special keys configuration, as well as any Bluetooth pairing information if it was previously paired with a Bluetooth keyboard. Therefore it will immediately enter into Bluetooth discovery mode again, trying to find a suitable keyboard to pair with.





