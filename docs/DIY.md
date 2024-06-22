## Build Your Own Device Instructions

This process is not 100% free, but it may be cheaper than purchasing a ready-made magicstick.io device, especially if you already own a *Raspberry Pi Pico W* board.

### Hardware

> If you are OK with only Bluetooth connectivity and you are not interested in the wired connectivity (implying no wired charging as well), you can skip (2).
If you are not interested in the plastic enclosure, you can skip (3). In that case you just need a *Raspberry Pi Pico W* board and jump straight to the Device Preparation section!

1. You need to purchase a genuine *Raspberry Pi Pico W* board from a vendor in your area. 
2. You also need a female USB Type-A port and some _28AWG_ silicone cable for the wiring. Use the wiring schematics [here](../schematics) to solder the USB Type-A port to the board as shown.
3. Use the 3D print STL files [here](../case) to print the plastic case.

### Device Preparation

For the following process, you will need to work on a Windows PC.

1. Download and install the [magicstick-ui](https://github.com/samartzidis/magicstick.io/releases). 

2. Run the magicstick-ui utility (from the desktop icon), then right click on its System Tray icon and select the "Initialize device" option. 

3. You will be asked to plug your "Raspberry Pi Pico W" device in [BOOTSEL mode](./README.md#entering-into-bootsel-mode). The device will be auto-detected flashed with the initialization firmware.

3. In the magicstick-ui Utility, click the "Scan devices" option and select the "magicstick-dummy..." device that now appears in the device list. Next, select the "Device info" option and copy the device's serial number.

4. **Purchase** your device firmware from [Etsy](https://www.etsy.com/uk/listing/1709718352/magicstickio-firmware), by supplying the 16-digit serial number you copied in the previous step in the order form.

5. Plug your "Raspberry Pi Pico W" device in BOOTSEL mode again, and now in Windows File Explorer, copy the downloaded firmware file directly to the "Raspberry Pi Pico W" device, which should appear on Windows as "RPI-RP2".

After step 5, the device will automatically restart as a new raspikey.io device.

