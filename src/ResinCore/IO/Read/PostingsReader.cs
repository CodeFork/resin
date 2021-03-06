﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public class PostingsReader : BlockReader<List<DocumentPosting>>
    {
        public PostingsReader(Stream stream, bool leaveOpen = false)
            : base(stream, leaveOpen)
        {
        }
        protected override List<DocumentPosting> Deserialize(byte[] data)
        {
            return Serializer.DeserializePostings(data).ToList();
        }

        public static IEnumerable<IList<DocumentPosting>> ReadPostings(string directory, IxInfo ix, IEnumerable<Term> terms)
        {
            var posFileName = Path.Combine(directory, String.Format("{0}.{1}", ix.VersionId, "pos"));
            var addresses = terms.Select(term => term.Word.PostingsAddress.Value).OrderBy(adr => adr.Position).ToList();

            using (var reader = new PostingsReader(new FileStream(posFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)))
            {
                var postings = reader.Get(addresses).SelectMany(x => x).ToList();
                yield return postings;
            }
        }
    }
}