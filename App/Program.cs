using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;

namespace ImplicateX.TinyCLR.Drivers.Display;

internal class Program
{
	public static class ControllerPin
	{
		public const int VFReset = FEZFeather.GpioPin.PD6;
		public const int VFDCS = FEZFeather.GpioPin.PD5;
		public const int VFDData = FEZFeather.GpioPin.PD4;
		public const int VFDClock = FEZFeather.GpioPin.PD3;
	}

	private static GpioController gpioController;
	private static Generic16x2VFD vfd;

	static void Main()
	{
		gpioController = GpioController.GetDefault();

		#region Initialize 16x2 Vacuum Fluorescent Display (VFD)
		vfd = new(
			gpioController,
			vfdClockPinId: ControllerPin.VFDClock,
			vfdDataPinId: ControllerPin.VFDData,
			vfdCSPinId: ControllerPin.VFDCS,
			vfResetPinId: ControllerPin.VFReset );

		Debug.WriteLine( "VFD scrolling start" );

		vfd.SetLines( "Radio 2 98.9 MHz", "Broadcast for you and me" );
		vfd.SetScrollDelay( 200 );

		Thread vfdThread = new( () => vfd.Run() );

		vfdThread.Start();
		#endregion
	}
}
