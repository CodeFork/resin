﻿using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public int Id { get; set; }
        public IDictionary<string, IList<string>> Fields { get; set; }

        public static Document FromDictionary(int docId, IDictionary<string, IList<string>> fields)
        {
            return new Document{Id = docId, Fields = fields};
        }
    }

    public class DocumentInfo
    {
        public int Id { get; set; }
        public IDictionary<string, FieldInfo> Fields { get; set; }

    }

    public class FieldInfo
    {
        public int FieldId { get; set; }
        public IList<string> Values { get; set; }
    }
}