using ArticulationExplorer.Model;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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

                UpdateControls();
            };

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

            foreach (var node in nodes.OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                if (!node.Visited)
                {
                    DFSAnalyze(node);

                    // kdyby po dokončení komponenty něco zůstalo na stacku hran
                    if (edgeStack.Count > 0)
                    {
                        AddBlockFromRemainingEdges();
                    }
                }
            }

            HighlightResults();
            ShowAnalysisResults();
        }

        //tarjan
        private void DFSAnalyze(Node u)
        {
            u.Visited = true;
            u.Disc = u.Low = ++dfsTime;

            int children = 0;

            foreach (var v in u.Neighbors.OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                // stromová hrana
                if (!v.Visited)
                {
                    children++;
                    v.Parent = u;

                    edgeStack.Push((u, v));

                    DFSAnalyze(v);

                    u.Low = Math.Min(u.Low, v.Low);

                    // MOST
                    if (v.Low > u.Disc)
                    {
                        bridges.Add((u, v));
                    }

                    // ARTIKULACE pro nekořen
                    if (u.Parent != null && v.Low >= u.Disc)
                    {
                        articulationPoints.Add(u);
                    }

                    // BLOK
                    if (v.Low >= u.Disc)
                    {
                        AddBlockUntilEdge(u, v);
                    }
                }
                // zpětná hrana k předkovi
                else if (v != u.Parent && v.Disc < u.Disc)
                {
                    u.Low = Math.Min(u.Low, v.Disc);
                    edgeStack.Push((u, v));
                }
            }

            // ARTIKULACE pro kořen
            if (u.Parent == null && children > 1)
            {
                articulationPoints.Add(u);
            }
        }

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
                string newKey = string.Join("|", blockEdges
                    .Select(e => FormatEdge(e.From, e.To))
                    .OrderBy(s => s, StringComparer.Ordinal));

                bool exists = blocks.Any(b =>
                    string.Join("|", b
                        .Select(e => FormatEdge(e.From, e.To))
                        .OrderBy(s => s, StringComparer.Ordinal)) == newKey);

                if (!exists)
                {
                    blocks.Add(blockEdges);
                }
            }
        }
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
        private void ShowAnalysisResults()
        {
            string articulationText = string.Join(", ", articulationPoints
                    .OrderBy(n => n.Name, StringComparer.Ordinal)
                    .Select(n => n.Name));

            string bridgeText = string.Join("\n", bridges
                    .OrderBy(b => b.From.Name, StringComparer.Ordinal)
                    .ThenBy(b => b.To.Name, StringComparer.Ordinal)
                    .Select(b => $"{b.From.Name}{b.To.Name}"));


            var lines = new List<string>();
            for (int i = 0; i < blocks.Count; i++)
            {
                lines.Add($"B{i + 1}: {string.Join(", ", blocks[i].Select(n => FormatEdge(n.From, n.To)))}");
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
        }

        private void ClearGraph_Click(object sender, RoutedEventArgs e)
        {
            ClearGraph();
        }

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

        //krokovani
        private void SaveState(string description, Node? current = null, Node? neighbor = null)
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
                Stack = new Stack<DFSFrame>(dfsStack.Reverse().Select(f =>
                    new DFSFrame
                    {
                        Node = f.Node,
                        Parent = f.Parent,
                        NeighborIndex = f.NeighborIndex,
                        Children = f.Children
                    })),
                EdgeStack = new Stack<(Node From, Node To)>(edgeStack.Reverse()),
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
            Node u = frame.Node;

            //prvni vrchol
            if (!disc.ContainsKey(u))
            {
                disc[u] = low[u] = ++dfsTime;
                SaveState($"Navštíven vrchol {u.Name}, por=min={dfsTime}", u);
                return;
            }

            List<Node> neighbors = u.Neighbors
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
                    dfsStack.Push(new DFSFrame
                    {
                        Node = v,
                        Parent = u,
                        NeighborIndex = 0,
                        Children = 0
                    });
                    SaveState($"Jdeme z {u.Name} do {v.Name}", u, v);
                    return;
                }

                //nestromova hrana
                if (v != frame.Parent && disc[v] < disc[u])
                {
                    low[u] = Math.Min(low[u], disc[v]);
                    edgeStack.Push((u, v));
                    SaveState($"Nestromová hrana {u.Name}{v.Name}, min[{u.Name}] = {low[u]}", u, v);
                    return;
                }

            }

            //navrat z vrcholu, jsme v nejnizsi vrstve
            dfsStack.Pop();

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

                    if (isBridge)
                    {
                        string blockText = string.Join(", ", extractedBlock.Select(n => FormatEdge(n.From,n.To)));

                        if (isArticulation)
                        {
                            SaveState($"Most: {FormatBridge(p, u)}, protože min[{u.Name}]={low[u]} > por[{p.Name}]={disc[p]}" +
                                $"\nartikulace: {p.Name} a blok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}",
                                p, u);
                        }
                        else
                        {
                            SaveState($"Most: {FormatBridge(p, u)}, protože min[{u.Name}]={low[u]} > por[{p.Name}]={disc[p]}" +
                                $"\nblok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}",
                                p, u);
                        }

                        return;
                    }

                    if (closesBlock)
                    {
                        string blockText = string.Join(", ", extractedBlock.Select(n => FormatEdge(n.From, n.To)));

                        if (isArticulation)
                        {
                            SaveState($"Artikulace: {p.Name} a blok: {blockText}, protože min[{u.Name}]={low[u]} >= por[{p.Name}]={disc[p]}", p, u);
                        }
                        else
                        {
                            SaveState($"Blok: {blockText} uzavřen", p, u);
                        }

                        return;
                    }

                    if (oldLowP > low[u])
                    {
                        SaveState($"Návrat z {u.Name} do {p.Name}, min[{p.Name}] se mění na {low[p]}, protože min[{p.Name}]={oldLowP} > min[{u.Name}]={low[u]}", p, u);
                    }
                    else
                    {
                        SaveState($"Návrat z {u.Name} do {p.Name}", p, u);
                    }
                }

                return;
            }

            //je koren artikulace?
            if (frame.Children > 1)
            {
                articulationPoints.Add(u);
                SaveState($"Kořen: {u.Name} je artikulace, protože má více než jednoho přímého následníka", u);
            }
            else
            {
                SaveState($"Algoritmus dokončen pro kořen {u.Name}", u);
            }
        }
        private void LoadState(AlgorithmState state)
        {
            disc = state.Disc.ToDictionary(e => e.Key, e => e.Value);
            low = state.Low.ToDictionary(e => e.Key, e => e.Value);
            articulationPoints = new HashSet<Node>(state.Articulations);
            bridges = new List<(Node From, Node To)>(state.Bridges);
            blocks = state.Blocks.Select(b => b.ToList()).ToList();
            dfsStack = new Stack<DFSFrame>(state.Stack.Reverse());
            edgeStack = new Stack<(Node From, Node To)>(state.EdgeStack.Reverse());

            RedrawGraph();
            UpdateInfoPanel(state);
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
            string stackText = "";
            if (state.Stack.Count != 0)
            stackText = string.Join("\n", state.Stack
                .Reverse()
                .Select(f => f.Node)
                .Distinct()
                .Select(n =>
                {
                    int d = state.Disc.ContainsKey(n) ? state.Disc[n] : 0;
                    int l = state.Low.ContainsKey(n) ? state.Low[n] : 0;
                    return $"{n.Name}({d}, {l})";
                }));

            string articulationsText = string.Join(", ", state.Articulations
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .Select(n => n.Name));

            string bridgesText = string.Join(", ", state.Bridges
                .OrderBy(b => b.From.Name, StringComparer.Ordinal)
                .ThenBy(b => b.To.Name, StringComparer.Ordinal)
                .Select(b => FormatBridge(b.From, b.To)));

            var lines = new List<string>();
            for (int i = 0; i < state.Blocks.Count; i++)
            {
                lines.Add($"B{i + 1}: {string.Join(", ", state.Blocks[i].Select(n => FormatEdge(n.From, n.To)))}");
            }
            string blocksText = string.Join("\n", lines);

            string edgeStackText = string.Join(", ", state.EdgeStack
                .Reverse()
                .Select(e => FormatEdge(e.From,e.To)));


            InfoText.Text =
                $"Krok {historyIndex}\n" +
                $"Popis:\n{state.Description}\n\n" +
                $"\n\nLIFO:\n{stackText}" +
                $"\n\nHrany:\n{edgeStackText}" +
                $"\n\nArtikulace:\n{articulationsText}" +
                $"\n\nMosty:\n{bridgesText}" +
                $"\n\nBloky:\n{blocksText}";
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
            HashSet<Node> nodesOnStack = dfsStack
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
                Node current = dfsStack.Peek().Node;
                current.Shape.Fill = Brushes.Yellow;
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
        }

        //tlacitka na krokovani
        private void StartAlgorithm_Click(object sender, RoutedEventArgs e)
        {
            StartAlgorithm();
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
            StepBackButton.IsEnabled = isSteppingMode && historyIndex > 0;
            StepForwardButton.IsEnabled = isSteppingMode;
            ResetAlgorithmButton.IsEnabled = isSteppingMode;
            StartAlgorithmButton.IsEnabled = !isSteppingMode && nodes.Count > 0;
            AnalyzeGraphButton.IsEnabled = !isSteppingMode;
            ClearGraphButton.IsEnabled = !isSteppingMode;

            GraphCanvas.Cursor = isSteppingMode ? Cursors.No : Cursors.Arrow;
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
    }
}