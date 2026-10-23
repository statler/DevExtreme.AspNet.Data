using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {

    // Tests for the CivilPro fork features, run on in-memory data. The async tests set AllowAsyncOverSync
    // because LINQ to Objects has no async adapter.
    // The registries are static; each test clears the compiler lists it used. AccessorLibrary entries cannot
    // be cleared, so they are keyed on types used only here.
    public class ForkAutoMapperTests {

        public class ForkPerson {
            public int ID { get; set; }
            public string First { get; set; }
            public string Last { get; set; }
            public int Value { get; set; }
            public List<ForkTag> Tags { get; set; } = new List<ForkTag>();
        }

        public class ForkTag {
            public int UserId { get; set; }
            public string Name { get; set; }
        }

        public class ForkPersonDto {
            public int ID { get; set; }
            public string FullName { get; set; }
            public int Scaled { get; set; }
        }

        static IMapper CreateMapper() {
            // Replaced per query by the projection parameter object, the same way the API passes minsOffset.
            int factor = 1;
            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<ForkPerson, ForkPersonDto>()
                    .ForMember(d => d.FullName, o => o.MapFrom(s => s.First + " " + s.Last))
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => s.Value * factor));
            });
            return config.CreateMapper();
        }

        static IQueryable<ForkPerson> People() {
            return new[] {
                new ForkPerson { ID = 1, First = "anna", Last = "smith", Value = 1 },
                new ForkPerson { ID = 2, First = "bob", Last = "jones", Value = 2, Tags = { new ForkTag { UserId = 7, Name = "x" } } },
                new ForkPerson { ID = 3, First = "carl", Last = "abbot", Value = 3, Tags = { new ForkTag { UserId = 8, Name = "x" } } }
            }.AsQueryable();
        }

        static int ReadInt(object context, string name) {
            return (int)context.GetType().GetProperty(name).GetValue(context);
        }

        static string DataQuery(SampleLoadOptions options) {
            return options.ExpressionLog.Single(line => line.Contains(".Select(") && !line.Contains(".Count()"));
        }

        [Fact]
        public async Task ProjectsAfterFilterAndSortOnMapFromMembers() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "FullName", "contains", "b" },
                    Sort = new[] { new SortingInfo { Selector = "FullName", Desc = true } },
                    RequireTotalCount = true,
                    AllowAsyncOverSync = true
                };

                var result = await DataSourceLoader.LoadAsync<ForkPerson, ForkPersonDto>(People(), options, mapper, new { factor = 10 });
                var data = result.data.Cast<ForkPersonDto>().ToArray();

                Assert.Equal(2, result.totalCount);
                Assert.Equal(new[] { 3, 2 }, data.Select(i => i.ID));
                Assert.Equal(new[] { "carl abbot", "bob jones" }, data.Select(i => i.FullName));
                Assert.Equal(new[] { 30, 20 }, data.Select(i => i.Scaled));

                var query = DataQuery(options);
                Assert.True(query.IndexOf(".Where(") < query.IndexOf(".Select("), query);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void SyncLoadWithMapperProjects() {
            var mapper = CreateMapper();
            var result = DataSourceLoader.Load<ForkPerson, ForkPersonDto>(People(), new SampleLoadOptions(), mapper);
            var data = result.data.Cast<ForkPersonDto>().ToArray();
            Assert.Equal(new[] { "anna smith", "bob jones", "carl abbot" }, data.Select(i => i.FullName));
            Assert.Equal(new[] { 1, 2, 3 }, data.Select(i => i.Scaled));
        }

        [Fact]
        public async Task ProjectBeforeFilterFiltersOnDtoMembers() {
            var mapper = CreateMapper();
            var options = new SampleLoadOptions {
                Filter = new object[] { "Scaled", ">", 15 },
                Sort = new[] { new SortingInfo { Selector = "Scaled" } },
                ProjectBeforeFilter = true,
                AllowAsyncOverSync = true
            };

            var result = await DataSourceLoader.LoadAsync<ForkPerson, ForkPersonDto>(People(), options, mapper, new { factor = 10 });
            var data = result.data.Cast<ForkPersonDto>().ToArray();

            Assert.Equal(new[] { 20, 30 }, data.Select(i => i.Scaled));

            var query = DataQuery(options);
            Assert.True(query.IndexOf(".Select(") < query.IndexOf(".Where("), query);
        }

        [Fact]
        public async Task ContextAccessorReadsRuntimeResolutionContext() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
                CustomAccessorCompilers.RegisterContext<ForkPerson, int>("ForkContextValue", ctx => {
                    var offset = ReadInt(ctx, "Offset");
                    return p => p.Value + offset;
                });

                var options = new SampleLoadOptions {
                    Filter = new object[] { "ForkContextValue", "=", 102 },
                    RequireTotalCount = true,
                    AllowAsyncOverSync = true
                };

                var result = await DataSourceLoader.LoadAsync<ForkPerson, ForkPersonDto>(People(), options, mapper, new { Offset = 100 });
                var data = result.data.Cast<ForkPersonDto>().ToArray();

                Assert.Equal(1, result.totalCount);
                Assert.Equal(2, data.Single().ID);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public async Task ContextFilterUsesCompileNonCustomBinary() {
            var mapper = CreateMapper();
            try {
                CustomFilterCompilers.RegisterBinaryExpressionCompilerWithContext((info, compiler) => {
                    if(info.DataItemExpression.Type != typeof(ForkPerson) || info.AccessorText != "TagName")
                        return null;

                    var userId = ReadInt(compiler.RuntimeResolutionContext, "UserId");
                    var tag = Expression.Parameter(typeof(ForkTag), "t");
                    var nameMatches = compiler.CompileNonCustomBinary(tag, new object[] { "Name", info.Operation, info.Value });
                    var body = Expression.AndAlso(nameMatches, Expression.Equal(Expression.Property(tag, "UserId"), Expression.Constant(userId)));

                    return Expression.Call(
                        typeof(Enumerable), nameof(Enumerable.Any), new[] { typeof(ForkTag) },
                        Expression.Property(info.DataItemExpression, "Tags"), Expression.Lambda(body, tag)
                    );
                });

                var options = new SampleLoadOptions {
                    Filter = new object[] { "TagName", "=", "x" },
                    AllowAsyncOverSync = true
                };

                var result = await DataSourceLoader.LoadAsync<ForkPerson, ForkPersonDto>(People(), options, mapper, new { UserId = 7 });
                Assert.Equal(2, result.data.Cast<ForkPersonDto>().Single().ID);
            } finally {
                CustomFilterCompilers.Binary.CompilerFuncs.Clear();
            }
        }

        [Fact]
        public void UpstreamStyleCustomFilterStillRegisters() {
            try {
                CustomFilterCompilers.RegisterBinaryExpressionCompiler(info => {
                    if(info.DataItemExpression.Type == typeof(ForkPerson) && info.AccessorText == "HasTags")
                        return Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { typeof(ForkTag) }, Expression.Property(info.DataItemExpression, "Tags"));
                    return null;
                });

                var result = DataSourceLoader.Load(People(), new SampleLoadOptions {
                    Filter = new object[] { "HasTags", "=", true },
                    RequireTotalCount = true
                });

                Assert.Equal(2, result.totalCount);
            } finally {
                CustomFilterCompilers.Binary.CompilerFuncs.Clear();
            }
        }
    }

}
