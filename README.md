# ImplicateX.TinyCLR.Drivers.Display.Generic16x2VFD

A GPIO-based driver for compatible 16x2 vacuum fluorescent displays (VFDs) running on TinyCLR OS. It provides two-line text output, automatic horizontal scrolling for long lines, configurable scroll speed, and adjustable brightness.

## Requirements

- .NET and TinyCLR OS 3.0.2.3000
- A compatible 16x2 VFD
- Four available GPIO pins for clock, data, chip select, and reset

## Usage

Create the driver with the GPIO controller and the pin IDs connected to the display. Set the line contents and start `Run()` on a dedicated thread; it continuously refreshes the display.

```csharp
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Gpio;
using ImplicateX.TinyCLR.Drivers.Display;

var gpio = GpioController.GetDefault();
var display = new Generic16x2VFD(
    gpio,
    vfdClockPinId: clockPin,
    vfdDataPinId: dataPin,
    vfdCSPinId: chipSelectPin,
    vfResetPinId: resetPin);

display.SetLines("Hello, TinyCLR!", "16x2 VFD display");
display.SetScrollDelay(200);

var displayThread = new Thread(() => display.Run());
displayThread.Start();
```

Use `SetLine1`, `SetLine2`, or `SetLines` to update the text. Text longer than 16 characters scrolls automatically. Set `display.Brightness.Value` to a value from `0x10` to `0xF0` to change the brightness.

## TinyCLR OS

Designed for embedded development using .NET and TinyCLR OS 3.0.2.3000.