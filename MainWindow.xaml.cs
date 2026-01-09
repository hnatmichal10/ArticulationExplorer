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

        //pridani vrcholu
        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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
                RemoveNode(node);
                e.Handled = true;
            };

        }
        private void HandleNodeClick(Node node)
        {

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
                RemoveEdge(edge);
                e.Handled = true;
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
    }
}