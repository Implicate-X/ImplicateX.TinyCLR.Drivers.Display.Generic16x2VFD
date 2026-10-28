using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace ImplicateX.TinyCLR
{
	public static class Text
	{
		public static string Reverse( string text )
		{
			if( text == null )
			{
				return string.Empty;
			}

			var chars = text.ToCharArray();

			for( var i = 0; i < chars.Length / 2; i++ )
			{
				var temp = chars[ i ];
				chars[ i ] = chars[ chars.Length - 1 - i ];
				chars[ chars.Length - 1 - i ] = temp;
			}

			return new string( chars );
		}

		public static string PadRight( string text, int totalWidth )
		{
			if( text == null )
			{
				text = string.Empty;
			}

			if( text.Length >= totalWidth )
			{
				return text.Substring( 0, totalWidth );
			}

			var result = new char[ totalWidth ];

			for( var i = 0; i < text.Length; i++ )
			{
				result[ i ] = text[ i ];
			}

			for( var i = text.Length; i < totalWidth; i++ )
			{
				result[ i ] = ' ';
			}

			return new string( result );
		}
	}
}
