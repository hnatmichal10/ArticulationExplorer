using ArticulationExplorer.Model;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ArticulationExplorer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            UpdateControls();

            GraphCanvas.MouseLeftButtonDown += GraphCanvas_MouseLeftButtonDown;
        }
        //id vrcholu
        private int nodeCounter = 0;

        //vrcholy
        private List<Node> nodes = new();

        //pro drag vrcholu
        private Node? draggedNode = null;
        private Point mouseDownPos;
        private bool isDragging = false;

        //pro spojeni vrcholu pomoci hran
        private Node? selectedNodeForEdge = null;
        private List<Edge> edges = new();
        private bool ignoreNextNodeClick = false;

        //pro tarjana
        private int dfsTime;
        private HashSet<Node> articulationPoints = new();

        //pro krokovani
        private Dictionary<Node, int> disc = new();
        private Dictionary<Node, int> low = new();
        private Stack<DFSFrame> dfsStack = new();
        private List<AlgorithmState> history = new();
        private List<(Node From, Node To)> bridges = new();
        private List<List<(Node From, Node To)>> blocks = new();
        private Stack<(Node From, Node To)> edgeStack = new();
        private int historyIndex = -1;
        private bool isSteppingMode = false;
        private List<(Node From, Node To)> traversedEdgePairs = new();

        //prepina grafu a matice
        private bool showingMatrix = false;

        //preskrtnuti lifo vrcholu a hran
        private List<StackHistoryItem<Node>> dfsHistory = new();
        private List<StackHistoryItem<(Node From, Node To)>> edgeHistory = new();

        //pridani vrcholu
        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isSteppingMode)
                return;

            if (e.Source == GraphCanvas)
            {
                Point p = e.GetPosition(GraphCanvas);

                // hitbox
                Node? hitNode = GetNodeAtPosition(p);

                if (hitNode != null)
                {
                    return;
                }

                AddNode(p.X, p.Y);
                ignoreNextNodeClick = true;

                UpdateControls();
            }
        }

        private void AddNode(double x, double y)
        {
            string nodeName = GetNextAvailableNodeName();

            //vizualni vrchol
            Ellipse ellipse = new Ellipse
            {
                Width = 30,
                Height = 30,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            //text
            TextBlock label = new TextBlock
            {
                Text = nodeName,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false
            };

            //pozice
            Canvas.SetLeft(ellipse, x - 15);
            Canvas.SetTop(ellipse, y - 15);

            Canvas.SetLeft(label, x - 5);
            Canvas.SetTop(label, y - 8);

            //objekt
            Node node = new Node
            {
                Id = nodeCounter,
                Name = nodeName,
                X = x,
                Y = y,
                Shape = ellipse,
                Label = label
            };

            nodes.Add(node);

            //pridani na canvas
            GraphCanvas.Children.Add(ellipse);
            GraphCanvas.Children.Add(label);

            //drag vrcholu
            ellipse.MouseLeftButtonDown += (s, e) =>
            {
                if (isSteppingMode)
                    return;

                mouseDownPos = e.GetPosition(GraphCanvas);
                draggedNode = node;
                isDragging = false;

                ellipse.CaptureMouse();
                e.Handled = true;
            };

            ellipse.MouseLeftButtonUp += (s, e) =>
            {
                ellipse.ReleaseMouseCapture();

                if (!isDragging)
                {
                    if (ignoreNextNodeClick)
                    {
                        ignoreNextNodeClick = false;
                    }
                    else
                    {
                        HandleNodeClick(node);
                    }
                }

                draggedNode = null;
                isDragging = false;
                e.Handled = true;
            };

            GraphCanvas.MouseMove += (s, e) =>
            {
                if (isSteppingMode)
                    return;

                if (draggedNode == null) return;

                Point p = e.GetPosition(GraphCanvas);

                //prah pro drag (aby se klik nebral jako tah)
                if (!isDragging &&
                    (Math.Abs(p.X - mouseDownPos.X) > 5 ||
                     Math.Abs(p.Y - mouseDownPos.Y) > 5))
                {
                    isDragging = true;
                }

                if (isDragging)
                {
                    draggedNode.X = p.X;
                    draggedNode.Y = p.Y;

                    Canvas.SetLeft(draggedNode.Shape, p.X - 15);
                    Canvas.SetTop(draggedNode.Shape, p.Y - 15);

                    Canvas.SetLeft(draggedNode.Label, p.X - 5);
                    Canvas.SetTop(draggedNode.Label, p.Y - 8);

                    UpdateEdges(draggedNode);
                }
            };

            //pridani hran
            ellipse.MouseLeftButtonDown += (s, e) =>
            {
                HandleNodeClick(node);
                e.Handled = true;
            };

            //mazani vrcholu
            ellipse.MouseRightButtonDown += (s, e) =>
            {
                if (isSteppingMode)
                    return;

                RemoveNode(node);
                e.Handled = true;

            };
            UpdateControls();
        }
        private void HandleNodeClick(Node node)
        {
            if (isSteppingMode)
                return;

            //prvni klik
            if (selectedNodeForEdge == null)
            {
                selectedNodeForEdge = node;
                node.Shape.Fill = Brushes.Orange;
                return;
            }

            //klik na stejny vrchol = zruseni
            if (selectedNodeForEdge == node)
            {
                node.Shape.Fill = Brushes.LightBlue;
                selectedNodeForEdge = null;
                return;
            }

            //druhy klik = vytvor hranu
            //jen pokud dana hrana jeste neexistuje
            if (!EdgeExists(selectedNodeForEdge, node))
            {
                AddEdge(selectedNodeForEdge, node);
            }

            selectedNodeForEdge.Shape.Fill = Brushes.LightBlue;
            selectedNodeForEdge = null;

        }

        private void AddEdge(Node from, Node to)
        {
            //validita hrany
            if (from == to)
                return;

            if (EdgeExists(from, to))
                return;

            Line hitbox = new Line
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = 16,
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Cursor = Cursors.Hand
            };

            Line line = new Line
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                IsHitTestVisible = false
            };

            Edge edge = new Edge
            {
                From = from,
                To = to,
                Shape = line,
                Hitbox = hitbox
            };

            edges.Add(edge);
            from.Neighbors.Add(to);
            to.Neighbors.Add(from);

            from.Neighbors.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            to.Neighbors.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

            //hrany musi byt pod vrcholy
            GraphCanvas.Children.Insert(0, hitbox);
            GraphCanvas.Children.Insert(1, line);


            //mazani hran
            hitbox.MouseRightButtonDown += (s, e) =>
            {
                if (isSteppingMode)
                    return;

                RemoveEdge(edge);
                e.Handled = true;

                UpdateControls();
            };
        }

        private void UpdateEdges(Node node)
        {
            foreach (var edge in edges)
            {
                if (edge.From == node || edge.To == node)
                {
                    edge.Shape.X1 = edge.From.X;
                    edge.Shape.Y1 = edge.From.Y;
                    edge.Shape.X2 = edge.To.X;
                    edge.Shape.Y2 = edge.To.Y;

                    edge.Hitbox.X1 = edge.From.X;
                    edge.Hitbox.Y1 = edge.From.Y;
                    edge.Hitbox.X2 = edge.To.X;
                    edge.Hitbox.Y2 = edge.To.Y;
                }
            }
        }

        //hitbox
        private Node? GetNodeAtPosition(Point p)
        {
            foreach (var node in nodes)
            {
                double dx = node.X - p.X;
                double dy = node.Y - p.Y;

                if (Math.Sqrt(dx * dx + dy * dy) <= node.Shape.Width)
                    return node;
            }

            return null;
        }

        //resi duplicitu hran
        private bool EdgeExists(Node a, Node b)
        {
            return edges.Any(e =>
                (e.From == a && e.To == b) ||
                (e.From == b && e.To == a));
        }

        //mazani hran
        private void RemoveEdge(Edge edge)
        {
            GraphCanvas.Children.Remove(edge.Shape);
            GraphCanvas.Children.Remove(edge.Hitbox);

            edge.From.Neighbors.Remove(edge.To);
            edge.To.Neighbors.Remove(edge.From);

            edges.Remove(edge);
        }

        //mazani vrcholu
        private void RemoveNode(Node node)
        {
            //pokud byl vybrany
            if (selectedNodeForEdge == node)
            {
                selectedNodeForEdge = null;
            }

            //vsechny jeho hrany
            var edgesToRemove = edges
                .Where(e => e.From == node || e.To == node)
                .ToList();

            //mazani hran
            foreach (var edge in edgesToRemove)
            {
                RemoveEdge(edge);
            }
            
            GraphCanvas.Children.Remove(node.Shape);
            GraphCanvas.Children.Remove(node.Label);

            nodes.Remove(node);
            UpdateControls();
        }

        //jmena vrcholu (pismena)
        private string GetNodeName(int index)
        {
            string name = "";
            index++;

            while (index > 0)
            {
                index--;
                name = (char)('a' + (index % 26)) + name;
                index /= 26;
            }

            return name;
        }

        private int GetNodeIndex(string name)
        {
            int result = 0;

            foreach (char c in name)
            {
                result = result * 26 + (c - 'a' + 1);
            }

            return result - 1;
        }

        private string GetNextAvailableNodeName()
        {
            HashSet<int> usedIndexes = nodes
                .Select(n => GetNodeIndex(n.Name))
                .ToHashSet();

            int index = 0;
            while (usedIndexes.Contains(index))
            {
                index++;
            }

            return GetNodeName(index);
        }

        //hledani artikulaci, bloku a mostu bez krokovani
        private void AnalyzeGraph()
        {
            ResetTarjanData();

            foreach (var node in nodes.OrderBy(n => n.Name,
                StringComparer.Ordinal))
            {
                if (!node.Visited)
                {
                    DFSAnalyze(node);

                    //zbytek hran po dokonceni komponenty
                    if (edgeStack.Count > 0)
                    {
                        AddBlockFromRemainingEdges();
                    }
                }
            }

            HighlightResults();
            ShowAnalysisResults();
        }

        private void DFSAnalyze(Node u)
        {
            u.Visited = true;
            u.Disc = u.Low = ++dfsTime;

            int children = 0;

            foreach (var v in u.Neighbors.OrderBy(n => n.Name,
                StringComparer.Ordinal))
            {
                //stromová hrana
                if (!v.Visited)
                {
                    children++;
                    v.Parent = u;

                    edgeStack.Push((u, v));
                    DFSAnalyze(v);

                    u.Low = Math.Min(u.Low, v.Low);

                    //most
                    if (v.Low > u.Disc)
                    {
                        bridges.Add((u, v));
                    }

                    //artikulace pro nekořen
                    if (u.Parent != null && v.Low >= u.Disc)
                    {
                        articulationPoints.Add(u);
                    }

                    //blok
                    if (v.Low >= u.Disc)
                    {
                        AddBlockUntilEdge(u, v);
                    }
                }
                //nestromová hrana
                else if (v != u.Parent && v.Disc < u.Disc)
                {
                    u.Low = Math.Min(u.Low, v.Disc);
                    edgeStack.Push((u, v));
                }
            }

            //artikulace pro kořen
            if (u.Parent == null && children > 1)
            {
                articulationPoints.Add(u);
            }
        }

        //přidání bloku po určitou hranu
        private void AddBlockUntilEdge(Node u, Node v)
        {
            List<(Node From, Node To)> blockEdges = new();

            while (edgeStack.Count > 0)
            {
                var edge = edgeStack.Pop();
                blockEdges.Add(edge);

                if ((edge.From == u && edge.To == v) ||
                    (edge.From == v && edge.To == u))
                {
                    break;
                }
            }

            if (blockEdges.Count > 0)
            {
                string newKey = string.Join(", ", blockEdges
                    .Select(e => FormatEdge(e.From, e.To))
                    .OrderBy(s => s, StringComparer.Ordinal));

                bool exists = blocks.Any(b =>
                    string.Join(", ", b
                        .Select(e => FormatEdge(e.From, e.To))
                        .OrderBy(s => s, StringComparer.Ordinal)) == newKey);

                if (!exists)
                {
                    blocks.Add(blockEdges);
                }
            }
        }

        //přidání zbylých hran
        private void AddBlockFromRemainingEdges()
        {
            List<(Node From, Node To)> blockEdges = new();

            while (edgeStack.Count > 0)
            {
                var edge = edgeStack.Pop();
                blockEdges.Add(edge);
            }

            if (blockEdges.Count > 0)
            {
                string newKey = string.Join(", ", blockEdges
                    .Select(e => FormatEdge(e.From, e.To))
                    .OrderBy(s => s, StringComparer.Ordinal));

                bool exists = blocks.Any(b =>
                    string.Join(", ", b
                        .Select(e => FormatEdge(e.From, e.To))
                        .OrderBy(s => s, StringComparer.Ordinal)) == newKey);

                if (!exists)
                {
                    blocks.Add(blockEdges);
                }
            }
        }

        //reset všech pomocných dat
        private void ResetTarjanData()
        {
            dfsTime = 0;
            articulationPoints.Clear();
            bridges.Clear();
            blocks.Clear();
            edgeStack.Clear();

            foreach (var node in nodes)
            {
                node.Visited = false;
                node.Parent = null;
                node.Disc = 0;
                node.Low = 0;
                node.Shape.Fill = Brushes.LightBlue;
            }

            foreach (var edge in edges)
            {
                edge.Shape.Stroke = Brushes.Black;
                edge.Shape.StrokeThickness = 2;
            }
        }

        //zvýraznění artikulací a mostů
        private void HighlightResults()
        {
            foreach (var node in articulationPoints)
            {
                node.Shape.Fill = Brushes.Red;
            }

            foreach (var bridge in bridges)
            {
                Edge? edge = edges.FirstOrDefault(e =>
                    (e.From == bridge.From && e.To == bridge.To) ||
                    (e.From == bridge.To && e.To == bridge.From));

                if (edge != null)
                {
                    edge.Shape.Stroke = Brushes.Red;
                }
            }
        }

        //výpis nalezených artikulací, mostů a bloků
        private void ShowAnalysisResults()
        {
            string articulationText = string.Join(", ", articulationPoints
                    .OrderBy(n => n.Name, StringComparer.Ordinal)
                    .Select(n => n.Name));

            string bridgeText = string.Join(", ", bridges
                    .OrderBy(b => b.From.Name, StringComparer.Ordinal)
                    .ThenBy(b => b.To.Name, StringComparer.Ordinal)
                    .Select(b => $"{b.From.Name}{b.To.Name}"));


            var lines = new List<string>();
            for (int i = 0; i < blocks.Count; i++)
            {
                lines.Add($"B{i + 1}: {string.Join(", ",
                    blocks[i].Select(n => FormatEdge(n.From, n.To))
                    .Reverse())}");
            }
            string blockText = string.Join("\n", lines);

            InfoText.Text =
                $"Artikulace:\n{articulationText}\n\n" +
                $"Mosty:\n{bridgeText}\n\n" +
                $"Bloky:\n{blockText}";
        }

        //tlacitko spustit
        private void AnalyzeGraph_Click(object sender, RoutedEventArgs e)
        {
            AnalyzeGraph();
        }

        private void ClearGraph()
        {
            if (isSteppingMode)
                return;

            GraphCanvas.Children.Clear();

            nodes.Clear();
            edges.Clear();
            articulationPoints.Clear();

            selectedNodeForEdge = null;
            draggedNode = null;

            dfsTime = 0;
            nodeCounter = 0;
            InfoText.Text = "";
            UpdateControls();
            RefreshMatrixView();
        }

        private void ClearGraph_Click(object sender, RoutedEventArgs e)
        {
            ClearGraph();
        }

        //krokovani
        private void SaveState(string description, Node? current = null, Node? neighbor = null, IEnumerable<Node>? highlightedNodes = null, IEnumerable<(Node From, Node To)>? highlightedEdges = null)
        {
            //uprava historie, kvuli krokovani zpet
            if (historyIndex < history.Count - 1)
            {
                history = history.Take(historyIndex + 1).ToList();
            }

            var state = new AlgorithmState
            {
                Disc = disc.ToDictionary(e => e.Key, e => e.Value),
                Low = low.ToDictionary(e => e.Key, e => e.Value),
                Articulations = new HashSet<Node>(articulationPoints),
                Bridges = new List<(Node From, Node To)>(bridges),
                Blocks = blocks.Select(b => b.ToList()).ToList(),
                TraversedEdgePairs = new List<(Node From, Node To)>(traversedEdgePairs),
                Stack = new Stack<DFSFrame>(dfsStack.Reverse().Select(f =>
                    new DFSFrame
                    {
                        Node = f.Node,
                        Parent = f.Parent,
                        NeighborIndex = f.NeighborIndex,
                        Children = f.Children
                    })),
                EdgeStack = new Stack<(Node From, Node To)>(edgeStack.Reverse()),
                DfsHistory = dfsHistory
                    .Select(x => new StackHistoryItem<Node>
                    {
                        Item = x.Item,
                        IsRemoved = x.IsRemoved
                    }).ToList(),
                EdgeHistory = edgeHistory
                    .Select(x => new StackHistoryItem<(Node From, Node To)>
                    {
                        Item = x.Item,
                        IsRemoved = x.IsRemoved
                    }).ToList(),
                HighlightedNodes = highlightedNodes != null
                    ? new HashSet<Node>(highlightedNodes)
                    : new HashSet<Node>(),

                HighlightedEdges = highlightedEdges != null
                    ? highlightedEdges.ToList()
                    : new List<(Node From, Node To)>(),
                CurrentNode = current,
                CurrentNeighbor = neighbor,
                Description = description
            };

            history.Add(state);
            historyIndex = history.Count - 1;

        }

        public void StartAlgorithm()
        {
            if (nodes.Count == 0)
                return;

            disc.Clear();
            low.Clear();
            articulationPoints.Clear();
            bridges.Clear();
            blocks.Clear();
            dfsStack.Clear();
            edgeStack.Clear();

            history.Clear();
            historyIndex = -1;
            dfsTime = 0;

            Node start = nodes
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .First();

            dfsStack.Push(new DFSFrame
            {
                Node = start,
                Parent = null,
                NeighborIndex = 0,
                Children = 0
            });

            dfsHistory.Add(new StackHistoryItem<Node>
            {
                Item = start,
                IsRemoved = false
            });

            isSteppingMode = true;

            SaveState("Inicializace algoritmu", start);

            UpdateControls();
        }
        public void StepForward()
        {
            //nacitani z historie, kvuli krokovani zpet
            if (historyIndex < history.Count - 1)
            {
                historyIndex++;
                LoadState(history[historyIndex]);
                UpdateControls();
                return;
            }

            ExecuteNextStep();

            if (historyIndex >= 0 && historyIndex < history.Count)
            {
                LoadState(history[historyIndex]);
            }

            UpdateControls();
        }

        public void ExecuteNextStep()
        {
            if (dfsStack.Count == 0)
                return;

            DFSFrame frame = dfsStack.Peek();
            Node? u = frame.Node;

            //prvni vrchol
            if (!disc.ContainsKey(u!))
            {
                disc[u!] = low[u!] = ++dfsTime;
                SaveState($"Navštíven vrchol {u!.Name}, por=min={dfsTime}", u, highlightedNodes: [u]);
                return;
            }

            List<Node> neighbors = u!.Neighbors
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            while (frame.NeighborIndex < neighbors.Count)
            {
                Node v = neighbors[frame.NeighborIndex++];

                //stromova hrana
                if (!disc.ContainsKey(v))
                {
                    frame.Children++;

                    edgeStack.Push((u, v));

                    edgeHistory.Add(new StackHistoryItem<(Node From, Node To)>
                    {
                        Item = (u, v),
                        IsRemoved = false
                    });

                    traversedEdgePairs.Add((u, v));
                    dfsStack.Push(new DFSFrame
                    {
                        Node = v,
                        Parent = u,
                        NeighborIndex = 0,
                        Children = 0
                    });

                    dfsHistory.Add(new StackHistoryItem<Node>
                    {
                        Item = v,
                        IsRemoved = false
                    });

                    SaveState($"Jdeme z {u.Name} do {v.Name}", u, v, highlightedNodes: [u, v], highlightedEdges: [(u, v)]);
                    return;
                }

                //nestromova hrana
                if (v != frame.Parent && disc[v] < disc[u])
                {
                    low[u] = Math.Min(low[u], disc[v]);
                    edgeStack.Push((u, v));

                    edgeHistory.Add(new StackHistoryItem<(Node From, Node To)>
                    {
                        Item = (u, v),
                        IsRemoved = false
                    });

                    traversedEdgePairs.Add((u, v));
                    SaveState($"Nestromová hrana {u.Name}{v.Name}, min[{u.Name}] = {low[u]}", u, v, highlightedNodes: [u, v], highlightedEdges: [(u, v)]);
                    return;
                }

            }

            //navrat z vrcholu, jsme v nejnizsi vrstve
            dfsStack.Pop();

            for (int i = dfsHistory.Count - 1; i >= 0; i--)
            {
                if (dfsHistory[i].Item == u && !dfsHistory[i].IsRemoved)
                {
                    dfsHistory[i].IsRemoved = true;
                    break;
                }
            }

            if (frame.Parent != null)
            {
                Node p = frame.Parent;

                if (dfsStack.Count > 0)
                {
                    DFSFrame parentFrame = dfsStack.Peek();

                    int oldLowP = low[p];
                    low[p] = Math.Min(low[p], low[u]);

                    bool isBridge = low[u] > disc[p];
                    bool closesBlock = low[u] >= disc[p];
                    bool isArticulation = parentFrame.Parent != null && low[u] >= disc[p];

                    List<(Node From, Node To)> extractedBlock = new();

                    if (isBridge)
                    {
                        bridges.Add((p, u));
                    }

                    if (closesBlock)
                    {
                        extractedBlock = ExtractBlockUntilEdge(p, u);
                        AddBlock(extractedBlock);
                    }

                    if (isArticulation)
                    {
                        articulationPoints.Add(p);
                    }

                    string description;
                    if (isBridge)
                    {
                        string blockText = string.Join(", ", extractedBlock.Select(n => FormatEdge(n.From,n.To)));

                        var highlightedEdges = new List<(Node From, Node To)> { (p, u) };
                        highlightedEdges.AddRange(extractedBlock);

                        if (isArticulation)
                        {
                            //most,blok a artikulace
                            description = $"Most: {FormatBridge(p, u)}, protože min[{u.Name}]={low[u]} > por[{p.Name}]={disc[p]}" +
                                $"\nartikulace: {p.Name} a blok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}";
                        }
                        else
                        {
                            //most a blok
                            description = $"Most: {FormatBridge(p, u)}, protože min[{u.Name}]={low[u]} > por[{p.Name}]={disc[p]}" +
                                $"\nblok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}";
                        }

                        SaveState(description, p, u, highlightedNodes: [p, u], highlightedEdges: highlightedEdges);

                        return;
                    }

                    if (closesBlock)
                    {
                        string blockText = string.Join(", ", extractedBlock.Select(n => FormatEdge(n.From, n.To)));

                        if (isArticulation)
                        {
                            //artikulace a blok
                            description = $"Artikulace: {p.Name} a blok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}"; 
                        }
                        else
                        {
                            //blok
                            description = $"Blok: {blockText} uzavřen";
                        }

                        SaveState(description, p, u, highlightedNodes: [p, u], highlightedEdges: extractedBlock);

                        return;
                    }
                    
                    if (oldLowP > low[u])
                    {
                        //nic a meni se
                        description = $"Návrat z {u.Name} do {p.Name}, min[{p.Name}] se mění na {low[p]}, protože min[{p.Name}]={oldLowP} > min[{u.Name}]={low[u]}"; 
                    }
                    else
                    {
                        //nic a nemeni se
                        description = $"Návrat z {u.Name} do {p.Name}";
                    }

                    SaveState(description, p, u, highlightedNodes: [p, u]);
                }

                return;
            }

            //je koren artikulace?
            if (frame.Children > 1)
            {
                //je
                articulationPoints.Add(u);
                SaveState($"Kořen: {u.Name} je artikulace, protože má více než jednoho přímého následníka", u, highlightedNodes: [u]);
            }
            else
            {
                //neni
                SaveState($"Algoritmus dokončen pro kořen {u.Name}", u, highlightedNodes: [u]);
            }
        }
        private void LoadState(AlgorithmState state)
        {
            disc = state.Disc.ToDictionary(e => e.Key, e => e.Value);
            low = state.Low.ToDictionary(e => e.Key, e => e.Value);
            articulationPoints = new HashSet<Node>(state.Articulations);
            bridges = new List<(Node From, Node To)>(state.Bridges);
            blocks = state.Blocks.Select(b => b.ToList()).ToList();
            traversedEdgePairs = new List<(Node From, Node To)>(state.TraversedEdgePairs);
            dfsStack = new Stack<DFSFrame>(state.Stack.Reverse());
            edgeStack = new Stack<(Node From, Node To)>(state.EdgeStack.Reverse());
            dfsHistory = state.DfsHistory
                .Select(x => new StackHistoryItem<Node>
                {
                    Item = x.Item,
                    IsRemoved = x.IsRemoved
                }).ToList();

            edgeHistory = state.EdgeHistory
                .Select(x => new StackHistoryItem<(Node From, Node To)>
                {
                    Item = x.Item,
                    IsRemoved = x.IsRemoved
                }).ToList();

            RedrawGraph();
            UpdateInfoPanel(state);

            if (showingMatrix)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StyleMatrixCells();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        public void StepBackward()
        {
            if (historyIndex <= 0) return;

            historyIndex--;
            LoadState(history[historyIndex]);

            UpdateControls();
        }
        private void UpdateInfoPanel(AlgorithmState state)
        {
            InfoText.Inlines.Clear();

            Dictionary<Node, Brush> nodeColors = new();
            Dictionary<string, Brush> edgeColors = new();

            int colorIndex = 0;

            foreach (var node in state.HighlightedNodes.OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                nodeColors[node] = GetColorByIndex(colorIndex++);
            }

            foreach (var edge in state.HighlightedEdges)
            {
                string key = FormatBridge(edge.From, edge.To);

                if (!edgeColors.ContainsKey(key))
                {
                    edgeColors[key] = GetColorByIndex(colorIndex++);
                }
            }

            InfoText.Inlines.Add(new Run($"Krok {historyIndex}\n"));
            InfoText.Inlines.Add(new Run("Popis:\n"));
            AddHighlightedDescription(state.Description!, nodeColors, edgeColors);

            InfoText.Inlines.Add(new Run("\n\n"));

            InfoText.Inlines.Add(new Run("LIFO:\n"));

            for (int i = 0; i < state.DfsHistory.Count; i++)
            {
                var item = state.DfsHistory[i];
                Node? node = item.Item;

                Run run = MakeNodeRun(node!, state, nodeColors);

                if (item.IsRemoved)
                    run.TextDecorations = TextDecorations.Strikethrough;

                InfoText.Inlines.Add(run);

                if (i < state.DfsHistory.Count - 1)
                    InfoText.Inlines.Add(new Run(", "));
            }

            InfoText.Inlines.Add(new Run("\n\nHrany:\n"));

            for (int i = 0; i < state.EdgeHistory.Count; i++)
            {
                var item = state.EdgeHistory[i];
                var edge = item.Item;

                Run run = MakeEdgeRun(edge.From, edge.To, edgeColors);

                if (item.IsRemoved)
                    run.TextDecorations = TextDecorations.Strikethrough;

                InfoText.Inlines.Add(run);

                if (i < state.EdgeHistory.Count - 1)
                    InfoText.Inlines.Add(new Run(", "));
            }

            InfoText.Inlines.Add(new Run("\n\nArtikulace:\n"));

            var articulationList = state.Articulations
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < articulationList.Count; i++)
            {
                InfoText.Inlines.Add(
                    MakeNodeNameRun(articulationList[i], nodeColors));

                if (i < articulationList.Count - 1)
                    InfoText.Inlines.Add(new Run(", "));
            }

            InfoText.Inlines.Add(new Run("\n\nMosty:\n"));

            var bridgeList = state.Bridges
                .OrderBy(b => b.From.Name, StringComparer.Ordinal)
                .ThenBy(b => b.To.Name, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < bridgeList.Count; i++)
            {
                InfoText.Inlines.Add(MakeEdgeRun(bridgeList[i].From, bridgeList[i].To, edgeColors));

                if (i < bridgeList.Count - 1)
                    InfoText.Inlines.Add(new Run(", "));
            }

            InfoText.Inlines.Add(new Run("\n\nBloky:\n"));

            for (int i = 0; i < state.Blocks.Count; i++)
            {
                InfoText.Inlines.Add(new Run($"B{i + 1}: "));

                var block = state.Blocks[i];

                for (int j = block.Count - 1; j >= 0; j--)
                {
                    var edge = block[j];

                    InfoText.Inlines.Add(MakeEdgeRun(edge.From, edge.To, edgeColors));

                    if (j > 0)
                        InfoText.Inlines.Add(new Run(", "));
                }

                if (i < state.Blocks.Count - 1)
                    InfoText.Inlines.Add(new Run("\n"));
            }
        }
        private void RedrawGraph()
        {
            //reset hran
            foreach (var edge in edges)
            {
                edge.Shape.Stroke = Brushes.Black;
                edge.Shape.StrokeThickness = 2;
            }

            //reset vrcholu
            foreach (var node in nodes)
            {
                node.Shape.Fill = Brushes.LightBlue;
                node.Shape.Opacity = 1.0;
                node.Label.TextDecorations = null;
            }

            //vrcholy v lifu
            HashSet<Node?> nodesOnStack = dfsStack
                .Select(f => f.Node)
                .ToHashSet();

            //projite vrcholy
            foreach (var node in nodes)
            {
                if (disc.ContainsKey(node))
                {
                    node.Shape.Fill = Brushes.Cyan;
                }
            }

            //artikulace
            foreach (var node in articulationPoints)
            {
                node.Shape.Fill = Brushes.Red;
            }

            // mosty
            foreach (var bridge in bridges)
            {
                Edge? edge = edges.FirstOrDefault(e =>
                    (e.From == bridge.From && e.To == bridge.To) ||
                    (e.From == bridge.To && e.To == bridge.From));

                if (edge != null)
                {
                    edge.Shape.Stroke = Brushes.Red;
                    edge.Shape.StrokeThickness = 2;
                }
            }

            //aktualni vrchol
            if (dfsStack.Count > 0)
            {
                Node? current = dfsStack.Peek().Node;
                current!.Shape.Fill = Brushes.Yellow;
                current.Shape.Opacity = 1.0;
            }

            //aktualni hrana
            if (historyIndex >= 0 && historyIndex < history.Count)
            {
                AlgorithmState state = history[historyIndex];

                if (state.CurrentNode != null && state.CurrentNeighbor != null)
                {
                    Edge? currentEdge = edges.FirstOrDefault(e =>
                        (e.From == state.CurrentNode && e.To == state.CurrentNeighbor) ||
                        (e.From == state.CurrentNeighbor && e.To == state.CurrentNode));

                    if (currentEdge != null)
                    {
                        currentEdge.Shape.Stroke = Brushes.Orange;
                        currentEdge.Shape.StrokeThickness = 4;
                    }
                }
            }
        }

        private void ResetAlgorithm()
        {
            dfsTime = 0;

            disc.Clear();
            low.Clear();
            dfsStack.Clear();
            edgeStack.Clear();
            articulationPoints.Clear();
            bridges.Clear();
            blocks.Clear();
            traversedEdgePairs.Clear();

            history.Clear();
            historyIndex = -1;

            foreach (var node in nodes)
            {
                node.Visited = false;
                node.Parent = null;
                node.Disc = 0;
                node.Low = 0;

                node.Shape.Fill = Brushes.LightBlue;
            }

            foreach (var edge in edges)
            {
                edge.Shape.Stroke = Brushes.Black;
            }

            RedrawGraph();
            InfoText.Text = "";

            isSteppingMode = false;
            UpdateControls();
            RefreshMatrixView();
        }

        //tlacitka na krokovani
        private void StartAlgorithm_Click(object sender, RoutedEventArgs e)
        {
            StartAlgorithm();
            Focus();
            if (historyIndex >= 0)
            {
                LoadState(history[historyIndex]);
            }
        }

        private void StepForward_Click(object sender, RoutedEventArgs e)
        {
            StepForward();
            LoadState(history[historyIndex]);
        }

        private void StepBackward_Click(object sender, RoutedEventArgs e)
        {
            StepBackward();
        }
        private void ResetAlgorithm_Click(object sender, RoutedEventArgs e)
        {
            ResetAlgorithm();
        }

        //enable/disable tlacitek
        private void UpdateControls()
        {
            bool hasSavedNextStep = historyIndex < history.Count - 1;
            bool algorithmStillRunning = dfsStack.Count > 0;

            StepBackButton.IsEnabled = isSteppingMode && historyIndex > 0;
            StepForwardButton.IsEnabled = isSteppingMode && (hasSavedNextStep || algorithmStillRunning);
            ResetAlgorithmButton.IsEnabled = isSteppingMode;
            StartAlgorithmButton.IsEnabled = !isSteppingMode && nodes.Count > 0;
            AnalyzeGraphButton.IsEnabled = !isSteppingMode;
            ClearGraphButton.IsEnabled = !isSteppingMode;
            GraphCanvas.Cursor = isSteppingMode ? Cursors.No : Cursors.Arrow;
            MatrixGrid.IsReadOnly = true;
            MatrixGrid.IsHitTestVisible = !isSteppingMode;
            AddNodeFromMatrixButton.IsEnabled = !isSteppingMode;
            RemoveNodeFromMatrixButton.IsEnabled = !isSteppingMode;
        }

        //formatovani mostu
        private string FormatBridge(Node a, Node b)
        {
            return StringComparer.Ordinal.Compare(a.Name, b.Name) < 0
                ? $"{a.Name}{b.Name}"
                : $"{b.Name}{a.Name}";
        }
        private string FormatEdge(Node a, Node b)
        {
            return $"{a.Name}{b.Name}";
        }

        private List<(Node From, Node To)> ExtractBlockUntilEdge(Node u, Node v)
        {
            List<(Node From, Node To)> blockEdges = new();

            while (edgeStack.Count > 0)
            {
                var edge = edgeStack.Pop();

                for (int i = edgeHistory.Count - 1; i >= 0; i--)
                {
                    var h = edgeHistory[i].Item;

                    bool same =
                        (h.From == edge.From && h.To == edge.To) ||
                        (h.From == edge.To && h.To == edge.From);

                    if (same && !edgeHistory[i].IsRemoved)
                    {
                        edgeHistory[i].IsRemoved = true;
                        break;
                    }
                }

                blockEdges.Add(edge);

                if ((edge.From == u && edge.To == v) ||
                    (edge.From == v && edge.To == u))
                {
                    break;
                }
            }

            return blockEdges;
        }

        private void AddBlock(List<(Node From, Node To)> blockEdges)
        {
            if (blockEdges.Count == 0)
                return;

            string key = string.Join(", ", blockEdges.Select(e =>FormatEdge(e.From,e.To)));

            bool exists = blocks.Any(b =>
                string.Join(", ", b
                .Select(e => FormatEdge(e.From, e.To))) == key);

            if (!exists)
            {
                blocks.Add(blockEdges);
            }
        }

        //prepinani grafu a matice
        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            showingMatrix = !showingMatrix;

            if (showingMatrix)
            {
                RefreshMatrixView();

                GraphCanvas.Visibility = Visibility.Collapsed;
                MatrixPanel.Visibility = Visibility.Visible;

                ToggleViewButton.Content = "graf";
            }
            else
            {
                GraphCanvas.Visibility = Visibility.Visible;
                MatrixPanel.Visibility = Visibility.Collapsed;

                ToggleViewButton.Content = "matice";
            }
        }

        private void RefreshMatrixView()
        {
            var orderedNodes = nodes
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            DataTable table = new DataTable();

            table.Columns.Add("Vrcholy");

            foreach (var node in orderedNodes)
            {
                table.Columns.Add(node.Name);
            }

            foreach (var rowNode in orderedNodes)
            {
                DataRow row = table.NewRow();
                row[0] = rowNode.Name;

                for (int j = 0; j < orderedNodes.Count; j++)
                {
                    Node colNode = orderedNodes[j];

                    bool connected = edges.Any(e =>
                        (e.From == rowNode && e.To == colNode) ||
                        (e.From == colNode && e.To == rowNode));

                    row[j + 1] = connected ? 1 : 0;
                }

                table.Rows.Add(row);
            }

            MatrixGrid.ItemsSource = table.DefaultView;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StyleMatrixCells();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        //edit matice
        private void MatrixGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isSteppingMode)
                return;

            DependencyObject source = (DependencyObject)e.OriginalSource;

            DataGridCell? cell = FindParent<DataGridCell>(source);
            if (cell == null)
                return;

            DataGridRow? row = FindParent<DataGridRow>(cell);
            if (row == null)
                return;

            int rowIndex = row.GetIndex();
            int colIndex = cell.Column.DisplayIndex;

            //prvni sloupec
            if (colIndex == 0)
                return;

            var orderedNodes = nodes
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            if (rowIndex >= orderedNodes.Count || colIndex - 1 >= orderedNodes.Count)
                return;

            Node rowNode = orderedNodes[rowIndex];
            Node colNode = orderedNodes[colIndex - 1];

            if (rowNode == colNode)
                return;

            Edge? edge = GetEdgeBetween(rowNode, colNode);

            if (edge != null)
                RemoveEdge(edge);
            else
                AddEdge(rowNode, colNode);

            RefreshMatrixView();

            e.Handled = true;
        }
        private Edge? GetEdgeBetween(Node a, Node b)
        {
            return edges.FirstOrDefault(e =>
                (e.From == a && e.To == b) ||
                (e.From == b && e.To == a));
        }
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
        private void AddNodeFromMatrix_Click(object sender, RoutedEventArgs e)
        {
            if (isSteppingMode)
                return;

            AddNode(0, 0);
            ArrangeNodesInCircle();
            RefreshMatrixView();
        }
        private void RemoveNodeFromMatrix_Click(object sender, RoutedEventArgs e)
        {
            if (isSteppingMode)
                return;

            Node? node = GetSelectedMatrixNode();

            if (node == null)
            {
                MessageBox.Show("Nejdříve vyberte řádek nebo buňku vrcholu v matici.");
                return;
            }

            RemoveNode(node);
            RefreshMatrixView();
        }
        private Node? GetSelectedMatrixNode()
        {
            DataRowView? rowView = null;

            if (MatrixGrid.SelectedCells.Count > 0)
            {
                rowView = MatrixGrid.SelectedCells[0].Item as DataRowView;
            }

            if (rowView == null)
            {
                rowView = MatrixGrid.SelectedItem as DataRowView;
            }

            if (rowView == null)
            {
                rowView = MatrixGrid.CurrentItem as DataRowView;
            }

            if (rowView == null)
                return null;

            string nodeName = rowView.Row[0]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(nodeName))
                return null;

            return nodes.FirstOrDefault(n => n.Name == nodeName);
        }
        private void ArrangeNodesInCircle()
        {
            if (nodes.Count == 0)
                return;

            double canvasWidth = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : 800;
            double canvasHeight = GraphCanvas.ActualHeight > 0 ? GraphCanvas.ActualHeight : 600;

            double centerX = canvasWidth / 2;
            double centerY = canvasHeight / 2;

            double radius = Math.Min(canvasWidth, canvasHeight) * 0.35;

            var orderedNodes = nodes
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                double angle = 2 * Math.PI * i / orderedNodes.Count - Math.PI / 2;

                Node node = orderedNodes[i];

                node.X = centerX + radius * Math.Cos(angle);
                node.Y = centerY + radius * Math.Sin(angle);

                Canvas.SetLeft(node.Shape, node.X - 15);
                Canvas.SetTop(node.Shape, node.Y - 15);

                Canvas.SetLeft(node.Label, node.X - 5);
                Canvas.SetTop(node.Label, node.Y - 8);

                UpdateEdges(node);
            }
        }

        //nabindovani sipek
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!isSteppingMode)
                return;

            if (e.Key == Key.Left)
            {
                StepBackward();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                StepForward();
                e.Handled = true;
            }
        }

        //sktani hran v matici

        private void StyleMatrixCells()
        {
            MatrixGrid.UpdateLayout();

            var orderedNodes = nodes
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            for (int rowIndex = 0; rowIndex < orderedNodes.Count; rowIndex++)
            {
                DataGridRow? row = MatrixGrid.ItemContainerGenerator
                    .ContainerFromIndex(rowIndex) as DataGridRow;

                if (row == null)
                    continue;

                for (int colIndex = 1; colIndex < MatrixGrid.Columns.Count; colIndex++)
                {
                    DataGridCell? cell = GetCell(row, colIndex);

                    if (cell == null)
                        continue;

                    TextBlock? textBlock = cell.Content as TextBlock;

                    if (textBlock == null)
                        continue;

                    Node rowNode = orderedNodes[rowIndex];
                    Node colNode = orderedNodes[colIndex - 1];

                    // reset
                    textBlock.TextDecorations = null;
                    textBlock.FontWeight = textBlock.Text == "1"
                        ? FontWeights.Bold
                        : FontWeights.Normal;

                    // prošlá hrana = přeškrtnout
                    if (IsTraversedEdge(rowNode, colNode))
                    {
                        textBlock.TextDecorations = TextDecorations.Strikethrough;
                    }
                }
            }
        }
        private bool IsTraversedEdge(Node a, Node b)
        {
            return traversedEdgePairs.Any(e =>
                (e.From == a && e.To == b) ||
                (e.From == b && e.To == a));
        }
        private DataGridCell? GetCell(DataGridRow row, int columnIndex)
        {
            DataGridCellsPresenter? presenter = FindVisualChild<DataGridCellsPresenter>(row);

            if (presenter == null)
            {
                row.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }

            return presenter?.ItemContainerGenerator
                .ContainerFromIndex(columnIndex) as DataGridCell;
        }
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                T? childOfChild = FindVisualChild<T>(child);

                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        //zvyrazneni vrcholu a hran v lifu
        private readonly Brush[] highlightBrushes =
        {
            Brushes.Red,
            Brushes.Blue,
            Brushes.Green,
            Brushes.Orange,
            Brushes.Purple,
            Brushes.Brown,
            Brushes.DarkCyan,
            Brushes.DeepPink
        };
        private Brush GetColorByIndex(int index)
        {
            return highlightBrushes[index % highlightBrushes.Length];
        }
        private Run MakeNodeRun(Node node, AlgorithmState state, Dictionary<Node, Brush> nodeColors)
        {
            int d = state.Disc.ContainsKey(node) ? state.Disc[node] : 0;
            int l = state.Low.ContainsKey(node) ? state.Low[node] : 0;

            Run run = new Run($"{node.Name}({d}, {l})");

            if (nodeColors.TryGetValue(node, out Brush? color))
            {
                run.Foreground = color;
                run.FontWeight = FontWeights.Bold;
            }

            return run;
        }
        private Run MakeEdgeRun(Node from, Node to, Dictionary<string, Brush> edgeColors)
        {
            string edgeKey = FormatBridge(from, to);

            Run run = new Run(FormatEdge(from, to));

            if (edgeColors.TryGetValue(edgeKey, out Brush? color))
            {
                run.Foreground = color;
                run.FontWeight = FontWeights.Bold;
            }

            return run;
        }
        private Run MakeNodeNameRun(Node node, Dictionary<Node, Brush> nodeColors)
        {
            Run run = new Run(node.Name);

            if (nodeColors.TryGetValue(node, out Brush? color))
            {
                run.Foreground = color;
                run.FontWeight = FontWeights.Bold;
            }

            return run;
        }
        private void AddHighlightedDescription(string text, Dictionary<Node, Brush> nodeColors, Dictionary<string, Brush> edgeColors)
        {
            int i = 0;

            while (i < text.Length)
            {
                // hrany
                var matchedEdge = edges
                    .SelectMany(e => new[]
                    {
                        new { Text = FormatEdge(e.From, e.To), Key = FormatBridge(e.From, e.To) },
                        new { Text = FormatEdge(e.To, e.From), Key = FormatBridge(e.From, e.To) }
                    })
                    .Where(x => edgeColors.ContainsKey(x.Key))
                    .OrderByDescending(x => x.Text.Length)
                    .FirstOrDefault(x =>
                        i + x.Text.Length <= text.Length &&
                        text.Substring(i, x.Text.Length) == x.Text);

                if (matchedEdge != null)
                {
                    Run run = new Run(matchedEdge.Text)
                    {
                        Foreground = edgeColors[matchedEdge.Key],
                        FontWeight = FontWeights.Bold
                    };

                    InfoText.Inlines.Add(run);
                    i += matchedEdge.Text.Length;
                    continue;
                }

                // vrcholy
                var matchedNode = nodeColors.Keys
                    .OrderByDescending(n => n.Name.Length)
                    .FirstOrDefault(n =>
                        i + n.Name.Length <= text.Length &&
                        text.Substring(i, n.Name.Length) == n.Name &&
                        IsWholeWord(text, i, n.Name.Length));

                if (matchedNode != null)
                {
                    Run run = new Run(matchedNode.Name)
                    {
                        Foreground = nodeColors[matchedNode],
                        FontWeight = FontWeights.Bold
                    };

                    InfoText.Inlines.Add(run);
                    i += matchedNode.Name.Length;
                    continue;
                }

                InfoText.Inlines.Add(new Run(text[i].ToString()));
                i++;
            }
        }
        private bool IsWholeWord(string text, int start, int length)
        {
            bool beforeOk = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
            bool afterOk = start + length >= text.Length || !char.IsLetterOrDigit(text[start + length]);

            return beforeOk && afterOk;
        }
    }
}