#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Utilities;
using System.Xml;
//using Newtonsoft.Json.Converters;
using System.Text;
//#if !NET20 && (!SILVERLIGHT || WINDOWS_PHONE)
//using System.Xml.Linq;
//#endif

namespace Newtonsoft.Json
{
	/// <summary>
	/// Provides methods for converting between common language runtime types and JSON types.
	/// </summary>
	internal static class JsonConvert
	{
		/// <summary>
		/// Represents JavaScript's boolean value true as a string. This field is read-only.
		/// </summary>
		public static readonly string True = "true";

		/// <summary>
		/// Represents JavaScript's boolean value false as a string. This field is read-only.
		/// </summary>
		public static readonly string False = "false";

		/// <summary>
		/// Represents JavaScript's null as a string. This field is read-only.
		/// </summary>
		public static readonly string Null = "null";

		/// <summary>
		/// Represents JavaScript's undefined as a string. This field is read-only.
		/// </summary>
		public static readonly string Undefined = "undefined";

		/// <summary>
		/// Represents JavaScript's positive infinity as a string. This field is read-only.
		/// </summary>
		public static readonly string PositiveInfinity = "Infinity";

		/// <summary>
		/// Represents JavaScript's negative infinity as a string. This field is read-only.
		/// </summary>
		public static readonly string NegativeInfinity = "-Infinity";

		/// <summary>
		/// Represents JavaScript's NaN as a string. This field is read-only.
		/// </summary>
		public static readonly string NaN = "NaN";

		internal static readonly long InitialJavaScriptDateTicks = 621355968000000000;

		internal static DateTime ConvertJavaScriptTicksToDateTime(long javaScriptTicks)
		{
			DateTime dateTime = new DateTime((javaScriptTicks * 10000) + InitialJavaScriptDateTicks, DateTimeKind.Utc);

			return dateTime;
		}
	}
}