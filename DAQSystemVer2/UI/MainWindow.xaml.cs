using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Binding = System.Windows.Data.Binding;
using Point = System.Windows.Point;

namespace DAQSystem.Application.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += OnWindowLoaded;
        }

        private void OnAnimationCompleted(object sender, EventArgs e)
        {
            var dataContext = DataContext as MainWindowViewModel;

            dataContext.IsSettingFadePlaying = false;
            dataContext.IsPlotFadePlaying = false;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            int starNum = 100;

            var range = new Point(TopMenuCanvas.ActualWidth, TopMenuCanvas.ActualHeight);
            Ellipse lastEllipse = default(Ellipse);
            for (int i = 0; i < starNum; i++)
            {
                Point point = new Point(random.Next((int)range.X), random.Next((int)range.Y));
                Ellipse ellipse = new Ellipse();

                Canvas.SetLeft(ellipse, point.X);
                Canvas.SetTop(ellipse, point.Y);
                this.TopMenuCanvas.Children.Add(ellipse);
                ellipsePoints[ellipse] = new Point(random.Next(-15, 15), random.Next(-15, 15));
                while (ellipsePoints[ellipse] == default)
                {
                    ellipsePoints[ellipse] = new Point(random.Next(-15, 15), random.Next(-15, 15));
                }

                lastEllipse = ellipse;

                // 动画计算
                int countTime = 1;
                Point offset = ellipsePoints[ellipse];
                while (point.X + offset.X > 0 && point.X + offset.X < range.X
                    && point.Y + offset.Y > 0 && point.Y + offset.Y < range.Y)
                {
                    point = new Point(point.X + offset.X, point.Y + offset.Y);
                    countTime++;
                }

                point = new Point(point.X + offset.X, point.Y + offset.Y);
                Storyboard storyboard = new Storyboard();
                storyboard.AutoReverse = true;
                storyboard.RepeatBehavior = RepeatBehavior.Forever;
                DoubleAnimation doubleAnimationX = new DoubleAnimation() { To = point.X, Duration = new Duration(TimeSpan.FromMilliseconds(5000 * countTime)) };
                DoubleAnimation doubleAnimationY = new DoubleAnimation() { To = point.Y, Duration = new Duration(TimeSpan.FromMilliseconds(5000 * countTime)) };
                DoubleAnimation doubleOpacity = new DoubleAnimation() { To = random.NextDouble(), Duration = new Duration(TimeSpan.FromMilliseconds(5000 * countTime)) };

                storyboard.Children.Add(doubleAnimationX);
                storyboard.Children.Add(doubleAnimationY);
                storyboard.Children.Add(doubleOpacity);
                Storyboard.SetTarget(doubleAnimationX, ellipse);
                Storyboard.SetTarget(doubleAnimationY, ellipse);
                Storyboard.SetTarget(doubleOpacity, ellipse);

                Storyboard.SetTargetProperty(doubleAnimationX, new PropertyPath("(Canvas.Left)"));
                Storyboard.SetTargetProperty(doubleAnimationY, new PropertyPath("(Canvas.Top)"));
                Storyboard.SetTargetProperty(doubleOpacity, new PropertyPath("(Ellipse.Opacity)"));
                storyboard.Begin(ellipse);

            }

            DispatcherTimer dispatcherTimerLine = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, OnTimeTickLineOpacity, Dispatcher);
            dispatcherTimerLine.Start();
        }

        private void OnTimeTickLineOpacity(object sender, EventArgs e)
        {
            int index = 0;
            foreach (var item in ellipsePoints)
            {
                if (index > 0)
                {
                    var ellipses = ellipsePoints.Keys.ToList();
                    var firstPoint = new Point(Canvas.GetLeft(ellipses[index - 1]), Canvas.GetTop(ellipses[index - 1]));
                    var secPoint = new Point(Canvas.GetLeft(ellipses[index]), Canvas.GetTop(ellipses[index]));

                    double dist = Math.Sqrt(Math.Pow(firstPoint.X - secPoint.X, 2) + Math.Pow(firstPoint.Y - secPoint.Y, 2));
                }
                index++;
            }
        }

        private Dictionary<Ellipse, Point> ellipsePoints = new Dictionary<Ellipse, Point>();
    }
}