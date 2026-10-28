using System;
using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Gpio;

namespace ImplicateX.TinyCLR.Drivers.Display
{
	/// <summary>
	/// Provides low-level control of a two-line vacuum fluorescent display (VFD) over GPIO using manual clock, data,
	/// chip-select, and reset signaling, including automatic horizontal scrolling for text longer than the display width.
	/// </summary>
	/// <remarks>Maintains thread-safe line content and scroll state, transmits display frames continuously in
	/// <c>Run()</c>, and applies a minimum scroll delay of 20 ms. The update loop is long-running and intended to execute
	/// on a dedicated background thread.</remarks>
	public class Generic16x2VFD
	{
		private readonly GpioPin vfdClockPin;
		private readonly GpioPin vfdDataPin;
		private readonly GpioPin vfdCSPin;
		private readonly GpioPin vfResetPin;
		private const int LineLength = 16;
		private readonly string scrollPadding = new( ' ', LineLength );
		private string line1Text = string.Empty;
		private string line2Text = string.Empty;
		private int line1ScrollOffset;
		private int line2ScrollOffset;
		private long line1Revision;
		private long line2Revision;
		private int scrollDelayMs = 200;
		private readonly object syncRoot = new object();

		/// <summary>
		/// Initializes a new instance of the GenericVFD class, configures the VFD control pins as outputs, sets initial pin
		/// states, and performs a hardware reset sequence.
		/// </summary>
		/// <remarks>Sets clock and data low, chip-select high, then toggles reset high-low-high with delays to ensure
		/// the display starts in a known state.</remarks>
		/// <param name="gpioController">GPIO controller used to open and manage the VFD pins.</param>
		/// <param name="vfdClockPinId">GPIO pin number used for the VFD clock signal.</param>
		/// <param name="vfdDataPinId">GPIO pin number used for the VFD data signal.</param>
		/// <param name="vfdCSPinId">GPIO pin number used for the VFD chip-select signal.</param>
		/// <param name="vfResetPinId">GPIO pin number used for the VFD reset signal.</param>
		public Generic16x2VFD( GpioController gpioController, int vfdClockPinId, int vfdDataPinId, int vfdCSPinId, int vfResetPinId )
		{
			vfdClockPin = gpioController.OpenPin( vfdClockPinId );
			vfdDataPin = gpioController.OpenPin( vfdDataPinId );
			vfdCSPin = gpioController.OpenPin( vfdCSPinId );
			vfResetPin = gpioController.OpenPin( vfResetPinId );

			vfdClockPin.SetDriveMode( GpioPinDriveMode.Output );
			vfdDataPin.SetDriveMode( GpioPinDriveMode.Output );
			vfdCSPin.SetDriveMode( GpioPinDriveMode.Output );
			vfResetPin.SetDriveMode( GpioPinDriveMode.Output );

			vfdClockPin.Write( GpioPinValue.Low );
			vfdDataPin.Write( GpioPinValue.Low );
			vfdCSPin.Write( GpioPinValue.High );

			vfResetPin.Write( GpioPinValue.High );
			Thread.Sleep( 100 );
			vfResetPin.Write( GpioPinValue.Low );
			Thread.Sleep( 100 );
			vfResetPin.Write( GpioPinValue.High );
		}

		/// <summary>
		/// Continuously renders display frames for two lines of text, applies scrolling delays, and advances scroll offsets
		/// when the displayed content has not changed.
		/// </summary>
		/// <remarks>Runs indefinitely in a loop. Uses synchronization to read and update shared state safely, keeps
		/// each line’s scroll position stable when content revisions change, and enforces a minimum delay for single-frame
		/// content.</remarks>
		public void Run()
		{
			while( true )
			{
				string firstLine;
				string secondLine;
				int delayMs;
				int firstOffset;
				int secondOffset;
				long firstRevision;
				long secondRevision;

				lock( syncRoot )
				{
					firstLine = line1Text;
					secondLine = line2Text;
					delayMs = scrollDelayMs;
					firstOffset = line1ScrollOffset;
					secondOffset = line2ScrollOffset;
					firstRevision = line1Revision;
					secondRevision = line2Revision;
				}

				var frameCount = Math.Max( GetFrameCount( firstLine ), GetFrameCount( secondLine ) );

				SendFrame( firstLine, secondLine, firstOffset, secondOffset );
				Thread.Sleep( frameCount == 1 ? Math.Max( delayMs, 500 ) : delayMs );

				lock( syncRoot )
				{
					if( firstRevision == line1Revision && GetFrameCount( line1Text ) > 1 )
					{
						line1ScrollOffset = ( line1ScrollOffset + 1 ) % GetFrameCount( line1Text );
					}

					if( secondRevision == line2Revision && GetFrameCount( line2Text ) > 1 )
					{
						line2ScrollOffset = ( line2ScrollOffset + 1 ) % GetFrameCount( line2Text );
					}
				}
			}
		}

		/// <summary>
		/// Updates both display lines and refreshes change tracking only for lines whose text has changed.
		/// </summary>
		/// <remarks>Performs the update under a lock for thread safety. When a line changes, its scroll offset is
		/// reset and its revision counter is incremented.</remarks>
		/// <param name="firstLine">Text for the first line. <see langword="null"/> is treated as <see cref="string.Empty"/>.</param>
		/// <param name="secondLine">Text for the second line. <see langword="null"/> is treated as <see cref="string.Empty"/>.</param>
		public void SetLines( string firstLine, string secondLine )
		{
			lock( syncRoot )
			{
				firstLine = firstLine ?? string.Empty;
				secondLine = secondLine ?? string.Empty;

				if( !string.Equals( line1Text, firstLine ) )
				{
					line1Text = firstLine;
					line1ScrollOffset = 0;
					line1Revision++;
				}

				if( !string.Equals( line2Text, secondLine ) )
				{
					line2Text = secondLine;
					line2ScrollOffset = 0;
					line2Revision++;
				}
			}
		}

		/// <summary>
		/// Sets the text for line 1.
		/// </summary>
		/// <remarks>Applies the update only when the text changes, then resets the line 1 scroll offset and
		/// increments the line 1 revision.</remarks>
		/// <param name="line">Text to assign to line 1. If null, an empty string is used.</param>
		public void SetLine1( string line )
		{
			lock( syncRoot )
			{
				line ??= string.Empty;

				if( !string.Equals( line1Text, line ) )
				{
					line1Text = line;
					line1ScrollOffset = 0;
					line1Revision++;
				}
			}
		}

		/// <summary>
		/// Sets the text displayed on line 2.
		/// </summary>
		/// <remarks>Updates internal state only when the new text differs from the current value, resetting the line
		/// 2 scroll offset and incrementing the line 2 revision in a thread-safe section.</remarks>
		/// <param name="line">Text to display on line 2. If null, an empty string is used.</param>
		public void SetLine2( string line )
		{
			lock( syncRoot )
			{
				line ??= string.Empty;

				if( !string.Equals( line2Text, line ) )
				{
					line2Text = line;
					line2ScrollOffset = 0;
					line2Revision++;
				}
			}
		}

		/// <summary>
		/// Sets the delay between scroll updates, in milliseconds.
		/// </summary>
		/// <remarks>Updates the delay in a thread-safe manner.</remarks>
		/// <param name="milliseconds">Delay duration in milliseconds. Values less than 20 are clamped to 20.</param>
		public void SetScrollDelay( int milliseconds )
		{
			if( milliseconds < 20 )
			{
				milliseconds = 20;
			}

			lock( syncRoot )
			{
				scrollDelayMs = milliseconds;
			}
		}

		/// <summary>
		/// Calculates the number of frames required for the specified text based on its length and the configured line
		/// length.
		/// </summary>
		/// <param name="text">The text to evaluate.</param>
		/// <returns>1 when the text is null, empty, or its length is less than or equal to the line length; otherwise, the sum of the
		/// text length, the line length, and 1.</returns>
		private int GetFrameCount( string text )
		{
			if( string.IsNullOrEmpty( text ) || text.Length <= LineLength )
			{
				return 1;
			}

			return text.Length + LineLength + 1;
		}

		/// <summary>
		/// Sends a complete two-line display frame by issuing frame control windows and line windows for both lines.
		/// </summary>
		/// <remarks>Writes a frame start control window, then line windows for addresses 0x10 and 0x90, followed by a
		/// frame end control window.</remarks>
		/// <param name="line1">Text to render on the first line.</param>
		/// <param name="line2">Text to render on the second line.</param>
		/// <param name="offset1">Horizontal offset applied when composing the first line window.</param>
		/// <param name="offset2">Horizontal offset applied when composing the second line window.</param>
		private void SendFrame( string line1, string line2, int offset1, int offset2 )
		{
			SendWindow( [0x50, 0x80] );
			SendWindow( ComposeLineWindow( 0x10, line1, offset1 ) );
			SendWindow( ComposeLineWindow( 0x90, line2, offset2 ) );
			SendWindow( [0x70] );
		}

		/// <summary>
		/// Composes a byte buffer containing a target address followed by a fixed-length text window.
		/// </summary>
		/// <remarks>The returned buffer length is 1 plus the configured line length.</remarks>
		/// <param name="address">Address byte written to the first position of the output buffer.</param>
		/// <param name="text">Source text used to build the visible line window.</param>
		/// <param name="offset">Starting position used when building the line window from the source text.</param>
		/// <returns>A byte array whose first element is the address and remaining elements are the line window characters converted to
		/// bytes.</returns>
		private byte[] ComposeLineWindow( byte address, string text, int offset )
		{
			var bytes = new byte[ 1 + LineLength ];
			bytes[ 0 ] = address;

			var window = BuildWindow( text, offset );

			for( var i = 0; i < LineLength; i++ )
			{
				bytes[ i + 1 ] = ( byte )window[ i ];
			}

			return bytes;
		}

		/// <summary>
		/// Builds a fixed-width display window from the input text at the specified scroll offset.
		/// </summary>
		/// <remarks>If the source text fits within the line length, the value is right-padded and reversed.
		/// Otherwise, a window is taken from the padded scrolling source and reversed.</remarks>
		/// <param name="text">Source text used to build the window. Null is treated as an empty string.</param>
		/// <param name="offset">Zero-based starting index in the padded scrolling source used to fill the window.</param>
		/// <returns>A string with exactly the configured line length, padded as needed and reversed for display orientation.</returns>
		private string BuildWindow( string text, int offset )
		{
			text ??= string.Empty;

			if( text.Length <= LineLength )
			{
				return Text.Reverse( Text.PadRight( text, LineLength ) );
			}

			var source = scrollPadding + text + scrollPadding;
			var result = new char[ LineLength ];

			for( var i = 0; i < LineLength; i++ )
			{
				var sourceIndex = offset + i;
				result[ i ] = sourceIndex < source.Length ? source[ sourceIndex ] : ' ';
			}

			return Text.Reverse( new string( result ) );
		}

		/// <summary>
		/// Sends a sequence of bytes while the VFD chip-select line is asserted.
		/// </summary>
		/// <remarks>Drives chip-select low before transmission and high after all bytes are sent.</remarks>
		/// <param name="bytes">Bytes to transmit in order.</param>
		private void SendWindow( byte[] bytes )
		{
			vfdCSPin.Write( GpioPinValue.Low );

			for( var i = 0; i < bytes.Length; i++ )
			{
				SendByte( bytes[ i ] );
			}

			vfdCSPin.Write( GpioPinValue.High );
		}

		/// <summary>
		/// Sends a byte to the display interface one bit at a time, least significant bit first, pulsing the clock after each
		/// bit.
		/// </summary>
		/// <remarks>Writes each bit to the data pin and advances transmission with a clock pulse.</remarks>
		/// <param name="value">Byte value to transmit.</param>
		private void SendByte( byte value )
		{
			for( var bit = 0; bit < 8; bit++ )
			{
				vfdDataPin.Write( ( ( value >> bit ) & 0x01 ) != 0 ? GpioPinValue.High : GpioPinValue.Low );
				PulseClock();
			}
		}

		/// <summary>
		/// Generates a single clock pulse by driving the VFD clock GPIO pin high and then low.
		/// </summary>
		private void PulseClock()
		{
			vfdClockPin.Write( GpioPinValue.High );
			vfdClockPin.Write( GpioPinValue.Low );
		}
	}
}
