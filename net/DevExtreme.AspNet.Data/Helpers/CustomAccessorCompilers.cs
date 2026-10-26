using AutoMapper;
using AutoMapper.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace DevExtreme.AspNet.Data.Helpers {

    public static class CustomAccessorCompilers {
        public delegate Expression CompilerFunc(Expression expr, string accessorText);
        public delegate Expression CompilerFuncWithContext(Expression expr, string accessorText, object runtimeContext);

        static readonly object _registrationLock = new object();
        static volatile CompilerFuncWithContext[] _compilersWithContext = Array.Empty<CompilerFuncWithContext>();
        static bool _accessorFinderRegistered;

        private static readonly AccessorLibrary CustomAccessorLibrary = new AccessorLibrary();
        static readonly AccessorLibrary LegacyMappedAccessorLibrary = new AccessorLibrary();
        static readonly CompilerFuncWithContext AccessorFinder = (target, member, context) =>
            CustomAccessorLibrary.Get(target, target.Type.Name, member, context)
            ?? LegacyMappedAccessorLibrary.Get(target, target.Type.Name, member, context);

        public static void Register(CompilerFunc compilerFunc) {
            RegisterWithContext((target, accessorText, runtimeContext) => compilerFunc(target, accessorText));
        }

        public static void RegisterWithContext(CompilerFuncWithContext compilerFunc) {
            lock(_registrationLock)
                AddCompiler(compilerFunc);
        }

        // Called under _registrationLock. Readers keep a stable snapshot while registrations are added.
        static void AddCompiler(CompilerFuncWithContext compilerFunc) {
            var compilers = new CompilerFuncWithContext[_compilersWithContext.Length + 1];
            Array.Copy(_compilersWithContext, compilers, _compilersWithContext.Length);
            compilers[compilers.Length - 1] = compilerFunc;
            _compilersWithContext = compilers;
        }

        public static void Register<T, U>(string PropertyName, Expression<Func<T, U>> ResolveExpression) {
            CustomAccessorLibrary.Add(PropertyName, ResolveExpression);
            RegisterAccessorFinder();
        }

        public static void RegisterContext<T, U>(string PropertyName, Func<object, Expression<Func<T, U>>> ResolveExprFunc) {
            CustomAccessorLibrary.Add<T>(PropertyName, ResolveExprFunc);
            RegisterAccessorFinder();
        }

        public static void RegisterContext<T>(string PropertyName, Func<object, LambdaExpression> ResolveExprFunc) {
            CustomAccessorLibrary.Add<T>(PropertyName, ResolveExprFunc);
            RegisterAccessorFinder();
        }

        public static void RegisterAutomapperProfiles(IMapper mapper) {

            //Get all automapper config maps and create expressions that can be parsed by devex.aspnet.data
            //Create a dictionary of dictionaries so they can be (efficiently) extracted by the devex library
            //by type and property name
            var allTypeMaps = mapper.ConfigurationProvider.Internal().GetAllTypeMaps();
            foreach(TypeMap map in allTypeMaps) {
                var propertyMaps = map.PropertyMaps;

                foreach(PropertyMap propertyMap in propertyMaps) {
                    var modelMemberType = propertyMap?.TypeMap?.SourceType;
                    string destinationName = propertyMap?.DestinationName;
                    var exp = propertyMap.CustomMapExpression;
                    if(modelMemberType != null && destinationName != null && exp == null) continue;

                    LegacyMappedAccessorLibrary.Add(modelMemberType, destinationName, exp);
                }
            }
            RegisterAccessorFinder();
        }

        private static void RegisterAccessorFinder() {
            lock(_registrationLock) {
                if(_accessorFinderRegistered)
                    return;

                // Keep the finder's position in registration order, including when profiles are registered again.
                AddCompiler(AccessorFinder);
                _accessorFinderRegistered = true;
            }
        }

        internal static Expression TryCompile(Expression expr, string accessorText, object runtimeContext, CompilerFunc mappedAccessor = null) {
            var finderVisited = false;
            foreach(var compiler in _compilersWithContext) {
                Expression result;
                if(mappedAccessor != null && compiler == AccessorFinder) {
                    finderVisited = true;
                    result = CustomAccessorLibrary.Get(expr, expr.Type.Name, accessorText, runtimeContext)
                        ?? mappedAccessor(expr, accessorText);
                } else {
                    result = compiler(expr, accessorText, runtimeContext);
                }
                if(result != null)
                    return result;
            }

            // A typed load can resolve its mapper without global profile registration.
            return mappedAccessor != null && !finderVisited ? mappedAccessor(expr, accessorText) : null;
        }

#if DEBUG
        internal static void Clear() {
            lock(_registrationLock) {
                _compilersWithContext = Array.Empty<CompilerFuncWithContext>();
                _accessorFinderRegistered = false;
            }
        }
#endif

    }

    public class AccessorLibrary {
        readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Accessor>> _typedAccessors = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Accessor>>();
        // Retain the public string-keyed API for callers that use explicitly named libraries.
        readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Accessor>> _dctAccessors = new ConcurrentDictionary<string, ConcurrentDictionary<string, Accessor>>();
        public AccessorLibrary() {
        }

        public void Add(string TypeName, string PropertyName, LambdaExpression ResolveExpression) {
            var _accessor = new Accessor(TypeName, PropertyName, ResolveExpression);
            var expressionForType = _dctAccessors.GetOrAdd(TypeName, _ => new ConcurrentDictionary<string, Accessor>());
            expressionForType[PropertyName] = _accessor;
        }

        public void Add(string TypeName, string PropertyName, Func<object, LambdaExpression> ResolveExprFunc) {
            var _accessor = new Accessor(TypeName, PropertyName, ResolveExprFunc);
            var expressionForType = _dctAccessors.GetOrAdd(TypeName, _ => new ConcurrentDictionary<string, Accessor>());
            expressionForType[PropertyName] = _accessor;
        }
        public void Add<T>(string PropertyName, Func<object, LambdaExpression> ResolveExprFunc) {
            Add(typeof(T), PropertyName, ResolveExprFunc);
        }

        public void Add<T>(string PropertyName, LambdaExpression ResolveExpression) {
            Add(typeof(T), PropertyName, ResolveExpression);
        }

        public void Add<T, U>(string PropertyName, Expression<Func<T, U>> ResolveExpression) {
            Add(typeof(T), PropertyName, ResolveExpression);
        }

        internal void Add(Type type, string propertyName, LambdaExpression expression) {
            Add(type, propertyName, _ => expression);
        }

        internal void Add(Type type, string propertyName, Func<object, LambdaExpression> resolveExpression) {
            var accessors = _typedAccessors.GetOrAdd(type, _ => new ConcurrentDictionary<string, Accessor>());
            accessors[propertyName] = new Accessor(type.FullName, propertyName, resolveExpression);
        }

        public Expression Get(Expression target, string TypeName, string PropertyName, object runtimeContext) {
            Accessor accessor;
            if((_typedAccessors.TryGetValue(target.Type, out var typedAccessors) && typedAccessors.TryGetValue(PropertyName, out accessor))
                || (_dctAccessors.TryGetValue(TypeName, out var namedAccessors) && namedAccessors.TryGetValue(PropertyName, out accessor))) {
                var expression = accessor.GetResolvedExpression(runtimeContext);
                return new ParameterVisitor(expression.Parameters, target).Visit(expression.Body);
            }

            return null;
        }
    }

    public class Accessor {
        public string TypeName { get; set; }
        public string PropertyName { get; set; }
        Func<object, LambdaExpression> ResolveExpression { get; set; }

        public Accessor() {
        }
        public Accessor(string typeName, string propertyName, LambdaExpression resolveExpression) {
            TypeName = typeName;
            PropertyName = propertyName;
            ResolveExpression = (o) => resolveExpression;
        }
        public Accessor(string typeName, string propertyName, Func<object, LambdaExpression> resolveExpressionFunc) {
            TypeName = typeName;
            PropertyName = propertyName;
            ResolveExpression = resolveExpressionFunc;
        }

        public LambdaExpression GetResolvedExpression(object runtimeContext) {
            return ResolveExpression(runtimeContext);
        }
    }

    public class ParameterVisitor : ExpressionVisitor {
        private readonly ReadOnlyCollection<ParameterExpression> _from;
        private readonly Expression _to;
        public ParameterVisitor(
            ReadOnlyCollection<ParameterExpression> from,
            ParameterExpression to) : this(from, (Expression)to) {
        }

        public ParameterVisitor(
            ReadOnlyCollection<ParameterExpression> from,
            Expression to) {
            if(from == null) throw new ArgumentNullException("from");
            if(to == null) throw new ArgumentNullException("to");
            this._from = from;
            this._to = to;
        }
        protected override Expression VisitParameter(ParameterExpression node) {
            for(int i = 0; i < _from.Count; i++) {
                if(node == _from[i]) return _to;
            }
            return node;
        }
    }
}
