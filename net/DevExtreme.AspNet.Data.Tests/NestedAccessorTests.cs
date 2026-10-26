using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {
    public class NestedAccessorTests : IDisposable {
        public class Root {
            public int Id { get; set; }
            public Parent Parent { get; set; }
        }
        public class Parent {
            public string Name { get; set; }
            public int Number { get; set; }
            public Parent Next { get; set; }
            public List<Parent> Children { get; set; } = new List<Parent>();
        }

        public NestedAccessorTests() => CustomAccessorCompilers.Clear();
        public void Dispose() => CustomAccessorCompilers.Clear();

        static Root[] Rows() => new[] {
            new Root { Id = 1 },
            new Root { Id = 2, Parent = new Parent { Name = "p2", Number = 2, Next = new Parent { Name = "MiXeD" }, Children = { new Parent { Name = "p2" } } } },
            new Root { Id = 3, Parent = new Parent { Name = "p3", Number = 3 } }
        };

        [Theory]
        [InlineData("Parent.Alias")]
        [InlineData("Parent.Self.Alias")]
        public void MemberAndIdentityAccessorsCanBeAppliedToNestedTargets(string path) {
            CustomAccessorCompilers.Register<Parent, string>("Alias", parent => parent.Name);
            CustomAccessorCompilers.Register<Parent, Parent>("Self", parent => parent);
            var result = DataSourceLoader.Load(Rows(), new SampleLoadOptions {
                Filter = new object[] { path, "=", "p2" }, RequireTotalCount = true
            });
            Assert.Equal(2, Assert.Single(result.data.Cast<Root>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Fact]
        public void MultiLevelAccessorsKeepStringAndNullHandling() {
            CustomAccessorCompilers.Register<Parent, string>("Alias", parent => parent.Name);
            var result = DataSourceLoader.Load(Rows(), new SampleLoadOptions {
                Filter = new object[] { "Parent.Next.Alias", "contains", "IX" }, RequireTotalCount = true
            });
            Assert.Equal(2, Assert.Single(result.data.Cast<Root>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NestedContextAccessorsReceiveTheRequestContext(bool untyped) {
            var context = new { Offset = 100 };
            if(untyped)
                CustomAccessorCompilers.RegisterContext<Parent>("Adjusted", received => {
                    Assert.Same(context, received);
                    Expression<Func<Parent, int>> expression = parent => parent.Number + context.Offset;
                    return expression;
                });
            else
                CustomAccessorCompilers.RegisterContext<Parent, int>("Adjusted", received => {
                    Assert.Same(context, received);
                    return parent => parent.Number + context.Offset;
                });

            var result = DataSourceLoader.Load<Root, Root>(Rows(), new SampleLoadOptions {
                Filter = new object[] { "Parent.Adjusted", "=", 102 }, RequireTotalCount = true
            }, null, context);
            Assert.Equal(2, Assert.Single(result.data.Cast<Root>()).Id);
            Assert.Equal(1, result.totalCount);
        }

        [Fact]
        public void ParameterReplacementPreservesNestedLambdaParameters() {
            CustomAccessorCompilers.Register<Parent, bool>("HasMatchingChild", parent => parent.Children.Any(child => child.Name == parent.Name));
            var result = DataSourceLoader.Load(Rows(), new SampleLoadOptions {
                Filter = new object[] { "Parent.HasMatchingChild", "=", true }
            });
            Assert.Equal(2, Assert.Single(result.data.Cast<Root>()).Id);
        }

        [Fact]
        public void WholePathHookKeepsPrecedenceOverNestedAccessors() {
            var calls = new List<string>();
            var context = new object();
            CustomAccessorCompilers.RegisterWithContext((target, member, received) => {
                Assert.Same(context, received);
                calls.Add(target.Type.Name + ":" + member);
                return member == "Parent.Blocked" ? Expression.Constant("redacted") : null;
            });
            CustomAccessorCompilers.Register<Parent, string>("Blocked", parent => parent.Name);
            var expression = new FilterExpressionCompiler(typeof(Root), true, runtimeResolutionContext: context)
                .Compile(new object[] { "Parent.Blocked", "=", "redacted" });
            Assert.Equal(new[] { "Root:Parent.Blocked" }, calls);
            Assert.True(((Func<Root, bool>)expression.Compile())(Rows()[1]));
        }

        [Fact]
        public void SegmentHookSeesNestedTypeAndCanOverrideItsAccessor() {
            var context = new object();
            var calls = new List<string>();
            CustomAccessorCompilers.RegisterWithContext((target, member, received) => {
                Assert.Same(context, received);
                calls.Add(target.Type.Name + ":" + member);
                return target.Type == typeof(Parent) && member == "SegmentBlocked" ? Expression.Constant("redacted") : null;
            });
            CustomAccessorCompilers.Register<Parent, string>("SegmentBlocked", parent => parent.Name);
            var expression = new FilterExpressionCompiler(typeof(Root), true, runtimeResolutionContext: context)
                .Compile(new object[] { "Parent.SegmentBlocked", "=", "redacted" });
            Assert.Equal(new[] { "Root:Parent.SegmentBlocked", "Root:Parent", "Parent:SegmentBlocked" }, calls);
            Assert.True(((Func<Root, bool>)expression.Compile())(Rows()[1]));
        }
    }
}
