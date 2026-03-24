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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            UpdateControls();

            GraphCanvas.MouseLeftButtonDown += GraphCanvas_MouseLeftButtonDown;
        }
        //pro text na vrcholech
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
            string nodeName = GetNodeName(nodeCounter++);

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

            //klik na stejny uzel = zruseni
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

        //pismena misto cisel na vrcholech
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

        //hledani artikulaci
        private void FindArticulations()
        {
            //reset
            dfsTime = 0;
            articulationPoints.Clear();
            foreach (var node in nodes)
            {
                node.Visited = false;
                node.Parent = null;
                node.Disc = 0;
                node.Low = 0;
                node.Shape.Fill = Brushes.LightBlue;
            }

            //prochazeni vrcholu
            foreach (var node in nodes)
            {
                if (!node.Visited)
                {
                    DFS(node);
                }
            }

            //artikulace jsou cervene
            foreach (var ap in articulationPoints)
            {
                ap.Shape.Fill = Brushes.Red;
            }
        }

        //tarjan
        private void DFS(Node u)
        {
            u.Visited = true;
            u.Disc = u.Low = ++dfsTime;

            int children = 0;

            foreach (var v in u.Neighbors)
            {
                //stromova hrana
                if (!v.Visited)
                {
                    children++;
                    v.Parent = u;

                    DFS(v);

                    u.Low = Math.Min(u.Low, v.Low);

                    if (u.Parent != null && v.Low >= u.Disc)
                    {
                        articulationPoints.Add(u);
                    }
                }
                else if (v != u.Parent)
                {
                    u.Low = Math.Min(u.Low, v.Disc);
                }
            }

            //korenovy vrchol
            if (u.Parent == null && children > 1)
            {
                articulationPoints.Add(u);
            }
        }

        //tlacitko spustit
        private void FindArticulations_Click(object sender, RoutedEventArgs e)
        {
            FindArticulations();
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
                Stack = new Stack<DFSFrame>(dfsStack.Reverse().Select(f =>
                    new DFSFrame
                    {
                        Node = f.Node,
                        Parent = f.Parent,
                        NeighborIndex = f.NeighborIndex,
                        Children = f.Children
                    })),
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
            dfsStack.Clear();
            history.Clear();
            dfsTime = 0;

            Node start = nodes.First();

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

            if (!disc.ContainsKey(u))
            {
                disc[u] = low[u] = ++dfsTime;
                SaveState($"Navštíven uzel {u.Name}, disc=low={dfsTime}", u);
                return;
            }

            List<Node> neighbors = u.Neighbors.ToList();

            while (frame.NeighborIndex < neighbors.Count)
            {
                Node v = neighbors[frame.NeighborIndex++];

                if (!disc.ContainsKey(v))
                {
                    frame.Children++;

                    dfsStack.Push(new DFSFrame
                    {
                        Node = v,
                        Parent = u,
                        NeighborIndex = 0,
                        Children = 0
                    });

                    SaveState($"Jdu z {u.Name} do {v.Name}", u, v);
                    return;
                }

                if (v != frame.Parent)
                {
                    low[u] = Math.Min(low[u], disc[v]);
                    SaveState($"Back-edge {u.Name} -> {v.Name}, low[{u.Name}] = {low[u]}", u, v);
                    return;
                }

            }

            dfsStack.Pop();

            if (frame.Parent != null)
            {
                Node p = frame.Parent;

                if (dfsStack.Count > 0)
                {
                    low[p] = Math.Min(low[p], low[u]);

                    DFSFrame parentFrame = dfsStack.Peek();

                    if (parentFrame.Parent != null && low[u] >= disc[p])
                    {
                        articulationPoints.Add(p);
                        SaveState($"Artikulace nalezena: {p.Name}", p);
                    }
                    else
                    {
                        SaveState($"Návrat z {u.Name} do {p.Name}", p);
                    }
                }

                return;
            }

            if (frame.Children > 1)
            {
                articulationPoints.Add(u);
                SaveState($"Kořen je artikulace: {u.Name}", u);
            }
            else
            {
                SaveState($"DFS dokončeno pro kořen {u.Name}", u);
            }
        }
        private void LoadState(AlgorithmState state)
        {
            disc = state.Disc.ToDictionary(e => e.Key, e => e.Value);
            low = state.Low.ToDictionary(e => e.Key, e => e.Value);
            articulationPoints = new HashSet<Node>(state.Articulations);
            dfsStack = new Stack<DFSFrame>(state.Stack.Reverse());

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
            InfoText.Text =
                $"Krok: {historyIndex}\n" +
                $"Popis: {state.Description}\n\n" +
                string.Join("\n", state.Disc.Select(n =>
                    $"{n.Key.Name}: disc={n.Value}, low={state.Low[n.Key]}"));
        }
        private void RedrawGraph()
        {
            // 1)hrany
            foreach (var edge in edges)
            {
                edge.Shape.Stroke = Brushes.Black;
            }

            // 2)vrcholy
            foreach (var node in nodes)
            {
                node.Shape.Fill = Brushes.LightBlue;
            }

            // 3)artikulace
            foreach (var node in articulationPoints)
            {
                node.Shape.Fill = Brushes.Red;
            }

            // 4)aktualni frame
            if (dfsStack.Count > 0)
            {
                var current = dfsStack.Peek().Node;
                current.Shape.Fill = Brushes.Yellow;
            }
        }

        private void ResetAlgorithm()
        {
            dfsTime = 0;

            disc.Clear();
            low.Clear();
            dfsStack.Clear();
            articulationPoints.Clear();

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
            FindArticulationsButton.IsEnabled = !isSteppingMode;
            ClearGraphButton.IsEnabled = !isSteppingMode;

            GraphCanvas.Cursor = isSteppingMode ? Cursors.No : Cursors.Arrow;
        }
    }
}