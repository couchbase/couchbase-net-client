using System;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Transcoders
{
	[TestFixture]
	public class CustomTranscoderTests
	{
		private class TestStringTranscoder : DefaultTranscoder, ITypeTranscoder
		{
			Flags ITypeTranscoder.GetFormat<T>(T value)
			{
				if (value == null || value.GetType() != typeof(string))
				{
					throw new InvalidOperationException("This test transcoder is supposed to use string only");
				}
				return new Flags() { Compression = Compression.None, DataFormat = DataFormat.String, TypeCode = TypeCode.String };
			}
		}

		[Test]
		public void TestCustomTranscoder()
		{
			//passing string value wrapped in object should result in JSON object with default transcoder
			Add<object> add = new Add<object>("text_key", "text", null, new DefaultTranscoder(), 2500);

			byte[] expectedExtras = new byte[] { 0x02, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
			byte[] expectedBody = new byte[] { 0x22, 0x74, 0x65, 0x78, 0x74, 0x22 }; //note the extra 0x22 quotes for JSON text escaping
			DataFormat expectedFormat = DataFormat.Json;

			byte[] actualExtras = add.CreateExtras();
			Assert.AreEqual(expectedFormat, add.Format);
			Assert.AreEqual(expectedExtras, actualExtras);

			byte[] actualBody = add.CreateBody();
			Assert.AreEqual(expectedBody, actualBody);

			//passing string value wrapped in object should result in plain string value with our test transcoder
			add = new Add<object>("text_key", "text", null, new TestStringTranscoder(), 2500);

			expectedExtras = new byte[] { 0x04, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x00 };
			expectedBody = new byte[] { 0x74, 0x65, 0x78, 0x74 }; //note that there are no extra quote characters
			expectedFormat = DataFormat.String;

			actualExtras = add.CreateExtras();
			Assert.AreEqual(expectedFormat, add.Format);
			Assert.AreEqual(expectedExtras, actualExtras);

			actualBody = add.CreateBody();
			Assert.AreEqual(expectedBody, actualBody);
		}
	}
}
