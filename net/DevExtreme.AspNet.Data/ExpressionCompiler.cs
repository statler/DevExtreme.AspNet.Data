using DevExtreme.AspNet.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace DevExtreme.AspNet.Data {

    public abstract class ExpressionCompiler {
        protected readonly Type ItemType;
        protected readonly bool GuardNulls;
        public object RuntimeResolutionContext;
        internal MappedAccessorResolver MappedAccessor { get; set; }
        CustomAccessorCompilers.CompilerFunc MappedResolve => MappedAccessor == null ? null : MappedAccessor.Resolve;

        public ExpressionCompiler(Type itemType, bool guardNulls, object runtimeResolutionContext = null) {
            ItemType = itemType;
            GuardNulls = guardNulls;
            RuntimeResolutionContext = runtimeResolutionContext;
        }

        protected internal Expression CompileAccessorExpression(Expression target, string clientExpr, Action<List<Expression>> customizeProgression = null, bool liftToNullable = false) {
            var customResult = CustomAccessorCompilers.TryCompile(target, clientExpr, RuntimeResolutionContext, MappedResolve);
            var progression = new List<Expression> { target };
            if(customResult != null) {
                // Custom accessors still need string/group transformations and the normal null handling.
                progression.Add(customResult);
            } else {
                var clientExprItems = clientExpr.Split('.');
                var currentTarget = target;

                // A typed load queries the source item but returns the DTO. A selector may only name what the DTO
                // exposes, so each segment must also be a member of the DTO, or of the previous segment's DTO type.
                // The load's own primary key is the server's, not the request's.
                var destination = MappedAccessor?.DestinationTypeFor(target);
                if(destination != null && MappedAccessor.IsServerKey(clientExpr))
                    destination = null;
                if(destination != null && clientExprItems.All(item => item == "this"))
                    throw NotAMember(clientExpr, destination);

                for(var i = 0; i < clientExprItems.Length; i++) {
                    var clientExprItem = clientExprItems[i];

                    if(i == 0 && clientExprItem == "this")
                        continue;

                    var checkSegment = false;
                    if(Utils.IsNullable(currentTarget.Type)) {
                        clientExprItem = "Value";
                        i--;
                    } else {
                        checkSegment = destination != null;
                    }

                    if(currentTarget.Type == typeof(ExpandoObject)) {
                        if(checkSegment) destination = DestinationMemberType(destination, clientExprItem, null);
                        currentTarget = ReadExpando(currentTarget, clientExprItem);
                    } else if(DynamicBindingHelper.ShouldUseDynamicBinding(currentTarget.Type)) {
                        if(checkSegment) destination = DestinationMemberType(destination, clientExprItem, null);
                        currentTarget = DynamicBindingHelper.CompileGetMember(currentTarget, clientExprItem);
                    } else {
                        var customResultSplit = CustomAccessorCompilers.TryCompile(currentTarget, clientExprItem, RuntimeResolutionContext, MappedResolve);
                        // A registered accessor on the source item itself (e.g. CustomRegisterValue_12) is the server's
                        // choice, so it may lead a path the DTO does not declare; the rest is checked against its result.
                        // Deeper in a path it is refused like any member the DTO does not declare.
                        if(checkSegment) {
                            // A root member the map ignores carries no value in the DTO; refuse it unless a registered
                            // accessor supplies it, rather than read a same-named entity column the DTO hides.
                            if(currentTarget == target && customResultSplit == null && MappedAccessor.IsIgnored(clientExprItem))
                                throw NotAMember(clientExprItem, destination);
                            destination = DestinationMemberType(destination, clientExprItem,
                                currentTarget == target ? customResultSplit?.Type : null);
                        }
                        if(customResultSplit != null) {
                            currentTarget = customResultSplit;
                        }
                        else currentTarget = FixReflectedType(GetPropertyOrField(currentTarget, clientExprItem));
                    }

                    progression.Add(currentTarget);
                }
            }

            customizeProgression?.Invoke(progression);

            if(GuardNulls && progression.Count > 1 || liftToNullable && progression.Count > 2) {
                var lastIndex = progression.Count - 1;
                var last = progression[lastIndex];
                if(Utils.CanAssignNull(target.Type) && !Utils.CanAssignNull(last.Type))
                    progression[lastIndex] = Expression.Convert(last, Utils.MakeNullable(last.Type));
            }

            return CompileNullGuard(progression);
        }

        Expression CompileNullGuard(IEnumerable<Expression> progression) {
            var last = progression.Last();
            var lastType = last.Type;

            if(!GuardNulls)
                return last;

            Expression allTests = null;

            foreach(var i in progression) {
                if(i == last)
                    break;

                var type = i.Type;
                if(!Utils.CanAssignNull(type))
                    continue;

                var test = Expression.Equal(i, Expression.Constant(null, type));
                if(allTests == null)
                    allTests = test;
                else
                    allTests = Expression.OrElse(allTests, test);
            }

            if(allTests == null)
                return last;

            return Expression.Condition(
                allTests,
                Expression.Constant(Utils.GetDefaultValue(lastType), lastType),
                last
            );
        }

        public ParameterExpression CreateItemParam() {
            return Expression.Parameter(ItemType, "obj");
        }

        internal static void ForceToString(List<Expression> progression) {
            var last = progression.Last();
            if(last.Type != typeof(String))
                progression.Add(Expression.Call(last, typeof(Object).GetMethod(nameof(Object.ToString))));
        }

        static Expression ReadExpando(Expression expando, string member) {
            return Expression.Property(
                Expression.Convert(expando, typeof(IDictionary<string, object>)),
                "Item",
                Expression.Constant(member)
            );
        }

        // Matched as Expression.PropertyOrField matches: public instance members, case-insensitive, exact case first.
        // registeredType: the result type of a registered accessor that may stand in for a member the DTO lacks.
        static Type DestinationMemberType(Type destination, string member, Type registeredType) {
            var candidates = destination.GetMember(member, MemberTypes.Property | MemberTypes.Field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
            var found = candidates.FirstOrDefault(m => m.Name == member) ?? candidates.FirstOrDefault();
            var type = found switch {
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => registeredType ?? throw NotAMember(member, destination)
            };
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        // The message and parameter name Expression.PropertyOrField uses for an unknown member.
        static ArgumentException NotAMember(string member, Type type)
            => new ArgumentException($"'{member}' is not a member of type '{type}'", "propertyOrFieldName");

        internal static MemberExpression GetPropertyOrField(Expression expression, string propertyOrFieldName) {
            MemberExpression memberExpr;
            try {
                memberExpr = Expression.PropertyOrField(expression, propertyOrFieldName);
            } catch(AmbiguousMatchException) {
                memberExpr = GetDeclaredOnlyProperty(expression, propertyOrFieldName);
            }
            return memberExpr;
        }

        static MemberExpression GetDeclaredOnlyProperty(Expression expression, string propertyOrFieldName) {
            PropertyInfo pi = expression.Type.GetProperty(propertyOrFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly);
            if(pi != null)
                return Expression.Property(expression, pi);
            throw new ArgumentException($"'{propertyOrFieldName}' is not a member of type '{expression.Type}'", nameof(propertyOrFieldName));
        }

        static MemberExpression FixReflectedType(MemberExpression expr) {
            var member = expr.Member;
            var declaringType = member.DeclaringType;

            if(member.ReflectedType != declaringType) {
                switch(member.MemberType) {
                    case MemberTypes.Property:
                        return Expression.Property(expr.Expression, declaringType, member.Name);
                    case MemberTypes.Field:
                        return Expression.Field(expr.Expression, declaringType, member.Name);
                }
            }

            return expr;
        }
    }

}
