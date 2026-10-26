using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {
    public class ForkMappedAccessorPrecedenceTests : IDisposable {
        public class Item {
            public int Value { get; set; }
        }
        public class ItemDto {
            public int Score { get; set; }
        }
        public class DerivedItem : Item { }
        public class ExplicitItem {
            public int Value { get; set; }
        }
        public class ExplicitDto {
            public int Score { get; set; }
        }

        public ForkMappedAccessorPrecedenceTests() => CustomAccessorCompilers.Clear();
        public void Dispose() => CustomAccessorCompilers.Clear();

        static IMapper CreateMapper() {
            int factor = 1;
            return new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>()
                .ForMember(d => d.Score, o => o.MapFrom(s => s.Value * factor))).CreateMapper();
        }

        [Fact]
        public void TypedLoadResolvesMappingWithoutGlobalRegistration() {
            var result = DataSourceLoader.Load<Item, ItemDto>(new[] { new Item { Value = 2 } }, new SampleLoadOptions {
                Filter = new object[] { "Score", "=", 20 }, RequireTotalCount = true
            }, CreateMapper(), new { factor = 10 });
            Assert.Equal(20, Assert.Single(result.data.Cast<ItemDto>()).Score);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ScopedMappingKeepsHookPrecedenceAndOriginalRuntimeContext(bool hookFirst) {
            var mapper = CreateMapper();
            var context = new { factor = 10 };
            CustomAccessorCompilers.CompilerFuncWithContext hook = (target, member, receivedContext) => {
                Assert.Same(context, receivedContext);
                return member == "Score" ? Expression.Constant(99) : null;
            };
            if(hookFirst)
                CustomAccessorCompilers.RegisterWithContext(hook);
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
            if(!hookFirst)
                CustomAccessorCompilers.RegisterWithContext(hook);

            var result = DataSourceLoader.Load<Item, ItemDto>(new[] { new Item { Value = 2 } }, new SampleLoadOptions {
                Select = new[] { "Score" }, RemoteSelect = true
            }, mapper, context);
            var row = Assert.Single(result.data.Cast<IDictionary<string, object>>());
            Assert.Equal(hookFirst ? 99 : 20, row["Score"]);
        }

        [Fact]
        public void CovariantSourceUsesTheQueryElementType() {
            IQueryable<Item> source = new[] { new DerivedItem { Value = 2 } }.AsQueryable();
            var result = DataSourceLoader.Load<Item, ItemDto>(source, new SampleLoadOptions {
                Filter = new object[] { "Score", "=", 20 }, RequireTotalCount = true
            }, CreateMapper(), new { factor = 10 });
            Assert.Equal(20, Assert.Single(result.data.Cast<ItemDto>()).Score);
            Assert.Equal(1, result.totalCount);
        }

        [Fact]
        public void ExplicitContextAccessorTakesPrecedenceOverScopedMapping() {
            int factor = 1;
            var mapper = new MapperConfiguration(cfg => cfg.CreateMap<ExplicitItem, ExplicitDto>()
                .ForMember(d => d.Score, o => o.MapFrom(s => s.Value * factor))).CreateMapper();
            var context = new { factor = 10 };
            CustomAccessorCompilers.RegisterContext<ExplicitItem, int>("Score", receivedContext => {
                Assert.Same(context, receivedContext);
                return item => item.Value + 100;
            });
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

            var result = DataSourceLoader.Load<ExplicitItem, ExplicitDto>(new[] { new ExplicitItem { Value = 2 } }, new SampleLoadOptions {
                Select = new[] { "Score" }, RemoteSelect = true
            }, mapper, context);
            Assert.Equal(102, Assert.Single(result.data.Cast<IDictionary<string, object>>())["Score"]);
        }
    }
}
