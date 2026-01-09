using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace ArticulationExplorer.Model
{
    class Edge
    {
        public required Node From { get; set; }
        public required Node To { get; set; }

        public required Line Shape { get; set; }
        public required Line Hitbox { get; set; }
    }
}
