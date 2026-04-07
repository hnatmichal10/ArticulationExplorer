using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArticulationExplorer.Model
{
    internal class AlgorithmState
    {
        public Dictionary<Node, int> Disc { get; set; } = new();
        public Dictionary<Node, int> Low { get; set; } = new();
        public HashSet<Node> Articulations { get; set; } = new();
        public List<(Node From, Node To)> Bridges { get; set; } = new();
        public List<List<Node>> Blocks { get; set; } = new();
        public Stack<DFSFrame> Stack { get; set; } = new();
        public Stack<(Node From, Node To)> EdgeStack { get; set; } = new();

        public Node? CurrentNode { get; set; }
        public Node? CurrentNeighbor { get; set; }
        public string? Description { get; set; }

    }
}
