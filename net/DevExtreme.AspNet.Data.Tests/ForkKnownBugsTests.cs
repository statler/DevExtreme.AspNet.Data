using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using DevExtreme.AspNet.Data.ResponseModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {

    // Regression coverage for defects found in the review of 2026-10-26. These tests assert corrected behaviour.
    // The registries are static. Each test clears the compiler lists; AccessorLibrary entries cannot be cleared, so
    // the types below are used only here.
    // Fixed registration defects are covered by ForkRegistrationTests with assertions for the correct behaviour.
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
        public void FilterOnMapFromMemberUsesProjectionParameter() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Scaled", ">", 15 },
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper, new { factor = 10 });

                var data = result.data.Cast<KbPersonDto>().ToArray();
                Assert.Equal(new[] { 2, 3 }, data.Select(item => item.ID));
                Assert.Equal(new[] { 20, 30 }, data.Select(item => item.Scaled));
                Assert.Equal(2, result.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void SelectOfMapFromMemberUsesProjectionParameter() {
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

                Assert.Equal(new object[] { 10, 20, 30 }, scaled);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void ProjectionWithLocalSelectReturnsSelectedDtoValues() {
            var mapper = CreateMapper();

            // RemoteSelect is false by default for LINQ to Objects, and for any provider when there are more than 32 select fields.
            var options = new SampleLoadOptions {
                Select = new[] { "ID", "Doubled" }
            };

            var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People(), options, mapper);
            var rows = result.data.Cast<IDictionary<string, object>>().ToArray();
            Assert.Equal(new object[] { 1, 2, 3 }, rows.Select(row => row["ID"]));
            Assert.Equal(new object[] { 2, 4, 6 }, rows.Select(row => row["Doubled"]));
            Assert.All(rows, row => Assert.Equal(2, row.Count));
        }

        [Fact]
        public void NumericGroupIntervalIsAppliedToMapFromMember() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Group = new[] { new GroupingInfo { Selector = "Doubled", GroupInterval = "5", IsExpanded = false } },
                    RemoteGrouping = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper);
                var keys = result.data.Cast<Group>().Select(g => g.key);

                Assert.Equal(new object[] { 0, 5 }, keys);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void ContainsOnNumericMapFromMemberConvertsToString() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Doubled", "contains", "4" }
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper);
                Assert.Equal(2, Assert.Single(result.data.Cast<KbPersonDto>()).ID);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void DottedPathSegmentUsesItsOwnTypeAccessor() {
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(CreateMapper());
                CustomAccessorCompilers.Register<KbPerson, string>("Name", p => "ROOT-ACCESSOR");

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Parent.Name", "=", "p2" },
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load(People().AsQueryable(), options);

                Assert.Equal(1, result.totalCount);
                Assert.Equal(2, Assert.Single(result.data.Cast<KbPerson>()).ID);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void AccessorsWithTheSameShortTypeNameRemainIndependent() {
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(CreateMapper());
                CustomAccessorCompilers.Register<KbOuterA.KbItem, int>("KbCode", i => i.Value);
                CustomAccessorCompilers.Register<KbOuterB.KbItem, int>("KbCode", i => i.Value * 100);

                var items = new[] { new KbOuterA.KbItem { Value = 1 }, new KbOuterA.KbItem { Value = 2 } }.AsQueryable();
                var options = new SampleLoadOptions {
                    Filter = new object[] { "KbCode", "=", 2 }
                };

                var result = DataSourceLoader.Load(items, options);
                Assert.Equal(2, Assert.Single(result.data.Cast<KbOuterA.KbItem>()).Value);
                var other = DataSourceLoader.Load(new[] { new KbOuterB.KbItem { Value = 2 } }, new SampleLoadOptions {
                    Filter = new object[] { "KbCode", "=", 200 }
                });
                Assert.Equal(2, Assert.Single(other.data.Cast<KbOuterB.KbItem>()).Value);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void MapFromMembersUseTheRequestedDto() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var orders = new[] { new KbOrder { ID = 1, Code = "x" } }.AsQueryable();
                var resultA = DataSourceLoader.Load<KbOrder, KbOrderDtoA>(orders, new SampleLoadOptions {
                    Filter = new object[] { "Label", "=", "A:x" },
                    RequireTotalCount = true
                }, mapper);
                var resultB = DataSourceLoader.Load<KbOrder, KbOrderDtoB>(orders, new SampleLoadOptions {
                    Filter = new object[] { "Label", "=", "B:x" },
                    RequireTotalCount = true
                }, mapper);

                Assert.Equal(1, resultA.totalCount);
                Assert.Equal(1, resultB.totalCount);
                Assert.Equal("A:x", Assert.Single(resultA.data.Cast<KbOrderDtoA>()).Label);
                Assert.Equal("B:x", Assert.Single(resultB.data.Cast<KbOrderDtoB>()).Label);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void RetiredProjectBeforeFilterKeepsDataAndCountConsistent() {
            var mapper = CreateMapper();
            try {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);

                var options = new SampleLoadOptions {
                    Filter = new object[] { "Scaled", ">", 15 },
#pragma warning disable CS0618 // Verify compatibility with callers still setting the retired option.
                    ProjectBeforeFilter = true,
#pragma warning restore CS0618
                    RequireTotalCount = true
                };

                var result = DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, mapper, new { factor = 10 });

                Assert.Equal(2, result.data.Cast<KbPersonDto>().Count());
                Assert.Equal(2, result.totalCount);
            } finally {
                CustomAccessorCompilers.Clear();
            }
        }

        [Fact]
        public void NullMapperIdentifiesTheMissingArgument() {
            var options = new SampleLoadOptions();

            var error = Assert.Throws<ArgumentNullException>(() => DataSourceLoader.Load<KbPerson, KbPersonDto>(People().AsQueryable(), options, null));
            Assert.Equal("mapper", error.ParamName);
        }
    }

}
