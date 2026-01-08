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

        //reseni hitboxu vrcholu
        private const double NodeRadius = 15;
        private const double HitboxRadius = 30;

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
            int id = nodeCounter++;

            //vizualni vrchol
            Ellipse ellipse = new Ellipse
            {
                Width = 30,
                Height = 30,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };

            //text
            TextBlock label = new TextBlock
            {
                Text = id.ToString(),
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false // aby nepřekážel myši
            };

            //pozice
            Canvas.SetLeft(ellipse, x - 15);
            Canvas.SetTop(ellipse, y - 15);

            Canvas.SetLeft(label, x - 5);
            Canvas.SetTop(label, y - 8);

            //objekt
            Node node = new Node
            {
                Id = id,
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
            AddEdge(selectedNodeForEdge, node);

            selectedNodeForEdge.Shape.Fill = Brushes.LightBlue;
            selectedNodeForEdge = null;
        }
        private void AddEdge(Node from, Node to)
        {
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
                Shape = line
            };

            edges.Add(edge);

            //hrany musi byt pod vrcholy
            GraphCanvas.Children.Insert(0, line);
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

                if (Math.Sqrt(dx * dx + dy * dy) <= HitboxRadius)
                    return node;
            }

            return null;
        }
    }
}