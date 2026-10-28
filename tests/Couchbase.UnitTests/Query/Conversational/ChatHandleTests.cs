using Couchbase.Core.Exceptions;
using Couchbase.Query.Conversational;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational
{
    public class ChatHandleTests
    {
        [Fact]
        public void ToJson_WithOwner_WritesBothFields()
        {
            Assert.Equal("""{"chatId":"chat-1","owner":"uuid-a"}""", new ChatHandle("chat-1", "uuid-a").ToJson());
        }

        [Fact]
        public void ToJson_WithoutOwner_WritesNull()
        {
            Assert.Equal("""{"chatId":"chat-1","owner":null}""", new ChatHandle("chat-1", null).ToJson());
        }

        [Theory]
        [InlineData("uuid-a")]
        [InlineData(null)]
        public void FromJson_ToJson_RoundTrips(string? owner)
        {
            var handle = new ChatHandle("23a2c880-4566-4f78-9e81-86ea755b2a7f", owner);

            Assert.Equal(handle, ChatHandle.FromJson(handle.ToJson()));
        }

        [Fact]
        public void FromJson_EscapedCharacters_RoundTrip()
        {
            var handle = new ChatHandle("chat \"1\" <é>", "uuid\\a");

            Assert.Equal(handle, ChatHandle.FromJson(handle.ToJson()));
        }

        [Fact]
        public void FromJson_IgnoresUnknownFieldsAndOrder()
        {
            var handle = ChatHandle.FromJson("""
                {
                  "version": 2,
                  "owner": "uuid-a",
                  "extra": { "nested": [1, { "chatId": 5 }] },
                  "chatId": "chat-1",
                  "list": []
                }
                """);

            Assert.Equal(new ChatHandle("chat-1", "uuid-a"), handle);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        [InlineData("""{"chatId":"chat-1","owner":null""")]
        [InlineData("""{"chatId":"chat-1","owner":null} x""")]
        [InlineData("""{"chatId":"chat-1","owner":null}{}""")]
        [InlineData("""{"chatId":"chat-1",}""")]
        [InlineData("""{"chatId":"chat-1",/* c */"owner":null}""")]
        [InlineData("[]")]
        [InlineData("\"chat-1\"")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("""{"owner":"uuid-a"}""")]
        [InlineData("""{"chatId":"chat-1"}""")]
        [InlineData("""{"chatId":null,"owner":"uuid-a"}""")]
        [InlineData("""{"chatId":"","owner":"uuid-a"}""")]
        [InlineData("""{"chatId":5,"owner":"uuid-a"}""")]
        [InlineData("""{"chatId":["chat-1"],"owner":"uuid-a"}""")]
        [InlineData("""{"chatId":"chat-1","owner":5}""")]
        [InlineData("""{"chatId":"chat-1","owner":false}""")]
        [InlineData("""{"chatId":"chat-1","owner":{"uuid":"a"}}""")]
        [InlineData("""{"ChatId":"chat-1","Owner":"uuid-a"}""")]
        public void FromJson_InvalidHandle_ThrowsInvalidArgument(string json)
        {
            Assert.Throws<InvalidArgumentException>(() => ChatHandle.FromJson(json));
        }

        [Fact]
        public void FromJson_Null_ThrowsInvalidArgument()
        {
            Assert.Throws<InvalidArgumentException>(() => ChatHandle.FromJson(null!));
        }

        [Fact]
        public void ToString_ShowsTheFields()
        {
            Assert.Equal("ChatHandle { ChatId = chat-1, Owner = uuid-a }", new ChatHandle("chat-1", "uuid-a").ToString());
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
