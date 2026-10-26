using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {
    public class MapperArgumentTests {
        public class Item { public int Id { get; set; } }
        public class Dto { public int Id { get; set; } }

        [Theory]
        [InlineData("data")]
        [InlineData("count")]
        [InlineData("summary")]
        [InlineData("select")]
        public async Task MissingMapperIsRejectedByEveryTypedOverload(string mode) {
            var source = new[] { new Item { Id = 1 } };
            var options = new SampleLoadOptions {
                IsCountQuery = mode == "count", IsSummaryQuery = mode == "summary",
                Select = mode == "select" ? new[] { "Id" } : null,
                RemoteSelect = true, AllowAsyncOverSync = true
            };

            Assert.Equal("mapper", Assert.Throws<ArgumentNullException>(() => DataSourceLoader.Load<Item, Dto>(source, options, null)).ParamName);
            Assert.Equal("mapper", Assert.Throws<ArgumentNullException>(() => DataSourceLoader.Load<Item, Dto>(source.AsQueryable(), options, null)).ParamName);
            Assert.Equal("mapper", (await Assert.ThrowsAsync<ArgumentNullException>(() => DataSourceLoader.LoadAsync<Item, Dto>(source.AsQueryable(), options, null))).ParamName);
            Assert.Equal("mapper", (await Assert.ThrowsAsync<ArgumentNullException>(() => DataSourceLoader.LoadAsync<Item, Dto>(source.AsQueryable(), options, CancellationToken.None, null))).ParamName);
        }

        [Fact]
        public async Task SameTypeLoadsDoNotRequireAMapper() {
            var source = new[] { new Item { Id = 1 } }.AsQueryable();
            var options = new SampleLoadOptions { AllowAsyncOverSync = true, RequireTotalCount = true };
            var sync = DataSourceLoader.Load<Item, Item>(source, options, null);
            var asyncResult = await DataSourceLoader.LoadAsync<Item, Item>(source, options, null);
            Assert.Equal(1, Assert.Single(sync.data.Cast<Item>()).Id);
            Assert.Equal(1, Assert.Single(asyncResult.data.Cast<Item>()).Id);
            Assert.Equal(1, sync.totalCount);
            Assert.Equal(1, asyncResult.totalCount);
        }
    }
}
