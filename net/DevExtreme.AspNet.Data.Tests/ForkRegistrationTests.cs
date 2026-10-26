using AutoMapper;
using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevExtreme.AspNet.Data.Tests {

    public class ForkRegistrationTests : IDisposable {
        public class RegistrationItem {
            public int Value { get; set; }
        }

        public ForkRegistrationTests() {
            CustomAccessorCompilers.Clear();
        }

        public void Dispose() {
            CustomAccessorCompilers.Clear();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void AccessorsWorkWithoutRegisteringAutoMapperProfiles(int registrationKind) {
            // Clear resets the compiler registry but keeps accessor entries. Check that registration activates it again.
            for(var attempt = 0; attempt < 2; attempt++) {
                CustomAccessorCompilers.Clear();
                if(registrationKind == 0)
                    CustomAccessorCompilers.Register<RegistrationItem, int>("Triple", item => item.Value * 3);
                else if(registrationKind == 1)
                    CustomAccessorCompilers.RegisterContext<RegistrationItem, int>("Triple", context => {
                        var factor = (int)context;
                        return item => item.Value * factor;
                    });
                else
                    CustomAccessorCompilers.RegisterContext<RegistrationItem>("Triple", context => {
                        var factor = (int)context;
                        Expression<Func<RegistrationItem, int>> expression = item => item.Value * factor;
                        return expression;
                    });

                var items = new[] { new RegistrationItem { Value = 1 }, new RegistrationItem { Value = 2 } };
                var result = DataSourceLoader.Load<RegistrationItem, RegistrationItem>(items, new SampleLoadOptions {
                    Filter = new object[] { "Triple", "=", 6 },
                    RequireTotalCount = true
                }, null, 3);

                Assert.Equal(2, Assert.Single(result.data.Cast<RegistrationItem>()).Value);
                Assert.Equal(1, result.totalCount);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RepeatedRegistrationPreservesCompilerPrecedence(bool hookFirst) {
            var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
            CustomAccessorCompilers.CompilerFuncWithContext hook = (target, member, context) =>
                member == "Priority" ? Expression.Constant(99) : null;

            if(hookFirst)
                CustomAccessorCompilers.RegisterWithContext(hook);
            CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
            CustomAccessorCompilers.Register<RegistrationItem, int>("Priority", item => 10);
            if(!hookFirst)
                CustomAccessorCompilers.RegisterWithContext(hook);

            for(var i = 0; i < 3; i++) {
                CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
                CustomAccessorCompilers.Register<RegistrationItem, int>("Priority", item => 20);
            }

            var parameter = Expression.Parameter(typeof(RegistrationItem));
            var expression = CustomAccessorCompilers.TryCompile(parameter, "Priority", null);
            Assert.Equal(hookFirst ? 99 : 20, Expression.Lambda<Func<RegistrationItem, int>>(expression, parameter).Compile()(new RegistrationItem()));
            Assert.Equal(2, RegisteredCompilers().Count());
        }

        [Fact]
        public void ConcurrentRegistrationsInstallOneAccessorFinder() {
            var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
            Parallel.For(0, 100, i => {
                switch(i % 3) {
                    case 0:
                        CustomAccessorCompilers.RegisterAutomapperProfiles(mapper);
                        break;
                    case 1:
                        CustomAccessorCompilers.Register<RegistrationItem, int>("Concurrent", item => item.Value);
                        break;
                    default:
                        CustomAccessorCompilers.RegisterContext<RegistrationItem, int>("ConcurrentContext", context => item => item.Value);
                        break;
                }
            });

            Assert.Single(RegisteredCompilers());
        }

        // Duplicate finders return identical results, so inspect the registry to catch unbounded registration growth.
        static IEnumerable<CustomAccessorCompilers.CompilerFuncWithContext> RegisteredCompilers() {
            return (IEnumerable<CustomAccessorCompilers.CompilerFuncWithContext>)typeof(CustomAccessorCompilers)
                .GetField("_compilersWithContext", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        [Fact]
        public void RegistrationDuringCompilationTakesEffectOnTheNextCompilation() {
            var registered = false;
            CustomAccessorCompilers.Register((target, member) => {
                if(!registered) {
                    registered = true;
                    CustomAccessorCompilers.RegisterWithContext((nextTarget, nextMember, context) => Expression.Constant(42));
                }
                return null;
            });

            var parameter = Expression.Parameter(typeof(RegistrationItem));
            Assert.Null(CustomAccessorCompilers.TryCompile(parameter, "Late", null));
            Assert.Equal(42, Assert.IsType<ConstantExpression>(CustomAccessorCompilers.TryCompile(parameter, "Late", null)).Value);
        }

        [Fact]
        public async Task ConcurrentCompilerRegistrationPreservesTheActiveSnapshotAndEveryCallback() {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            CustomAccessorCompilers.RegisterWithContext((target, member, context) => {
                if(member == "Pending") {
                    entered.Set();
                    if(!release.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("The active compiler was not released.");
                }
                return null;
            });
            var parameter = Expression.Parameter(typeof(RegistrationItem));
            var active = Task.Run(() => CustomAccessorCompilers.TryCompile(parameter, "Pending", null));
            try {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                Parallel.For(0, 32, i => {
                    if(i % 2 == 0)
                        CustomAccessorCompilers.Register((target, member) => member == "Worker" + i ? Expression.Constant(i) : null);
                    else
                        CustomAccessorCompilers.RegisterWithContext((target, member, context) => member == "Worker" + i ? Expression.Constant(i) : null);
                });
                CustomAccessorCompilers.Register((target, member) => member == "Pending" ? Expression.Constant(99) : null);
            } finally {
                release.Set();
                await active;
            }

            Assert.Null(await active);
            Assert.Equal(99, Assert.IsType<ConstantExpression>(CustomAccessorCompilers.TryCompile(parameter, "Pending", null)).Value);
            for(var i = 0; i < 32; i++)
                Assert.Equal(i, Assert.IsType<ConstantExpression>(CustomAccessorCompilers.TryCompile(parameter, "Worker" + i, null)).Value);
        }

        [Fact]
        public void ConcurrentContextRegistrationsAndReadsKeepRequestValuesIsolated() {
            Parallel.For(0, 100, offset => {
                CustomAccessorCompilers.RegisterContext<RegistrationItem, int>("RequestValue", context => {
                    var currentOffset = (int)context;
                    return item => item.Value + currentOffset;
                });
                var result = DataSourceLoader.Load<RegistrationItem, RegistrationItem>(
                    new[] { new RegistrationItem { Value = 1 }, new RegistrationItem { Value = 2 } },
                    new SampleLoadOptions { Filter = new object[] { "RequestValue", "=", offset + 2 }, RequireTotalCount = true },
                    null, offset);
                Assert.Equal(2, Assert.Single(result.data.Cast<RegistrationItem>()).Value);
                Assert.Equal(1, result.totalCount);
            });
            Assert.Single(RegisteredCompilers());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConcurrentFirstRegistrationsRetainEveryProperty(bool withContext) {
            const int workers = 4;
            const int rounds = 200;
            var libraries = Enumerable.Range(0, rounds).Select(_ => new AccessorLibrary()).ToArray();
            var readableDuringRegistration = new bool[rounds, workers];
            using var start = new Barrier(workers);
            var tasks = Enumerable.Range(0, workers).Select(worker => Task.Factory.StartNew(() => {
                Expression<Func<RegistrationItem, int>> expression = item => item.Value + worker;
                for(var round = 0; round < rounds; round++) {
                    if(!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Concurrent registration workers did not reach the barrier.");
                    if(withContext)
                        libraries[round].Add(nameof(RegistrationItem), "Member" + worker, context => expression);
                    else
                        libraries[round].Add(nameof(RegistrationItem), "Member" + worker, expression);

                    // Read while other workers may still be adding properties for this type.
                    readableDuringRegistration[round, worker] = libraries[round].Get(Expression.Parameter(typeof(RegistrationItem)), nameof(RegistrationItem), "Member" + worker, null) != null;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            await Task.WhenAll(tasks);

            foreach(var readable in readableDuringRegistration)
                Assert.True(readable);

            foreach(var library in libraries) {
                for(var worker = 0; worker < workers; worker++) {
                    var parameter = Expression.Parameter(typeof(RegistrationItem));
                    var expression = library.Get(parameter, nameof(RegistrationItem), "Member" + worker, null);
                    Assert.NotNull(expression);
                    Assert.Equal(10 + worker, Expression.Lambda<Func<RegistrationItem, int>>(expression, parameter).Compile()(new RegistrationItem { Value = 10 }));
                }
            }
        }
    }
}
