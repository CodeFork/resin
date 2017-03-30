﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.Querying;
using Resin.Sys;

namespace Resin
{
    public class Collector : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly string _directory;
        private readonly IxInfo _ix;
        private readonly IScoringScheme _scorer;
        private readonly int _max;

        public Collector(string directory, IxInfo ix, IScoringScheme scorer, int max)
        {
            _directory = directory;
            _ix = ix;
            _scorer = scorer;
            _max = max;
        }

        public IList<DocumentScore> Collect(QueryContext query)
        {
            var queries = new List<QueryContext> {query}.Concat(query.Children).ToList();

            Scan(queries);
            Score(queries);

            var time = Time();
            var reduced = query.Reduce().ToList();

            Log.DebugFormat("reduced {0} in {1}", query, time.Elapsed);

            return reduced.OrderByDescending(s=>s.Score).ToList();
        }

        private void Scan(IList<QueryContext> queries)
        {
            Parallel.ForEach(queries, DoScan);

            //foreach (var q in queries)
            //{
            //    DoScan(q);
            //}
        }

        private void DoScan(QueryContext query)
        {
            var time = Time();
            var reader = GetTreeReader(query.Field, query.Value);

            if (reader == null)
            {
                query.Terms = Enumerable.Empty<Term>();
            }
            else
            {
                if (query.Fuzzy)
                {
                    query.Terms = reader.Near(query.Value, query.Edits).Select(word => new Term(query.Field, word)).ToList();
                }
                else if (query.Prefix)
                {
                    query.Terms = reader.StartsWith(query.Value).Select(word => new Term(query.Field, word)).ToList();
                }
                else
                {
                    var terms = new List<Term>();
                    Word word;
                    if (reader.HasWord(query.Value, out word))
                    {
                        terms.Add(new Term(query.Field, word));
                    }
                    query.Terms = terms;
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query.AsReadable(), time.Elapsed);

            DoGetPostings(query);
        }

        private void DoGetPostings(QueryContext query)
        {
            var time = Time();

            var postings = DoReadPostings(query.Terms).ToList();

            if (postings.Count > 0)
            {
                var result = postings
                    .Aggregate<IEnumerable<DocumentPosting>, IEnumerable<DocumentPosting>>(
                        null, DocumentPosting.Join);

                query.Postings = result;
            }
            else
            {
                query.Postings = new DocumentPosting[0];
            }

            Log.DebugFormat("read postings for {0} in {1}", query.AsReadable(), time.Elapsed);
        }
        
        private IEnumerable<IEnumerable<DocumentPosting>> DoReadPostings(IEnumerable<Term> terms)
        {
            yield return GetPostings(terms).OrderByDescending(p=>p.Count).Take(_max);
        }

        private IEnumerable<DocumentPosting> GetPostings(IEnumerable<Term> terms)
        {
            using (var reader = new PostingsReader(new FileStream(Path.Combine(_directory, _ix.Name + ".pos"), FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)))
            {
                var addresses = terms.Select(term => term.Word.PostingsAddress).OrderBy(adr => adr.Position).ToList();

                return reader.Get(addresses).SelectMany(x=>x).ToList();
            }
        }

        private void Score(IList<QueryContext> queries)
        {
            Parallel.ForEach(queries, query =>
            {
                query.Scored = DoScore(query.Postings.ToList(), query.Field);
            });
        }

        private IEnumerable<DocumentScore> DoScore(IList<DocumentPosting> postings, string field)
        {
            if (postings.Any())
            {
                var scorer = _scorer.CreateScorer(_ix.DocumentCount.DocCount[field], postings.Count);

                foreach (var posting in postings)
                {
                    var score = scorer.Score(posting);

                    yield return score;
                }
            }
        }

        private ITrieReader GetTreeReader(string field, string token)
        {
            var suffix = token.ToBucketName();
            var fileId = field.ToTrieFileId();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}-{2}.tri", _ix.Name, fileId, suffix));

            if (!File.Exists(fileName)) return null;

            var reader = new MappedTrieReader(fileName);

            return reader;
        }

        private static Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        public void Dispose()
        {
        }
    }
}