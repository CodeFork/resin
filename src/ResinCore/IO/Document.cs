﻿using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public class Document
    {
        public int Id { get; set; }
        public UInt64 Hash { get; set; }

        private readonly IList<Field> _fields;

        public IList<Field> Fields { get { return _fields; } }

        public Document(IList<Field> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }
}