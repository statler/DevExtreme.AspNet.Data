using AutoMapper;
using AutoMapper.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DevExtreme.AspNet.Data.Helpers {

    // Owned by one load: prepared expressions must never retain another request's parameters or DTO mapping.
    sealed class MappedAccessorResolver {
        readonly Type _sourceType;
        readonly Lazy<Dictionary<string, LambdaExpression>> _accessors;

        public MappedAccessorResolver(Type sourceType, Type destinationType, IMapper mapper, object parameters) {
            _sourceType = sourceType;
            _accessors = new Lazy<Dictionary<string, LambdaExpression>>(() => {
                var configuration = mapper.ConfigurationProvider.Internal();
                var map = configuration.ResolveTypeMap(sourceType, destinationType);
                var accessors = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal);
                if(map == null)
                    return accessors;
                var members = map.PropertyMaps.Where(p => p.CustomMapExpression != null && !p.Ignored).ToArray();
                if(members.Length == 0)
                    return accessors;

                // GetProjection applies AutoMapper's own object/dictionary parameter substitution and null handling.
                var query = configuration.ProjectionBuilder.GetProjection(sourceType, destinationType, parameters, Array.Empty<MemberPath>());
                var source = query.LetClause?.Parameters[0] ?? query.Projection.Parameters[0];
                foreach(var member in members) {
                    var body = ReadMember(query.Projection.Body, member.DestinationName);
                    if(body == null)
                        continue;
                    if(query.LetClause != null)
                        body = new ProjectionRewriter(query.Projection.Parameters[0], query.LetClause.Body).Visit(body);
                    accessors.Add(member.DestinationName, Expression.Lambda(body, source));
                }
                return accessors;
            });
        }

        public Expression Resolve(Expression target, string member) {
            // DTOs and anonymous grouping results already contain their projected values.
            if(target.Type != _sourceType || !_accessors.Value.TryGetValue(member, out var accessor))
                return null;
            return new ProjectionRewriter(accessor.Parameters[0], target).Visit(accessor.Body);
        }

        static Expression ReadMember(Expression expression, string member) {
            if(expression is MemberInitExpression init) {
                var assignment = init.Bindings.OfType<MemberAssignment>().FirstOrDefault(b => b.Member.Name == member);
                if(assignment != null)
                    return assignment.Expression;
                expression = init.NewExpression;
            }
            if(expression is NewExpression creation && creation.Members != null) {
                for(var i = 0; i < creation.Members.Count; i++) {
                    if(creation.Members[i].Name == member)
                        return creation.Arguments[i];
                }
            }
            return null;
        }

        sealed class ProjectionRewriter : ExpressionVisitor {
            readonly ParameterExpression _parameter;
            readonly Expression _replacement;

            public ProjectionRewriter(ParameterExpression parameter, Expression replacement) {
                _parameter = parameter;
                _replacement = replacement;
            }

            protected override Expression VisitParameter(ParameterExpression node) => node == _parameter ? _replacement : node;

            protected override Expression VisitMember(MemberExpression node) {
                var target = Visit(node.Expression);
                // Inline LetClause members instead of leaving member accesses on constructed temporary objects.
                return ReadMember(target, node.Member.Name) ?? node.Update(target);
            }
        }
    }
}
