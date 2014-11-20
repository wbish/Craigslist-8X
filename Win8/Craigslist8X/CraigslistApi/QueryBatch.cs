using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WB.CraigslistApi
{
    public class QueryBatch
    {
        public QueryBatch(IEnumerable<Query> queries)
        {
            if (queries == null)
            {
                throw new ArgumentNullException("queries");
            }

            this._queries = queries.ToList();
        }

        public static QueryBatch BuildQueryBatch(IEnumerable<CraigCity> cities, Query template)
        {
            List<Query> queries = new List<Query>();

            foreach (var city in cities)
            {
                Query q = template.Clone();
                q.City = city;
                queries.Add(q);
            }

            return new QueryBatch(queries);
        }

        public async Task<List<QueryResult>> Execute(CancellationToken token)
        {
            List<Task<QueryResult>> tasks = new List<Task<QueryResult>>(this._queries.Count);

            for (int i = 0; i < this._queries.Count; ++i)
            {
                var q = this._queries[i];
                tasks.Add(q.Execute(token));
            }

            await Task.WhenAll(tasks.ToArray());

            List<QueryResult> qrs = new List<QueryResult>(tasks.Count);
            foreach (var task in tasks)
            {
                QueryResult qr = await task;

                if (qr == null)
                    return null;

                qrs.Add(qr);
            }                

            return qrs;
        }

        public List<Query> Queries
        {
            get
            {
                return this._queries;
            }
        }

        List<Query> _queries;
    }
}
