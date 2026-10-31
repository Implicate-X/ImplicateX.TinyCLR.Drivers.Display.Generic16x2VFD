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

	private static GpioController gpioController = null!;
	private static Generic16x2VFD vfd = null!;

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

		vfd.SetLines( "Radio 2 98.9 MHz", "Broadcast for you and me" );
		vfd.SetScrollDelay( 200 );

		Debug.WriteLine( "VFD scrolling start" );

		string line2 = string.Empty;
		// 136 - 141 Level 0 bis 5
		// 30 = Pfeil nach oben, 31 = Pfeil nach unten
		// 126 = Pfeil nach rechts, 127 = Pfeil nach links
		// 128 = Einzelblockpfeil nach oben, 129 = Doppelblockpfeil nach oben
		// 144 = Einzelblockpfeil nach unten, 145 = Doppelblockpfeil nach unten

		for( int i = 136; i <= 141; i++ )
		{
			line2 += ( char )i;
		}
		vfd.SetLine2( line2 );

		Thread vfdThread = new( () => vfd.Run() );

		vfdThread.Start();


		#endregion
	}
}
