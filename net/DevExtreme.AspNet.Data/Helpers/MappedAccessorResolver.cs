using AutoMapper;
using AutoMapper.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DevExtreme.AspNet.Data.Helpers {

    // Owned by one load: prepared expressions must never retain another request's parameters or DTO mapping.
    sealed class MappedAccessorResolver {
        readonly Type _sourceType;
        readonly IMapper _mapper;
        readonly object _parameters;
        readonly ConcurrentDictionary<(Type Source, Type Destination), Lazy<MapMembers>> _maps =
            new ConcurrentDictionary<(Type, Type), Lazy<MapMembers>>();

        /// <summary>The DTO a typed load returns. Selectors on the source item may only name its members.</summary>
        public Type DestinationType { get; }

        readonly HashSet<string> _serverKeys;

        public MappedAccessorResolver(Type sourceType, Type destinationType, IMapper mapper, object parameters,
            IEnumerable<string> serverKeys = null) {
            _sourceType = sourceType;
            DestinationType = destinationType;
            _mapper = mapper;
            _parameters = parameters;
            _serverKeys = new HashSet<string>(serverKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        /// <summary>The destination type a selector compiled against <paramref name="target"/> must resolve on, or null when unchecked.</summary>
        public Type DestinationTypeFor(Expression target) => target.Type == _sourceType ? DestinationType : null;

        /// <summary>
        /// A member the map from <paramref name="source"/> to <paramref name="destination"/> ignores: the DTO never carries a
        /// value for it, so it is not the entity's to read.
        /// </summary>
        public bool IsIgnored(Type source, Type destination, string member) => For(source, destination).Ignored.Contains(member);

        /// <summary>The load's primary key, which the loader itself sorts and pages by.</summary>
        public bool IsServerKey(string selector) => _serverKeys.Contains(selector);

        /// <summary>A root member of the load's DTO that the map does not read by its own name from the source item.</summary>
        public Expression Resolve(Expression target, string member) {
            // DTOs and anonymous grouping results already contain their projected values.
            if(target.Type != _sourceType)
                return null;
            return Read(For(_sourceType, DestinationType), target, member);
        }

        /// <summary>
        /// A member of a nested DTO, read through that DTO's own map from the entity <paramref name="target"/> (its MapFrom,
        /// flattening or renaming), or null when the map reads it by its own name.
        /// </summary>
        public Expression ResolveNested(Expression target, Type destination, string member)
            => Read(For(target.Type, destination), target, member);

        MapMembers For(Type source, Type destination)
            => _maps.GetOrAdd((source, destination), key => new Lazy<MapMembers>(() => Build(key.Source, key.Destination))).Value;

        MapMembers Build(Type source, Type destination) {
            var configuration = _mapper.ConfigurationProvider.Internal();
            var map = configuration.ResolveTypeMap(source, destination);
            var result = new MapMembers();
            if(map == null)
                return result;
            result.Ignored.UnionWith(map.PropertyMaps.Where(p => p.Ignored).Select(p => p.DestinationName));
            // Read from the projection every member the map does not read by its own name: MapFrom members, members AutoMapper
            // flattens from a path (OwnerEmail <- Owner.Email) and members mapped from a differently named source member, so a
            // selector gets the DTO's value rather than a same-named entity member.
            var members = map.PropertyMaps.Where(p => !p.Ignored && (p.CustomMapExpression != null || p.SourceMembers.Length > 1
                || p.SourceMembers.Length == 1 && !string.Equals(p.SourceMembers[0].Name, p.DestinationName, StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.DestinationName).ToList();
            if(members.Count == 0)
                return result;

            // GetProjection applies AutoMapper's own object/dictionary parameter substitution and null handling.
            var query = configuration.ProjectionBuilder.GetProjection(source, destination, _parameters, Array.Empty<MemberPath>());
            var parameter = query.LetClause?.Parameters[0] ?? query.Projection.Parameters[0];
            foreach(var member in members) {
                var body = ReadMember(query.Projection.Body, member);
                if(body == null)
                    continue;
                if(query.LetClause != null)
                    body = new ProjectionRewriter(query.Projection.Parameters[0], query.LetClause.Body).Visit(body);
                result.Accessors[member] = Expression.Lambda(body, parameter);
            }
            return result;
        }

        static Expression Read(MapMembers map, Expression target, string member) {
            // Matched as the DTO check matches members: exact case first, then any case.
            if(!map.Accessors.TryGetValue(member, out var accessor)) {
                accessor = map.Accessors.FirstOrDefault(p => string.Equals(p.Key, member, StringComparison.OrdinalIgnoreCase)).Value;
                if(accessor == null)
                    return null;
            }
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

        sealed class MapMembers {
            public readonly Dictionary<string, LambdaExpression> Accessors = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal);
            public readonly HashSet<string> Ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
