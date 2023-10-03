[![magicstick-logo](docs/magicstick-logo.png)](https://github.com/samartzidis/magicstick.io)
###### Apple Keyboards USB Adapter for PC

# About

MagicStick.io is a USB stick adapter to connect any Apple (Magic 1, Magic 2 or the older Apple Wireless) keyboard to a PC, providing the correct keymap translation for PCs. 

That is, you get a working _Delete_, _Ctrl_, _Page Up/Down_, _Print Screen_, Multimedia keys etc. You also get dual keyboard connection modes. Both wired and Bluetooth. All modern versions of Windows (since Windows 95 OSR2) and Linux are supported. Although most recent Linux distros already support Apple keyboard devices natively.

Additionally, any device that accepts conventional USB keyboards should work with it, such as game consoles or smart TVs.
  
<table>
<body>
  <tr>
    <td>
    <a href="docs/front.png"><img src="docs/front_tn.png" alt=""></a>
    </br>
    Keyboard port
    </td>
    <td>
    <a href="docs/side.png"><img src="docs/side_tn.png" alt=""></a>
    </br>
    Side view
    </td>
    <td>
    <a href="docs/wired.png"><img src="docs/wired_tn.png" alt=""></a>
    </br>
    On wired</br>connection
    </td>
  </tr>
  <tr>
    <td>
    <a href="docs/wireless.png"><img src="docs/wireless_tn.png" alt=""></a>
    </br>
    On wireless</br>BT connection
    </td>
    <td>
    <a href="docs/open-1.png"><img src="docs/open-1_tn.png" alt=""></a>
    </br>
    Inside
    </td>    
    <td>
    <a href="docs/open-2.png"><img src="docs/open-2_tn.png" alt=""></a>
    </br>
    Inside
    </td>
  </tr>
  </body>
</table>

This project started as a spare-time hobby, driven by my need to connect the ultra high quality but "eccentric" Apple keyboards to my PC, without having to develop any Windows kernel mode driver. This became reality after discovering the potential (speedy cores, powerful PIO subsystem, Bluetooth chip and software stack) of the RP2040 Pico board. The plastic case was designed together with my 6 y.o. daughter in the adult and children friendly 3dslash! :)

You can order a hand-made "plug-and-play" MagicStick.io device from: 

[![magicstick-logo](docs/etsy.png)](https://www.etsy.com/shop/MagicStickIO)

Software stuff, such as documentation, utilities and firmware updates are here.

## User Manual

The user manual is [here](docs/README.md).

## Supported Apple Keyboard Models

| Model | Status |
| -------- | ------- |
| A1314 | Old keyboard. Supported but no UI battery level indicator in MagicstickUI. |
| A1644 | Fully supported. |
| A2450 | Fully supported. |
| A2449 | Supported - but no fingerprint sensor (yet!). |

## Features

- MagicStick.io is powered by a **133MHz dual-core Arm Cortex M0+** processor. All processing logic is implemented in **optimized C/C++ and assembly** code and is utilizing both processor cores. The dual USB stack is managed by the first core and the Bluetooth stack is managed by the second core.
- MagicStick.io can function **both wired and wirelessly**. You can connect your Apple keyboard either via a standard USB to Lightning cable or wirelessly via Bluetooth. 
- You can freely switch between wired or wireless connection modes at any time.
- Wired and Bluetooth operation modes provide **surprisingly fast response times**. Tests performed with online measurement tools could not detect any extra delays over the default 16ms rebounce delay of the A1644 keyboard).

  ![](docs/20231001222021.png)
  
  _The above measurement was done on [clickspeedtester.com](https://www.clickspeedtester.com) using an A1644 keyboard. It averaged the same between (all) MagicStick-wired, MagicStick-Bluetooth, and direct PC USB (that is without MagicStick and with no extra Windows drivers installed)_.
- MagicStick.io is a microcontroller-based device so it **works immediately** as soon as it is powered on. This allows you to use the keyboard as early as at the PC boot process, e.g. for accessing the BIOS/UEFI menus. Also since there is no Operating System driver required, the keyboard just works correctly in BIOS/UEFI mode.
- MagicStick.io is **programmable** so that you can reposition almost all keys as you like.
- MagicStick.io supports an OS **battery level indicator** in both wired and wireless connection modes in both **Windows** and **Linux**. Ubuntu Linux natively supports a battery-level indicator whereas for Windows you can use the MagicStickUI utility.
- MagicStick.io is built with **security** in mind. It is locked down to standard keyboard HID API on the side that connects to the PC. The Bluetooth connection uses a licensed [BlueKitchen](https://bluekitchen-gmbh.com/) Bluetooth stack with Level 2 security enabled (wireless encryption). Additionally, Bluetooth can be disabled if needed.
- In contrast to other similar solutions (e.g. MagicUtilities), MagicStick.io has **no subscription fees** or any connected device restrictions. You own the device, and you can connect it to **as many keyboards or computers** as you like.
- **PC sleep/wake-up** is supported and works **in both wired and Bluetooth** connection modes.
- **Firmware updates**. Any future improvements, such as support for new keyboard models are easy to install and provided for free.


## Compliance and Safety

Technically, MagicStick.io just is a programmed Raspberry Pi Pico W microcontroller. You can refer to this official link for the details on the [compliance and safety approvals](https://pip.raspberrypi.com/categories/688) of the Raspberry Pi Pico W.

## Disclaimer

MagicStick.io was professionally developed with love and attention to detail, following software engineering best practices. There is no 100% guarantee however that it will work for your particular setup neither I accept responsibility for anything going wrong to your equipment (including explosions, earthquakes and floods) or to you directly or indirectly through its use. By accepting to use the device and related software you also accept full responsibility for all of the above. Obviously if you get a MagicStick.io device from Etsy it is returnable and refundable based on the associated Etsy policy.


 







