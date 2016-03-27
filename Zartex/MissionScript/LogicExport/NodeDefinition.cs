﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zartex
{
    public class NodeDefinition
    {
        public byte OpCode { get; set; }

        public short StringId { get; set; }

        public NodeColor Color { get; set; }

        public short Reserved { get; set; }
        public short Flags { get; set; }

        public List<NodeProperty> Properties { get; set; }
    }
}
