using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Couchbase.KeyValue.ExpressionVisitors;
using Couchbase.Core.IO.Serializers;
using Xunit;

namespace Couchbase.UnitTests.KeyValue.ExpressionVisitors
{
    public class SubDocumentPathExpressionVisitorTests
    {
        #region Properties/Fields

        [Fact]
        public void Visit_Field_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringField;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringField`", result);
        }

        [Fact]
        public void Visit_Property_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringProperty`", result);
        }

        [Fact]
        public void Visit_NullableProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, int?>> expression =
                p => p.NullableProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`nullableProperty`", result);
        }

        [Fact]
        public void Visit_NullablePropertyWithValue_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, int>> expression =
                p => p.NullableProperty.Value;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`nullableProperty`", result);
        }

        [Fact]
        public void Visit_Dynamic_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, dynamic>> expression =
                p => p.DynamicProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`dynamicProperty`", result);
        }

        #endregion

        #region Arrays/Lists

        [Fact]
        public void Visit_ListIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringList[0];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringList`[0]", result);
        }

        [Fact]
        public void Visit_IListIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[0];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[0]", result);
        }

        [Fact]
        public void Visit_ListNegativeIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[-1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[-1]", result);
        }

        [Fact]
        public void Visit_ListCalculatedIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[2 + 1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[3]", result);
        }

        [Fact]
        public void Visit_ListExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[index];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[5]", result);
        }

        [Fact]
        public void Visit_ListNegatedExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[-index];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[-5]", result);
        }

        [Fact]
        public void Visit_ListExternalVariableAndCalculatedIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[index + 1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[6]", result);
        }

        [Fact]
        public void Visit_ListExternalVariableConditionalIndex_ReturnsPath()
        {
            // Arrange

            bool b = true;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[b ? 1 : -1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIList`[1]", result);
        }

        [Fact]
        public void Visit_ListAsMainDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<List<Document>, Document>> expression =
                p => p[2];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("[2]", result);
        }

        [Fact]
        public void Visit_ListAsMainDocumentSubProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<List<Document>, string>> expression =
                p => p[2].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("[2].`stringProperty`", result);
        }

        [Fact]
        public void Visit_ListAsMainDocumentNoIndexer_ReturnsEmptyPath()
        {
            // Note: used for mutation subdocument commands such as array insert
            // where the top-level document is a JSON array

            // Arrange

            Expression<Func<List<Document>, List<Document>>> expression =
                p => p;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("", result);
        }

        #endregion

        #region Dictionaries

        [Fact]
        public void Visit_DictionaryIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringDictionary["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringDictionary`.`key`", result);
        }

        [Fact]
        public void Visit_IDictionaryIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIDictionary`.`key`", result);
        }

        [Fact]
        public void Visit_DictionaryCalculatedIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary["key" + "part"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIDictionary`.`keypart`", result);
        }

        [Fact]
        public void Visit_DictionaryExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var key = "key";
            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary[key];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIDictionary`.`key`", result);
        }

        [Fact]
        public void Visit_DictionaryExternalVariableAndCalculatedIndex_ReturnsPath()
        {
            // Arrange

            var key = "key";
            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary[key + "path"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`stringIDictionary`.`keypath`", result);
        }

        [Fact]
        public void Visit_DictionaryAsMainDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<Dictionary<string, Document>, Document>> expression =
                p => p["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`key`", result);
        }

        [Fact]
        public void Visit_DictionaryAsMainDocumentSubProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Dictionary<string, Document>, string>> expression =
                p => p["key"].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`key`.`stringProperty`", result);
        }

        #endregion

        #region Subdocuments

        [Fact]
        public void Visit_SubDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, Document>> expression =
                p => p.SubDocument;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`subDocument`", result);
        }

        [Fact]
        public void Visit_SubDocumentProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.SubDocument.StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`subDocument`.`stringProperty`", result);
        }

        [Fact]
        public void Visit_SubDocumentInListProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.SubDocumentList[1].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.Equal("`subDocumentList`[1].`stringProperty`", result);
        }

        #endregion

        #region Exceptions

        [Fact]
        public void Visit_NullSerializer_ThrowsException()
        {
            // Arrange


            Expression<Func<Document, string>> expression =
                p => "key";

            // Act/Assert

            var result = Assert.Throws<ArgumentNullException>(
                () => SubDocumentPathExpressionVisitor.GetPath((IExtendedTypeSerializer) null, expression));

            Assert.Equal("serializer", result.ParamName);
        }

        [Fact]
        public void Visit_NullPath_ThrowsException()
        {
            // Act/Assert

            var result = Assert.Throws<ArgumentNullException>(
                () => SubDocumentPathExpressionVisitor.GetPath<Document, Document>(new DefaultSerializer(), null));

            Assert.Equal("path", result.ParamName);
        }

        [Fact]
        public void Visit_DoesntUseParameter_ThrowsException()
        {
            // Arrange


            Expression<Func<Document, string>> expression =
                p => "key";

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Fact]
        public void Visit_ParameterAsArrayIndex_ThrowsException()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringDictionary[p.StringProperty];

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Fact]
        public void Visit_NonSimplifableMethodCall_ThrowsException()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringField.ToUpper();

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Fact]
        public void Visit_TwoDimensionalIndexedProperty_ThrowsException()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.SubDocument[1, 2];

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        #endregion

        #region Escaping

        [Fact]
        public void WriteEscapedString_BasicString_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string");

            // Assert

            Assert.Equal("`string`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithBacktick_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("str`ing");

            // Assert

            Assert.Equal("`str``ing`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithNewline_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\n");

            // Assert

            Assert.Equal(@"`string\n`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithCarriageReturn_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\r");

            // Assert

            Assert.Equal(@"`string\r`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithTab_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\t");

            // Assert

            Assert.Equal(@"`string\t`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithBackslash_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\\");

            // Assert

            Assert.Equal(@"`string\\`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithFormFeed_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\f");

            // Assert

            Assert.Equal(@"`string\f`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithBackspace_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\b");

            // Assert

            Assert.Equal(@"`string\b`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithDoubleQuote_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\"");

            // Assert

            Assert.Equal(@"`string\""`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithSingleQuote_WritesString()
        {

            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\'");

            // Assert

            Assert.Equal(@"`string\'`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithNull_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\0");

            // Assert

            Assert.Equal(@"`string\u0000`", result);
        }

        [Fact]
        public void WriteEscapedString_StringWithUnicode_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\u1234");

            // Assert

            Assert.Equal(@"`string\u1234`", result);
        }

        #endregion

        #region Helpers

        public class Document
        {
            public string StringField;
            public string StringProperty { get; set; }
            public Document SubDocument { get; set; }
            public int? NullableProperty { get; set; }
            public dynamic DynamicProperty { get; set; }

            public string[] StringArray { get; set; }
            public List<string> StringList { get; set; }
            public IList<string> StringIList { get; set; }
            public List<Document> SubDocumentList{ get; set; }

            public Dictionary<string, string> StringDictionary { get; set; }
            public IDictionary<string, string> StringIDictionary { get; set; }
            public Dictionary<string, Document> SubDocumentDictionary { get; set; }

            public string this[int i, int j]
            {
                get
                {
                    return StringArray[i];
                }
            }
        }

        #endregion
    }
}
