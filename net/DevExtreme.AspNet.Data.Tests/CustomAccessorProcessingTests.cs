using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Linq.Expressions;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {
    public class CustomAccessorProcessingTests : IDisposable {
        public class Item {
            public int? Number { get; set; }
            public string Text { get; set; }
        }

        public CustomAccessorProcessingTests() => CustomAccessorCompilers.Clear();
        public void Dispose() => CustomAccessorCompilers.Clear();

        [Theory]
        [InlineData("contains", "IX", true)]
        [InlineData("startswith", "MI", true)]
        [InlineData("endswith", "ED", true)]
        [InlineData("=", "MIXED", true)]
        [InlineData("notcontains", "IX", false)]
        public void WholePathCompilerUsesCaseHandlingAndNullGuards(string operation, string value, bool expected) {
            // A dotted custom name is resolved in one call, without traversing real members.
            CustomAccessorCompilers.Register((target, member) => member == "Custom.Text"
                ? Expression.Property(target, nameof(Item.Text)) : null);
            var filter = (Func<Item, bool>)new FilterExpressionCompiler(typeof(Item), true, true)
                .Compile(new object[] { "Custom.Text", operation, value }).Compile();
            Assert.Equal(expected, filter(new Item { Text = "MiXeD" }));
            Assert.Equal(operation == "notcontains", filter(new Item()));
            Assert.Equal(operation == "notcontains", filter(null));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StringToLowerOptionIsRespected(bool lower) {
            CustomAccessorCompilers.Register<Item, string>("Alias", item => item.Text);
            var filter = (Func<Item, bool>)new FilterExpressionCompiler(typeof(Item), true, lower)
                .Compile(new object[] { "Alias", "=", "mixed" }).Compile();
            Assert.Equal(lower, filter(new Item { Text = "MiXeD" }));
        }

        [Fact]
        public void NullableNumericAccessorCanUseStringOperations() {
            CustomAccessorCompilers.Register<Item, int?>("Alias", item => item.Number);
            var filter = (Func<Item, bool>)new FilterExpressionCompiler(typeof(Item), true, true)
                .Compile(new object[] { "Alias", "contains", "2" }).Compile();
            Assert.True(filter(new Item { Number = 12 }));
            Assert.False(filter(new Item()));
            Assert.False(filter(null));
        }

        [Fact]
        public void RetiredOptionIsIgnoredBySerializersAndParser() {
#pragma warning disable CS0618 // Test the compatibility members intentionally.
            var options = new DataSourceLoadOptionsBase { ProjectBeforeFilter = true };
            Assert.Null(options.ProjectBeforeFilter);
            Assert.DoesNotContain("ProjectBeforeFilter", System.Text.Json.JsonSerializer.Serialize(options));
            Assert.Null(System.Text.Json.JsonSerializer.Deserialize<DataSourceLoadOptionsBase>("{\"ProjectBeforeFilter\":true}").ProjectBeforeFilter);
#if NEWTONSOFT_TESTS
            Assert.DoesNotContain("ProjectBeforeFilter", Newtonsoft.Json.JsonConvert.SerializeObject(options));
            Assert.Null(Newtonsoft.Json.JsonConvert.DeserializeObject<DataSourceLoadOptionsBase>("{\"ProjectBeforeFilter\":true}").ProjectBeforeFilter);
#endif
            DataSourceLoadOptionsParser.Parse(options, key => {
                Assert.NotEqual(DataSourceLoadOptionsParser.KEY_PROJECTBEFOREFILTER, key);
                return null;
            });
#pragma warning restore CS0618
        }
    }
}
