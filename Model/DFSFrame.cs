using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArticulationExplorer.Model
{
    internal class DFSFrame
    {
        public Node? Node;
        public Node? Parent;
        public int NeighborIndex;
        public int Children;
    }
}
