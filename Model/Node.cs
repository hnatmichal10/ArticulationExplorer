using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace ArticulationExplorer.Model
{
    class Node
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public required Ellipse Shape { get; set; }
        public required TextBlock Label { get; set; }
    }
}
