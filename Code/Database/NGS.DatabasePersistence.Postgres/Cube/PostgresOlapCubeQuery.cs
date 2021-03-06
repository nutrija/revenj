﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using NGS.DatabasePersistence.Postgres.QueryGeneration;
using NGS.DatabasePersistence.Postgres.QueryGeneration.QueryComposition;
using NGS.DatabasePersistence.Postgres.QueryGeneration.Visitors;
using NGS.DomainPatterns;
using NGS.Extensibility;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure;

namespace NGS.DatabasePersistence.Postgres
{
	public abstract class PostgresOlapCubeQuery<TSource> : IOlapCubeQuery
	{
		private readonly IServiceLocator Locator;
		private readonly IDatabaseQuery DatabaseQuery;

		protected abstract string Source { get; }
		protected readonly Dictionary<string, Func<string, string>> CubeDimensions = new Dictionary<string, Func<string, string>>();
		protected readonly Dictionary<string, Func<string, string>> CubeFacts = new Dictionary<string, Func<string, string>>();

		protected PostgresOlapCubeQuery(IServiceLocator locator)
		{
			Contract.Requires(locator != null);

			this.Locator = locator;
			this.DatabaseQuery = Locator.Resolve<IDatabaseQuery>();
		}

		public IEnumerable<string> Dimensions { get { return CubeDimensions.Keys; } }
		public IEnumerable<string> Facts { get { return CubeFacts.Keys; } }

		private void ValidateInput(List<string> usedDimensions, List<string> usedFacts, IEnumerable<string> customOrder)
		{
			if (usedDimensions.Count == 0 && usedFacts.Count == 0)
				throw new ArgumentException("Cube must have at least one dimension or fact.");

			foreach (var d in usedDimensions)
				if (!CubeDimensions.ContainsKey(d))
					throw new ArgumentException("Unknown dimension: {0}. Use Dimensions property for available dimensions".With(d));

			foreach (var f in usedFacts)
				if (!CubeFacts.ContainsKey(f))
					throw new ArgumentException("Unknown fact: {0}. Use Facts property for available facts".With(f));

			foreach (var o in customOrder)
				if (!usedDimensions.Contains(o) && !usedFacts.Contains(o))
					throw new ArgumentException("Invalid order: {0}. Order can be only field from used dimensions and facts.".With(o));
		}

		public DataTable Analyze<TFilter>(
			IEnumerable<string> dimensions,
			IEnumerable<string> facts,
			IEnumerable<KeyValuePair<string, bool>> order,
			ISpecification<TFilter> filter,
			int? limit,
			int? offset)
		{
			var usedDimensions = new List<string>();
			var usedFacts = new List<string>();
			var customOrder = new List<KeyValuePair<string, bool>>();

			if (dimensions != null)
				usedDimensions.AddRange(dimensions);
			if (facts != null)
				usedFacts.AddRange(facts);
			if (order != null)
				foreach (var o in order)
					if (o.Key != null)
						customOrder.Add(new KeyValuePair<string, bool>(o.Key, o.Value));

			ValidateInput(usedDimensions, usedFacts, customOrder.Select(it => it.Key));

			var sb = new StringBuilder();
			var alias = filter != null ? filter.IsSatisfied.Parameters.First().Name : "it";
			sb.Append("SELECT ");
			foreach (var d in usedDimensions)
				sb.AppendFormat("{0} AS \"{1}\", ", CubeDimensions[d](alias), d);
			foreach (var f in usedFacts)
				sb.AppendFormat("{0} AS \"{1}\", ", CubeFacts[f](alias), f);
			sb.Length -= 2;
			sb.AppendLine();
			sb.AppendFormat("FROM {0} \"{1}\"", Source, alias);
			sb.AppendLine();

			if (filter != null)
			{
				var cf = Locator.Resolve<IPostgresConverterFactory>();
				var ep = Locator.Resolve<IExtensibilityProvider>();
				var qp =
					new MainQueryParts(
						Locator,
						cf,
						ep.ResolvePlugins<IQuerySimplification>(),
						ep.ResolvePlugins<IExpressionMatcher>(),
						ep.ResolvePlugins<IMemberMatcher>(),
						new IProjectionMatcher[0]);
				var linq = new Queryable<TSource>(new QueryExecutor(DatabaseQuery, Locator, cf, ep)).Filter(filter);
				var parser = QueryParser.CreateDefault();
				var model = parser.GetParsedQuery(linq.Expression);
				if (model.BodyClauses.Count > 0)
				{
					sb.AppendLine("WHERE");
					for (int i = 0; i < model.BodyClauses.Count; i++)
					{
						var wc = model.BodyClauses[i] as WhereClause;
						if (wc == null)
							continue;
						sb.Append("	");
						if (i > 0)
							sb.Append("AND ");
						sb.Append(qp.GetSqlExpression(wc.Predicate));
					}
				}
			}
			sb.AppendLine();
			if (usedDimensions.Count > 0)
			{
				sb.Append("GROUP BY ");
				sb.AppendLine(string.Join(", ", usedDimensions.Select(it => CubeDimensions[it](alias))));
			}
			if (customOrder.Count > 0)
			{
				sb.Append("ORDER BY ");
				sb.AppendLine(string.Join(", ", customOrder.Select(it => "\"{0}\" {1}".With(it.Key, it.Value ? string.Empty : "DESC"))));
			}
			if (limit != null)
				sb.Append("LIMIT ").Append(limit.Value).AppendLine();
			if (offset != null)
				sb.Append("OFFSET ").Append(offset.Value).AppendLine();
			return DatabaseQuery.Fill(sb.ToString());
		}
	}
}
