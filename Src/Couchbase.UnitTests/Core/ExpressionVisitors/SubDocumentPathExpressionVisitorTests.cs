using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.ExpressionVisitors;
using Couchbase.Core.Serialization;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.ExpressionVisitors
{
    [TestFixture]
    public class SubDocumentPathExpressionVisitorTests
    {
        #region Properties/Fields

        [Test]
        public void Visit_Field_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringField;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringField`", result);
        }

        [Test]
        public void Visit_Property_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringProperty`", result);
        }

        [Test]
        public void Visit_NullableProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, int?>> expression =
                p => p.NullableProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`nullableProperty`", result);
        }

        [Test]
        public void Visit_NullablePropertyWithValue_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, int>> expression =
                p => p.NullableProperty.Value;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`nullableProperty`", result);
        }

        [Test]
        public void Visit_Dynamic_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, dynamic>> expression =
                p => p.DynamicProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`dynamicProperty`", result);
        }

        #endregion

        #region Arrays/Lists

        [Test]
        public void Visit_ListIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringList[0];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringList`[0]", result);
        }

        [Test]
        public void Visit_IListIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[0];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[0]", result);
        }

        [Test]
        public void Visit_ListNegativeIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[-1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[-1]", result);
        }

        [Test]
        public void Visit_ListCalculatedIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIList[2 + 1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[3]", result);
        }

        [Test]
        public void Visit_ListExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[index];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[5]", result);
        }

        [Test]
        public void Visit_ListNegatedExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[-index];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[-5]", result);
        }

        [Test]
        public void Visit_ListExternalVariableAndCalculatedIndex_ReturnsPath()
        {
            // Arrange

            var index = 5;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[index + 1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[6]", result);
        }

        [Test]
        public void Visit_ListExternalVariableConditionalIndex_ReturnsPath()
        {
            // Arrange

            bool b = true;

            Expression<Func<Document, string>> expression =
                p => p.StringIList[b ? 1 : -1];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIList`[1]", result);
        }

        [Test]
        public void Visit_ListAsMainDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<List<Document>, Document>> expression =
                p => p[2];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("[2]", result);
        }

        [Test]
        public void Visit_ListAsMainDocumentSubProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<List<Document>, string>> expression =
                p => p[2].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("[2].`stringProperty`", result);
        }

        [Test]
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

            Assert.AreEqual("", result);
        }

        #endregion

        #region Dictionaries

        [Test]
        public void Visit_DictionaryIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringDictionary["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringDictionary`.`key`", result);
        }

        [Test]
        public void Visit_IDictionaryIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIDictionary`.`key`", result);
        }

        [Test]
        public void Visit_DictionaryCalculatedIndex_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary["key" + "part"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIDictionary`.`keypart`", result);
        }

        [Test]
        public void Visit_DictionaryExternalVariableIndex_ReturnsPath()
        {
            // Arrange

            var key = "key";
            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary[key];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIDictionary`.`key`", result);
        }

        [Test]
        public void Visit_DictionaryExternalVariableAndCalculatedIndex_ReturnsPath()
        {
            // Arrange

            var key = "key";
            Expression<Func<Document, string>> expression =
                p => p.StringIDictionary[key + "path"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`stringIDictionary`.`keypath`", result);
        }

        [Test]
        public void Visit_DictionaryAsMainDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<Dictionary<string, Document>, Document>> expression =
                p => p["key"];

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`key`", result);
        }

        [Test]
        public void Visit_DictionaryAsMainDocumentSubProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Dictionary<string, Document>, string>> expression =
                p => p["key"].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`key`.`stringProperty`", result);
        }

        #endregion

        #region Subdocuments

        [Test]
        public void Visit_SubDocument_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, Document>> expression =
                p => p.SubDocument;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`subDocument`", result);
        }

        [Test]
        public void Visit_SubDocumentProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.SubDocument.StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`subDocument`.`stringProperty`", result);
        }

        [Test]
        public void Visit_SubDocumentInListProperty_ReturnsPath()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.SubDocumentList[1].StringProperty;

            // Act

            var result = SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression);

            // Assert

            Assert.AreEqual("`subDocumentList`[1].`stringProperty`", result);
        }

        #endregion

        #region Exceptions

        [Test]
        public void Visit_NullSerializer_ThrowsException()
        {
            // Arrange


            Expression<Func<Document, string>> expression =
                p => "key";

            // Act/Assert

            var result = Assert.Throws<ArgumentNullException>(() => SubDocumentPathExpressionVisitor.GetPath(null, expression));

            Assert.AreEqual("serializer", result.ParamName);
        }

        [Test]
        public void Visit_NullPath_ThrowsException()
        {
            // Act/Assert

            var result = Assert.Throws<ArgumentNullException>(
                () => SubDocumentPathExpressionVisitor.GetPath<Document, Document>(new DefaultSerializer(), null));

            Assert.AreEqual("path", result.ParamName);
        }

        [Test]
        public void Visit_DoesntUseParameter_ThrowsException()
        {
            // Arrange


            Expression<Func<Document, string>> expression =
                p => "key";

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Test]
        public void Visit_ParameterAsArrayIndex_ThrowsException()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringDictionary[p.StringProperty];

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Test]
        public void Visit_NonSimplifableMethodCall_ThrowsException()
        {
            // Arrange

            Expression<Func<Document, string>> expression =
                p => p.StringField.ToUpper();

            // Act/Assert

            Assert.Throws<NotSupportedException>(() => SubDocumentPathExpressionVisitor.GetPath(new DefaultSerializer(), expression));
        }

        [Test]
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

        [Test]
        public void WriteEscapedString_BasicString_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string");

            // Assert

            Assert.AreEqual("`string`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithBacktick_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("str`ing");

            // Assert

            Assert.AreEqual("`str``ing`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithNewline_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\n");

            // Assert

            Assert.AreEqual(@"`string\n`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithCarriageReturn_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\r");

            // Assert

            Assert.AreEqual(@"`string\r`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithTab_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\t");

            // Assert

            Assert.AreEqual(@"`string\t`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithBackslash_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\\");

            // Assert

            Assert.AreEqual(@"`string\\`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithFormFeed_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\f");

            // Assert

            Assert.AreEqual(@"`string\f`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithBackspace_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\b");

            // Assert

            Assert.AreEqual(@"`string\b`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithDoubleQuote_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\"");

            // Assert

            Assert.AreEqual(@"`string\""`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithSingleQuote_WritesString()
        {

            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\'");

            // Assert

            Assert.AreEqual(@"`string\'`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithNull_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\0");

            // Assert

            Assert.AreEqual(@"`string\u0000`", result);
        }

        [Test]
        public void WriteEscapedString_StringWithUnicode_WritesString()
        {
            // Act

            var result = SubDocumentPathExpressionVisitor.GetEscapedString("string\u1234");

            // Assert

            Assert.AreEqual(@"`string\u1234`", result);
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
