using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {
    public class AccessorTypeIdentityTests : IDisposable {
        public class Item<T> { public int Value { get; set; } }
        public class Dto { public int Code { get; set; } }

        public AccessorTypeIdentityTests() => CustomAccessorCompilers.Clear();
        public void Dispose() => CustomAccessorCompilers.Clear();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClosedGenericTypesKeepTheirOwnContextAccessors(bool untypedResult) {
            if(untypedResult) {
                CustomAccessorCompilers.RegisterContext<Item<int>>("ContextCode", context => {
                    var offset = (int)context;
                    return (Expression<Func<Item<int>, int>>)(item => item.Value + offset);
                });
                CustomAccessorCompilers.RegisterContext<Item<string>>("ContextCode", context => {
                    var offset = (int)context;
                    return (Expression<Func<Item<string>, int>>)(item => item.Value * offset);
                });
            } else {
                CustomAccessorCompilers.RegisterContext<Item<int>, int>("ContextCode", context => {
                    var offset = (int)context;
                    return item => item.Value + offset;
                });
                CustomAccessorCompilers.RegisterContext<Item<string>, int>("ContextCode", context => {
                    var offset = (int)context;
                    return item => item.Value * offset;
                });
            }

            Assert.Equal(12, Read<Item<int>>(new Item<int> { Value = 2 }, "ContextCode", 10));
            Assert.Equal(20, Read<Item<string>>(new Item<string> { Value = 2 }, "ContextCode", 10));
            Assert.Equal(5, Read<Item<int>>(new Item<int> { Value = 2 }, "ContextCode", 3));
        }

        [Fact]
        public void LegacyProfileLookupSeparatesSourceTypesWithTheSameName() {
            var mapper = new MapperConfiguration(cfg => {
                cfg.CreateMap<Item<int>, Dto>().ForMember(d => d.Code, o => o.MapFrom(s => s.Value + 10));
                cfg.CreateMap<Item<string>, Dto>().ForMember(d => d.Code, o => o.MapFrom(s => s.Value * 10));
            }).CreateMapper();
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

            Assert.Equal(12, Read(new Item<int> { Value = 2 }, "Code", null));
            Assert.Equal(20, Read(new Item<string> { Value = 2 }, "Code", null));
        }

        [Fact]
        public void NamedLibraryApiAndTypedRegistrationRemainUsable() {
            var library = new AccessorLibrary();
            Expression<Func<Item<int>, int>> named = item => item.Value + 10;
            library.Add("named", "Code", named);
            var target = Expression.Parameter(typeof(Item<int>));
            var expression = library.Get(target, "named", "Code", null);
            Assert.Equal(12, Expression.Lambda<Func<Item<int>, int>>(expression, target).Compile()(new Item<int> { Value = 2 }));

            library.Add<Item<int>, int>("Code", item => item.Value * 10);
            expression = library.Get(target, typeof(Item<int>).Name, "Code", null);
            Assert.Equal(20, Expression.Lambda<Func<Item<int>, int>>(expression, target).Compile()(new Item<int> { Value = 2 }));
        }

        static int Read<T>(T item, string member, object context) {
            var target = Expression.Parameter(typeof(T));
            var expression = CustomAccessorCompilers.TryCompile(target, member, context);
            return Expression.Lambda<Func<T, int>>(expression, target).Compile()(item);
        }
    }
}
