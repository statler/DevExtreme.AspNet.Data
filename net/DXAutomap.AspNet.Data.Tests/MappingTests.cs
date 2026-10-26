using AutoMapper;
using AutoMapper.Internal;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DXAutomap.AspNet.Data.Tests {
    public class MappingTests {
        public class Row {
            public int Id { get; set; }
            public int Value { get; set; }
            public string Text { get; set; }
            public DateTime? Date { get; set; }
            public Detail Detail { get; set; }
            public List<Child> Children { get; set; } = new List<Child>();
        }
        public class Child {
            public int Id { get; set; }
            public int RowId { get; set; }
            public int Value { get; set; }
        }
        public class Detail {
            public int Number { get; set; }
            public string Name { get; set; }
        }
        public class NestedContext {
            public int factor { get; set; } = 10;
            public int Offset { get; set; } = 100;
        }
        public class DtoA {
            public int Id { get; set; }
            public int Scaled { get; set; }
            public string Label { get; set; }
            public string MappedText { get; set; }
            public DateTime? MappedDate { get; set; }
            public int? NullableScaled { get; set; }
        }
        public class DtoB : DtoA { }
        public class WideDto {
            public int Field1 { get; set; }
            public int Field2 { get; set; }
            public int Field3 { get; set; }
            public int Field4 { get; set; }
            public int Field5 { get; set; }
            public int Field6 { get; set; }
            public int Field7 { get; set; }
            public int Field8 { get; set; }
            public int Field9 { get; set; }
            public int Field10 { get; set; }
            public int Field11 { get; set; }
            public int Field12 { get; set; }
            public int Field13 { get; set; }
            public int Field14 { get; set; }
            public int Field15 { get; set; }
            public int Field16 { get; set; }
            public int Field17 { get; set; }
            public int Field18 { get; set; }
            public int Field19 { get; set; }
            public int Field20 { get; set; }
            public int Field21 { get; set; }
            public int Field22 { get; set; }
            public int Field23 { get; set; }
            public int Field24 { get; set; }
            public int Field25 { get; set; }
            public int Field26 { get; set; }
            public int Field27 { get; set; }
            public int Field28 { get; set; }
            public int Field29 { get; set; }
            public int Field30 { get; set; }
            public int Field31 { get; set; }
            public int Field32 { get; set; }
            public int Field33 { get; set; }
        }
        public class ChildDto {
            public int Value { get; set; }
            public int Scaled { get; set; }
        }
        public class LetDto {
            public int Id { get; set; }
            public int Scaled { get; set; }
            public ChildDto FirstChild { get; set; }
        }

        static IMapper CreateMapper(int offset = 0) {
            int factor = 1;
            int days = 0;
            return new MapperConfiguration(cfg => {
                cfg.CreateMap<Row, DtoA>()
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => s.Value * factor + offset))
                    .ForMember(d => d.Label, o => o.MapFrom(s => "A:" + s.Value))
                    .ForMember(d => d.MappedText, o => o.MapFrom(s => s.Text))
                    .ForMember(d => d.MappedDate, o => o.MapFrom(s => s.Date.HasValue ? s.Date.Value.AddDays(days) : (DateTime?)null))
                    .ForMember(d => d.NullableScaled, o => o.MapFrom(s => s.Date.HasValue ? (int?)(s.Value * factor) : null));
                cfg.CreateMap<Row, DtoB>()
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => -s.Value * factor + offset))
                    .ForMember(d => d.Label, o => o.MapFrom(s => "B:" + s.Value))
                    .ForMember(d => d.MappedText, o => o.MapFrom(s => s.Text))
                    .ForMember(d => d.MappedDate, o => o.MapFrom(s => s.Date.HasValue ? s.Date.Value.AddDays(days) : (DateTime?)null))
                    .ForMember(d => d.NullableScaled, o => o.MapFrom(s => s.Date.HasValue ? (int?)(s.Value * factor) : null));
                cfg.CreateMap<Child, ChildDto>()
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => s.Value * factor));
                cfg.CreateMap<Row, LetDto>()
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => s.Value * factor))
                    .ForMember(d => d.FirstChild, o => o.MapFrom(s => s.Children.OrderBy(c => c.Id).FirstOrDefault()));
                cfg.CreateMap<Row, WideDto>().ForAllMembers(o => o.MapFrom(s => s.Value * factor));
            }).CreateMapper();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task FilterSortSelectAndCountUseRequestParameters(bool sqlite, bool dictionaryParameters) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
            object parameters = dictionaryParameters ? new Dictionary<string, object> { ["factor"] = -10 } : new { factor = -10 };
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Scaled", "<", -15 },
                Sort = new[] { new SortingInfo { Selector = "Scaled" } },
                Select = new[] { "Id", "Scaled" },
                RemoteSelect = true,
                RequireTotalCount = true,
                AllowAsyncOverSync = true
            }, mapper, parameters);
            var selected = result.data.Cast<IDictionary<string, object>>().ToArray();
            Assert.Equal(new object[] { 3, 2 }, selected.Select(row => row["Id"]));
            Assert.Equal(new object[] { -30, -20 }, selected.Select(row => row["Scaled"]));
            Assert.Equal(2, result.totalCount);

            var count = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Scaled", "<", -15 }, IsCountQuery = true, AllowAsyncOverSync = true
            }, mapper, parameters);
            Assert.Equal(2, count.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DestinationAndMapperConfigurationAreIsolated(bool sqlite) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            var otherMapper = CreateMapper(100);
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
            CustomAccessorCompilers.RegisterAutomapperProfiles(otherMapper);
            var a = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, Filter("Scaled", 20), mapper, new { factor = 10 });
            var b = await DataSourceLoader.LoadAsync<Row, DtoB>(data.Rows, Filter("Scaled", -20), mapper, new { factor = 10 });
            var other = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, Filter("Scaled", 120), otherMapper, new { factor = 10 });
            Assert.Equal("A:2", Assert.Single(a.data.Cast<DtoA>()).Label);
            Assert.Equal("B:2", Assert.Single(b.data.Cast<DtoB>()).Label);
            Assert.Equal(120, Assert.Single(other.data.Cast<DtoA>()).Scaled);
            Assert.Equal(1, a.totalCount);
            Assert.Equal(1, b.totalCount);
            Assert.Equal(1, other.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GroupPagingAndSummariesUseRequestMapping(bool sqlite) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            var result = await DataSourceLoader.LoadAsync<Row, DtoB>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Scaled", "<", -15 },
                Group = new[] { new GroupingInfo { Selector = "Scaled", IsExpanded = false } },
                RemoteGrouping = true, Take = 1, RequireGroupCount = true, RequireTotalCount = true,
                GroupSummary = new[] { new SummaryInfo { Selector = "Scaled", SummaryType = "sum" } },
                TotalSummary = new[] { new SummaryInfo { Selector = "Scaled", SummaryType = "sum" } },
                AllowAsyncOverSync = true
            }, mapper, new { factor = 10 });
            var group = Assert.Single(result.data.Cast<Group>());
            Assert.Equal(-30, group.key);
            Assert.Equal(-30m, Convert.ToDecimal(group.summary[0]));
            Assert.Equal(-50m, Convert.ToDecimal(result.summary[0]));
            Assert.Equal(2, result.totalCount);
            Assert.Equal(2, result.groupCount);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task EmptySumUsesMappedMemberType(bool sqlite, bool remote) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Id", "=", -1 },
                IsSummaryQuery = true, RemoteGrouping = remote,
                TotalSummary = new[] { new SummaryInfo { Selector = "Scaled", SummaryType = "sum" } },
                AllowAsyncOverSync = true
            }, CreateMapper(), new { factor = 10 });
            Assert.Equal(0m, Convert.ToDecimal(result.summary[0]));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task KeyPagingUsesTheSameFilterAsTheDataQuery(bool sqlite) {
            using var data = new Data(sqlite);
            var options = Filter("Scaled", 20);
            options.PrimaryKey = new[] { "Id" };
            options.PaginateViaPrimaryKey = true;
            options.Take = 1;
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, options, CreateMapper(), new { factor = 10 });
            Assert.Equal(2, Assert.Single(result.data.Cast<DtoA>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PreparedLetClauseCanResolveMappedFields(bool sqlite) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            var parameters = new { factor = 10 };
            var query = mapper.ConfigurationProvider.Internal().ProjectionBuilder.GetProjection(typeof(Row), typeof(LetDto), parameters, Array.Empty<MemberPath>());
            Assert.NotNull(query.LetClause);
            var result = await DataSourceLoader.LoadAsync<Row, LetDto>(data.Rows, Filter("Scaled", 20), mapper, parameters);
            var dto = Assert.Single(result.data.Cast<LetDto>());
            Assert.Equal(20, dto.Scaled);
            Assert.Equal(200, dto.FirstChild.Value);
        }

        [Fact]
        public async Task ConcurrentLoadsDoNotSharePreparedParameters() {
            using var data = new Data(false);
            var mapper = CreateMapper();
            await Task.WhenAll(Enumerable.Range(1, 40).Select(factor => Task.Run(async () => {
                var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, Filter("Scaled", factor * 2), mapper, new { factor });
                var dto = Assert.Single(result.data.Cast<DtoA>());
                Assert.Equal(2, dto.Id);
                Assert.Equal(factor * 2, dto.Scaled);
                Assert.Equal(1, result.totalCount);
            })));
        }

        static DataSourceLoadOptionsBase Filter(string member, object value) => new DataSourceLoadOptionsBase {
            Filter = new object[] { member, "=", value }, RequireTotalCount = true,
            StringToLower = false, AllowAsyncOverSync = true
        };

        [Theory]
        [InlineData(false, "contains", "IX")]
        [InlineData(true, "contains", "IX")]
        [InlineData(false, "startswith", "MI")]
        [InlineData(true, "startswith", "MI")]
        [InlineData(false, "endswith", "ED")]
        [InlineData(true, "endswith", "ED")]
        [InlineData(false, "=", "MIXED")]
        [InlineData(true, "=", "MIXED")]
        public async Task MappedStringsUseCaseHandlingAndHandleNulls(bool sqlite, string operation, string value) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "MappedText", operation, value },
                StringToLower = true, RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper());
            Assert.Equal(2, Assert.Single(result.data.Cast<DtoA>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false, "Scaled")]
        [InlineData(true, "Scaled")]
        [InlineData(false, "NullableScaled")]
        [InlineData(true, "NullableScaled")]
        public async Task NumericMappedFieldsSupportStringFilters(bool sqlite, string member) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { member, "contains", "4" },
                StringToLower = true, RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new { factor = 2 });
            Assert.Equal(2, Assert.Single(result.data.Cast<DtoA>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task NumericIntervalsApplyToMappedFieldsAndGroupCounts(bool sqlite, bool nullable) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Group = new[] { new GroupingInfo { Selector = nullable ? "NullableScaled" : "Scaled", GroupInterval = "5", IsExpanded = false } },
                Take = 10, RemoteGrouping = true, RequireGroupCount = true, RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new { factor = 3 });
            Assert.Equal(nullable ? new object[] { null, 0, 5 } : new object[] { 0, 5 }, result.data.Cast<Group>().Select(group => group.key));
            Assert.Equal(nullable ? 3 : 2, result.groupCount);
            Assert.Equal(3, result.totalCount);
        }

        [Theory]
        [InlineData(false, "month", 2, 4)]
        [InlineData(true, "month", 2, 4)]
        [InlineData(false, "quarter", 1, 2)]
        [InlineData(true, "quarter", 1, 2)]
        [InlineData(false, "dayOfWeek", 0, 4)]
        [InlineData(true, "dayOfWeek", 0, 4)]
        public async Task DateIntervalsApplyToNullableMappedFields(bool sqlite, string interval, int first, int second) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Group = new[] { new GroupingInfo { Selector = "MappedDate", GroupInterval = interval, IsExpanded = false } },
                Take = 10, RemoteGrouping = true, RequireGroupCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new { days = 1 });
            Assert.Equal(new object[] { null, first, second }, result.data.Cast<Group>().Select(group => group.key));
            Assert.Equal(3, result.groupCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RetiredOptionCannotChangeKeyPagingCountsOrGrouping(bool sqlite) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            var options = Filter("Scaled", 20);
#pragma warning disable CS0618 // Exercise a legacy caller setting the retired option.
            options.ProjectBeforeFilter = true;
#pragma warning restore CS0618
            options.PrimaryKey = new[] { "Id" };
            options.PaginateViaPrimaryKey = true;
            options.Take = 1;
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, options, mapper, new { factor = 10 });
            Assert.Equal(2, Assert.Single(result.data.Cast<DtoA>()).Id);
            Assert.Equal(1, result.totalCount);

            options.IsCountQuery = true;
            Assert.Equal(1, (await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, options, mapper, new { factor = 10 })).totalCount);
            options.IsCountQuery = false;
            options.PaginateViaPrimaryKey = false;
            options.Group = new[] { new GroupingInfo { Selector = "Scaled", IsExpanded = false } };
            options.RemoteGrouping = true;
            options.RequireGroupCount = true;
            var grouped = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, options, mapper, new { factor = 10 });
            Assert.Equal(20, Assert.Single(grouped.data.Cast<Group>()).key);
            Assert.Equal(1, grouped.totalCount);
            Assert.Equal(1, grouped.groupCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NestedContextAccessorSupportsFilteringSortingAndSelection(bool sqlite) {
            using var data = new Data(sqlite);
            CustomAccessorCompilers.RegisterContext<Detail, int>("Adjusted", context => {
                var offset = ((NestedContext)context).Offset;
                return detail => detail.Number + offset;
            });
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Detail.Adjusted", ">=", 102 },
                Sort = new[] { new SortingInfo { Selector = "Detail.Adjusted", Desc = true } },
                Select = new[] { "Id", "Detail.Adjusted" }, RemoteSelect = true,
                RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new NestedContext());
            var rows = result.data.Cast<IDictionary<string, object>>().ToArray();
            Assert.Equal(new object[] { 3, 2 }, rows.Select(row => row["Id"]));
            Assert.Equal(new object[] { 103, 102 }, rows.Select(row => ((IDictionary<string, object>)row["Detail"])["Adjusted"]));
            Assert.Equal(2, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NestedAccessorGroupingPreservesNullParentsAndGroupCount(bool sqlite) {
            using var data = new Data(sqlite);
            CustomAccessorCompilers.RegisterContext<Detail, int>("Adjusted", context => {
                var offset = ((NestedContext)context).Offset;
                return detail => detail.Number + offset;
            });
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Group = new[] { new GroupingInfo { Selector = "Detail.Adjusted", IsExpanded = false } },
                RemoteGrouping = true, Take = 10, RequireGroupCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new NestedContext());
            Assert.Equal(new object[] { null, 102, 103 }, result.data.Cast<Group>().Select(group => group.key));
            Assert.Equal(3, result.groupCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NestedStringAccessorKeepsCaseAndNullHandling(bool sqlite) {
            using var data = new Data(sqlite);
            CustomAccessorCompilers.Register<Detail, string>("Alias", detail => detail.Name);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "Detail.Alias", "contains", "IX" }, StringToLower = true,
                RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper());
            Assert.Equal(2, Assert.Single(result.data.Cast<DtoA>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NestedDtoMemberDoesNotReuseTheRootDtoMapping(bool sqlite) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, LetDto>(data.Rows, new DataSourceLoadOptionsBase {
                Filter = new object[] { "FirstChild.Scaled", "=", 2000 },
                RequireTotalCount = true, AllowAsyncOverSync = true
            }, CreateMapper(), new { factor = 10 });
            var row = Assert.Single(result.data.Cast<LetDto>());
            Assert.Equal(2, row.Id);
            Assert.Equal(20, row.Scaled);
            Assert.Equal(2000, row.FirstChild.Scaled);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LocalSelectUsesRequestedDtoParametersPagingAndCounts(bool sqlite) {
            using var data = new Data(sqlite);
            var mapper = CreateMapper();
            var options = new DataSourceLoadOptionsBase {
                Filter = new object[] { "Id", ">", 1 },
                Sort = new[] { new SortingInfo { Selector = "Id", Desc = true } },
                Select = new[] { "Id", "Scaled", "Label" }, RemoteSelect = false,
                Skip = 1, Take = 1, RequireTotalCount = true, AllowAsyncOverSync = true
            };
            var a = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, options, mapper, new { factor = 10 });
            var b = await DataSourceLoader.LoadAsync<Row, DtoB>(data.Rows, options, mapper, new Dictionary<string, object> { ["factor"] = 100 });
            var rowA = Assert.Single(a.data.Cast<IDictionary<string, object>>());
            var rowB = Assert.Single(b.data.Cast<IDictionary<string, object>>());
            Assert.Equal(2, rowA["Id"]);
            Assert.Equal(20, rowA["Scaled"]);
            Assert.Equal("A:2", rowA["Label"]);
            Assert.Equal(2, rowB["Id"]);
            Assert.Equal(-200, rowB["Scaled"]);
            Assert.Equal("B:2", rowB["Label"]);
            Assert.Equal(3, rowA.Count);
            Assert.Equal(3, rowB.Count);
            Assert.Equal(2, a.totalCount);
            Assert.Equal(2, b.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LocalSelectPreservesNestedLetClauseProjection(bool sqlite) {
            using var data = new Data(sqlite);
            var options = Filter("Scaled", 20);
            options.Select = new[] { "Id", "Scaled", "FirstChild.Scaled", "FirstChild.Value" };
            options.RemoteSelect = false;
            var result = await DataSourceLoader.LoadAsync<Row, LetDto>(data.Rows, options, CreateMapper(), new { factor = 10 });
            var row = Assert.Single(result.data.Cast<IDictionary<string, object>>());
            Assert.Equal(2, row["Id"]);
            Assert.Equal(20, row["Scaled"]);
            var child = Assert.IsAssignableFrom<IDictionary<string, object>>(row["FirstChild"]);
            Assert.Equal(2000, child["Scaled"]);
            Assert.Equal(200, child["Value"]);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LocalSelectSupportsGroupingAndSummaries(bool sqlite) {
            using var data = new Data(sqlite);
            var result = await DataSourceLoader.LoadAsync<Row, DtoA>(data.Rows, new DataSourceLoadOptionsBase {
                Select = new[] { "Id", "Scaled" }, RemoteSelect = false,
                Group = new[] { new GroupingInfo { Selector = "Scaled", IsExpanded = true } },
                RemoteGrouping = false, RequireGroupCount = true, RequireTotalCount = true,
                GroupSummary = new[] { new SummaryInfo { Selector = "Scaled", SummaryType = "sum" } },
                TotalSummary = new[] { new SummaryInfo { Selector = "Scaled", SummaryType = "sum" } },
                AllowAsyncOverSync = true
            }, CreateMapper(), new { factor = 10 });
            var groups = result.data.Cast<Group>().ToArray();
            Assert.Equal(new object[] { 10, 20, 30 }, groups.Select(group => group.key));
            Assert.All(groups, group => {
                var row = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(group.items));
                Assert.Equal(group.key, row["Scaled"]);
                Assert.Equal(Convert.ToDecimal(group.key), Convert.ToDecimal(group.summary[0]));
            });
            Assert.Equal(60m, Convert.ToDecimal(result.summary[0]));
            Assert.Equal(3, result.totalCount);
            Assert.Equal(3, result.groupCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WideSelectionAutomaticallyFallsBackToLocalDtoSelection(bool sqlite) {
            using var data = new Data(sqlite);
            var fields = typeof(WideDto).GetProperties().Select(property => property.Name).ToArray();
            Assert.Equal(33, fields.Length);
            var options = Filter("Field1", 20);
            options.Select = fields;
            var result = await DataSourceLoader.LoadAsync<Row, WideDto>(data.Rows, options, CreateMapper(), new { factor = 10 });
            var row = Assert.Single(result.data.Cast<IDictionary<string, object>>());
            Assert.Equal(33, row.Count);
            Assert.All(fields, field => Assert.Equal(20, row[field]));
            Assert.Equal(1, result.totalCount);
        }

        sealed class Data : IDisposable {
            readonly SqliteConnection _connection;
            readonly TestDb _db;
            public IQueryable<Row> Rows { get; }

            public Data(bool sqlite) {
                var rows = Enumerable.Range(1, 3).Select(i => new Row {
                    Id = i, Value = i, Children = new List<Child> { new Child { Id = i, RowId = i, Value = i * 100 } },
                    Text = i == 1 ? null : i == 2 ? "MiXeD" : "Other",
                    Date = i == 1 ? new DateTime(2026, 1, 31) : i == 2 ? new DateTime(2026, 4, 1) : null,
                    Detail = i == 1 ? null : new Detail { Number = i, Name = i == 2 ? "MiXeD" : "Other" }
                }).ToArray();
                if(sqlite) {
                    _connection = new SqliteConnection("Data Source=:memory:");
                    _connection.Open();
                    _db = new TestDb(new DbContextOptionsBuilder<TestDb>().UseSqlite(_connection).Options);
                    _db.Database.EnsureCreated();
                    _db.AddRange(rows);
                    _db.SaveChanges();
                    _db.ChangeTracker.Clear();
                    Rows = _db.Set<Row>().AsNoTracking();
                } else {
                    Rows = rows.AsQueryable();
                }
            }
            public void Dispose() {
                _db?.Dispose();
                _connection?.Dispose();
            }
        }

        sealed class TestDb : DbContext {
            public TestDb(DbContextOptions<TestDb> options) : base(options) { }
            protected override void OnModelCreating(ModelBuilder modelBuilder) {
                modelBuilder.Entity<Row>().HasMany(row => row.Children).WithOne().HasForeignKey(child => child.RowId);
                modelBuilder.Entity<Row>().OwnsOne(row => row.Detail);
            }
        }
    }
}
