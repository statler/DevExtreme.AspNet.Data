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

        /// <summary>The DTO a typed load returns. Selectors on the source item may only name its members.</summary>
        public Type DestinationType { get; }

        readonly HashSet<string> _serverKeys;
        readonly Lazy<HashSet<string>> _ignored;

        public MappedAccessorResolver(Type sourceType, Type destinationType, IMapper mapper, object parameters,
            IEnumerable<string> serverKeys = null) {
            _sourceType = sourceType;
            DestinationType = destinationType;
            _serverKeys = new HashSet<string>(serverKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            _ignored = new Lazy<HashSet<string>>(() => new HashSet<string>(
                mapper.ConfigurationProvider.Internal().ResolveTypeMap(sourceType, destinationType)?.PropertyMaps
                    .Where(p => p.Ignored).Select(p => p.DestinationName) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase));
            _accessors = new Lazy<Dictionary<string, LambdaExpression>>(() => {
                var configuration = mapper.ConfigurationProvider.Internal();
                var map = configuration.ResolveTypeMap(sourceType, destinationType);
                var accessors = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal);
                if(map == null)
                    return accessors;
                // MapFrom members, and members AutoMapper flattens from a path (OwnerEmail <- Owner.Email): read both from
                // the projection, so a selector gets the DTO's value rather than a same-named entity member.
                var members = map.PropertyMaps.Where(p => !p.Ignored && (p.CustomMapExpression != null || p.SourceMembers.Length > 1)).ToArray();
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

        /// <summary>The destination type a selector compiled against <paramref name="target"/> must resolve on, or null when unchecked.</summary>
        public Type DestinationTypeFor(Expression target) => target.Type == _sourceType ? DestinationType : null;

        /// <summary>A DTO member the map ignores: the DTO never carries a value for it, so it is not the entity's to read.</summary>
        public bool IsIgnored(string member) => _ignored.Value.Contains(member);

        /// <summary>The load's primary key, which the loader itself sorts and pages by.</summary>
        public bool IsServerKey(string selector) => _serverKeys.Contains(selector);

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
