![magicstick-logo](docs/magicstick-logo.png)
###### Apple Keyboard USB Adapter for PCs

# MagicStick.io

MagicStick is a USB stick adapter to connect any Apple (Magic 1, Magic 2 or the older Apple Wireless) keyboard to a PC, providing the correct keymap translation for PCs. That is, you get a working Delete, Ctrl, Page Up/Down, Print Screen, Multimedia keys etc. You also get dual keyboard connection modes, wired and Bluetooth. All modern versions of Windows and Linux are supported.

Additionally, any device that accepts conventional USB keyboards should work with it, such as game consoles or smart TVs.

MagicStick started as a personal hobby project, inspired by my need to connect the excellent quality but "eccentric" Apple keyboard peripherals to a PC, without having to develop any special kernel-mode drivers and after realizing the tremendous potential (e.g. highly clocked multiple cores, powerful PIO subsystem, Bluetooth chip) of the Raspberry Pi Pico/RP2040 chip. The plastic case on the Etsy Shop was proudly designed by my 6 year old daughter with some help from me.

You can visit the [MagicStick Etsy Page](https://www.etsy.com/your/shops/MagicStickShop) to order a hand-made MagicStick device. The rest of the software stuff, such as documentation, utilities and firmware updates are provided here.

## User Manual

The user manual is [here](docs/README.md).

## Currently Supported Apple Keyboard Models

- A1314 (no battery level indicator)
- A1644
- A2450 
- A2449 (no fingerprint sensor capability yet)

## Features

- At the heart of MagicStick, is a Raspberry Pi Pico board with a **133MHz dual-core Arm Cortex M0+** processor. All processing logic is implemented in **optimized C/C++ and assembly** code, utilizing both processor cores.
- MagicStick can function **both wired and wirelessly**. You can connect your Apple keyboard either via a standard USB to Lightning cable or wirelessly via Bluetooth. You can freely switch between wired or wireless connection modes at any time.
- Wired operation provides **very fast response times** (measured just **1ms** of extra overhead on top of the standard response time of about 15ms of the A1644 keyboard). This makes the wired connection ideal for **gaming** users (1).
- MagicStick is a microcontroller-based device. It **works immediately** as soon as it is powered on. This allows you to use the keyboard as early as at the PC boot process, e.g. for accessing the BIOS/UEFI menus. Also since there is no Operating System driver required, the keyboard just works correctly in BIOS/UEFI mode.
- MagicStick is **programmable** so that you can reposition almost all keys as you like.
- MagicStick supports an OS **battery level indicator** in both wired and wireless connection modes in both **Windows** and **Linux**. Ubuntu Linux natively supports a battery-level indicator whereas for Windows you can use the MagicStickUI utility.
- MagicStick is built with **security** in mind. It is locked down to standard keyboard HID IO operations on the side that connects to the PC. The Bluetooth connection uses a licensed [BlueKitchen](https://bluekitchen-gmbh.com/) Bluetooth stack with Level 2 security enabled (wireless encryption).
- In contrast to other similar solutions (e.g. MagicUtilities), MagicStick has **no subscription fees** or any connected device restrictions. You own the device, and you can connect it to **as many keyboards or computers** as you like.
- **PC sleep/wake-up** is supported and works **in both wired and Bluetooth** connection modes.
- **Firmware updates**. Any future improvements, such as support for new keyboard models or the A2449 fingerprint sensor are easy and provided for free.

(1) Tests were carried out using a reference Apple Magic Keyboard 2 keyboard using an online utility. Other third-party measurements: https://www.notebookcheck.net/Are-gaming-keyboards-really-faster-than-conventional-keyboards.258470.0.html

## Compliance and Safety

Technically, MagicStick just is a reprogrammed Raspberry Pi Zero device. Please refer to this official link for further details on the compliance and safety of the Raspberry Pi Pico device: https://pip.raspberrypi.com/categories/688

## Disclaimer

MagicStick was developed with love and dedication following software engineering best practices by a professional software engineer. There is no guarantee however that it will work for your particular setup neither I accept responsibility for anything going wrong in your equipment or you through its use. MagicStick is not designed for critical systems use. By accepting to use the device you also accept full responsibility for all of the above. 

MagicStick hand-made Etsy devices are returnable and refundable as per the Etsy shop policy.







