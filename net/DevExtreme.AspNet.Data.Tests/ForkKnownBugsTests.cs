using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using DevExtreme.AspNet.Data.ResponseModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {

    // Known defects in the fork features, found in the review of 2026-10-26. Each test asserts today's wrong result,
    // so it passes now and fails once the defect is fixed. The FIXME says what the correct result is and why.
    // The registries are static. Each test clears the compiler lists; AccessorLibrary entries cannot be cleared, so
    // the types below are used only here.
    // Not covered: the check-then-add race in AccessorLibrary.Add (not deterministic), and the extra accessor finder
    // that each RegisterAutomapperProfiles call adds (the duplicates return the same results).
    public class ForkKnownBugsTests {

        public class KbParent {
            public string Name { get; set; }
        }

        public class KbPerson {
            public int ID { get; set; }
            public int Value { get; set; }
            public string Name { get; set; }
            public KbParent Parent { get; set; }
        }

        public class KbPersonDto {
            public int ID { get; set; }
            public int Scaled { get; set; }
            public int Doubled { get; set; }
        }

        public class KbOrder {
            public int ID { get; set; }
            public string Code { get; set; }
        }

        public class KbOrderDtoA {
            public int ID { get; set; }
            public string Label { get; set; }
        }

        public class KbOrderDtoB {
            public int ID { get; set; }
            public string Label { get; set; }
        }

        // Two types with the same short name, KbItem.
        public class KbOuterA {
            public class KbItem {
                public int Value { get; set; }
            }
        }

        public class KbOuterB {
            public class KbItem {
                public int Value { get; set; }
            }
        }

        static IMapper CreateMapper() {
            // Replaced per query by the projection parameter object, the same way the API passes UserId and minsOffset.
            int factor = 1;
            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<KbPerson, KbPersonDto>()
                    .ForMember(d => d.Scaled, o => o.MapFrom(s => s.Value * factor))
                    .ForMember(d => d.Doubled, o => o.MapFrom(s => s.Value * 2));
                cfg.CreateMap<KbOrder, KbOrderDtoA>()
                    .ForMember(d => d.Label, o => o.MapFrom(s => "A:" + s.Code));
                cfg.CreateMap<KbOrder, KbOrderDtoB>()
                    .ForMember(d => d.Label, o => o.MapFrom(s => "B:" + s.Code));
            });
            return config.CreateMapper();
        }

        static List<KbPerson> People() {
            return new List<KbPerson> {
                new KbPerson { ID = 1, Value = 1, Name = "root1", Parent = new KbParent { Name = "p1" } },
                new KbPerson { ID = 2, Value = 2, Name = "root2", Parent = new KbParent { Name = "p2" } },
                new KbPerson { ID = 3, Value = 3, Name = "root3", Parent = new KbParent { Name = "p3" } }
            };
        }

        [Fact]
        public void FilterOnMapFromMemberIgnoresProjectionParameter() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Scaled", ">", 15 },
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper, new { factor = 10 });

                // FIXME: should return IDs 2 and 3 (Scaled 20 and 30), with a total count of 2. The projection uses factor = 10
                // from the projection parameters, but RegisterAutomapperProfiles stores the MapFrom expression with the
                // profile's captured variable, so the filter and the count use factor = 1 and match nothing. In the API this
                // affects NotificationListDto.StatusString, Status, IsRead and IsDismissed (UserId is always null, and
                // StatusString is in the notifications searchExpr), ApprovalListExtDto and ApprovalListDto.DaysTilDue
                // (minsOffset is always 600, a time-zone shift), and would affect CustomRegisterDto.IsActive, IsRequired,
                // IsReported and IsPrivate (RegisterTypeId is always -1) if the client filtered or sorted on them.
                Assert.Empty(result.data.Cast<KbPersonDto>());
                Assert.Equal(0, result.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void SelectOfMapFromMemberIgnoresProjectionParameter() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                // RemoteSelect is what EF Core uses when there are 32 or fewer select fields.
                var options = new SampleLoadOptions {
                    Select = new[] { "ID", "Scaled" },
                    RemoteSelect = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper, new { factor = 10 });
                var scaled = result.data.Cast<IDictionary<string, object>>().Select(i => i["Scaled"]);

                // FIXME: should be 10, 20 and 30. With a select, the projection is skipped and each field is read through the
                // stored MapFrom expression, which uses the profile's captured factor = 1, so the returned values are wrong.
                Assert.Equal(new object[] { 1, 2, 3 }, scaled);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void ProjectionWithSelectAndLocalSelectThrows() {
            var mapper = CreateMapper();

            // RemoteSelect is false by default for LINQ to Objects, and for any provider when there are more than 32 select fields.
            var options = new SampleLoadOptions {
                Select = new[] { "ID", "Doubled" }
            };

            // FIXME: should return the ID and Doubled values. AddSelect projects to the DTO when the select is not remote,
            // and ExecWithSelectAsync then reads the DTO query as the entity type, which throws. The API caps the select at
            // 32 fields and does not parse RemoteSelect, so it does not reach this today.
            Assert.Throws<ArgumentException>(() => DataSourceLoader.Load<KbPerson, KbPersonDto>(People(), options, mapper));
        }

        [Fact]
        public void NumericGroupIntervalIsIgnoredOnMapFromMember() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Group = new[] { new GroupingInfo { Selector = "Doubled", GroupInterval = "5", IsExpanded = false } },
                    RemoteGrouping = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper);
                var keys = result.data.Cast<Group>().Select(g => g.key);

                // FIXME: should be the interval keys 0 and 5 (Doubled is 2, 4 and 6). When a custom accessor resolves the
                // whole path, CompileAccessorExpression returns it before the progression is built, so the group interval,
                // the filter's string conversion and ToLower, liftToNullable and the null guards are not applied. Upstream does the same for any custom accessor; the
                // fork makes every MapFrom member a custom accessor.
                Assert.Equal(new object[] { 2, 4, 6 }, keys);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void ContainsOnNumericMapFromMemberThrows() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Doubled", "contains", "4" }
                };

                // FIXME: should match ID 2, as "contains" does on a plain numeric property, which is converted to a string
                // first. The custom accessor skips the progression, so the int value reaches the string call unconverted. On
                // LINQ to Objects the null guard (Coalesce with "") throws InvalidOperationException; on EF Core, where null
                // guards are off, String.Contains on an int throws ArgumentException. Same cause as
                // NumericGroupIntervalIsIgnoredOnMapFromMember.
                Assert.Throws<InvalidOperationException>(() => DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper));
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void DottedPathSegmentUsesRootTypeAccessor() {
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(CreateMapper());
                CustomAccessorCompilers.Register<KbPerson, string>("Name", p => "ROOT-ACCESSOR");

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Parent.Name", "=", "p2" },
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load(People().AsQueryable(), options);

                // FIXME: should be 1 (the row whose Parent.Name is p2). ExpressionCompiler looks up each path segment with the
                // root target instead of the current one, so the segment Name resolves to the root type's Name accessor and
                // replaces Parent.Name with "ROOT-ACCESSOR".
                Assert.Equal(0, result.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void RegisterIsNotUsedWithoutRegisterAutomapperProfiles() {
            try {
                CustomAccessorCompilers.Register<KbPerson, int>("KbTriple", p => p.Value * 3);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "KbTriple", "=", 6 }
                };

                // FIXME: should match ID 2. Register and RegisterContext only add to the accessor library. The function that
                // reads the library is registered by RegisterAutomapperProfiles, so without that call KbTriple is treated as
                // a property name and fails.
                Assert.Throws<ArgumentException>(() => DataSourceLoader.Load(People().AsQueryable(), options));
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void AccessorsAreKeyedByShortTypeName() {
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(CreateMapper());
                CustomAccessorCompilers.Register<KbOuterA.KbItem, int>("KbCode", i => i.Value);
                CustomAccessorCompilers.Register<KbOuterB.KbItem, int>("KbCode", i => i.Value * 100);

                var items = new[] { new KbOuterA.KbItem { Value = 1 }, new KbOuterA.KbItem { Value = 2 } }.AsQueryable();
                var options = new SampleLoadOptions {
                    Filter = new object[] { "KbCode", "=", 2 }
                };

                // FIXME: should match the item with Value 2. Both types are named KbItem, so the second registration replaces
                // the first, and its expression, which reads KbOuterB.KbItem.Value, cannot be applied to KbOuterA.KbItem.
                Assert.Throws<ArgumentException>(() => DataSourceLoader.Load(items, options));
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void MapFromMembersOfTwoDtosShareOneAccessor() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var orders = new[] { new KbOrder { ID = 1, Code = "x" } }.AsQueryable();
                var resultA = DataSourceLoader.Load<KbOrder, KbOrderDtoA>(orders, new SampleLoadOptions {
                    Filter = new object[] { "Label", "=", "A:x" },
                    RequireTotalCount = true,
                    StringToLower = false
                }, mapper);
                var resultB = DataSourceLoader.Load<KbOrder, KbOrderDtoB>(orders, new SampleLoadOptions {
                    Filter = new object[] { "Label", "=", "B:x" },
                    RequireTotalCount = true,
                    StringToLower = false
                }, mapper);

                // StringToLower is off because whole-path custom accessors are not lowered (see NumericGroupIntervalIsIgnoredOnMapFromMember).
                // FIXME: each load should match its order, a total of 2. MapFrom entries are keyed by entity type and member
                // name, without the DTO type, so the last map registered wins and the other DTO's filter uses the wrong
                // expression. Which DTO wins depends on the order of GetAllTypeMaps.
                Assert.Equal(1, resultA.totalCount + resultB.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void ProjectBeforeFilterCountIsNotProjected() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Scaled", ">", 15 },
                    ProjectBeforeFilter = true,
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper, new { factor = 10 });

                // FIXME: the total count should be 2, the number of rows returned. The data query projects before it filters,
                // but the count query (and the group count and key queries) filter the unprojected entity, through the stored
                // MapFrom expression, so the count is 0. Any request can set projectbeforefilter through the API's parser.
                Assert.Equal(2, result.data.Cast<KbPersonDto>().Count());
                Assert.Equal(0, result.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void NullMapperThrowsNullReferenceException() {
            var options = new SampleLoadOptions();

            // FIXME: should throw ArgumentNullException for the mapper, or load without projection. TDto differs from T, so
            // AddSelect reads Context.Mapper, which is null.
            Assert.Throws<NullReferenceException>(() => DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, null));
        }
    }

}
