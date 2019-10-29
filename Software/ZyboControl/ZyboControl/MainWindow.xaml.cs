using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZyboLogic;

namespace ZyboControl
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Color Color1 = new Color();
        Color Color2 = new Color();

        public MainWindowModelView MainWindowModelView;

        ConsoleWriter ConsoleWriter;
        Rectangle[] ledRectangles;

        public MainWindow()
        {
            MainWindowModelView = new MainWindowModelView();
            DataContext = MainWindowModelView;
            InitializeComponent();

            ConsoleWriter = new ConsoleWriter(ConsoleOutText);
            Console.SetOut(ConsoleWriter);

            MainWindowModelView.Zybo.SerialPortsChanged += (o, e) =>
            {
                comboBox.Items.Clear();
                e.ForEach(delegate (String item) { comboBox.Items.Add(item); });
            };

            comboBox.SelectionChanged += (o, e) =>
            {
                MainWindowModelView.Zybo.SetupPort(comboBox.SelectedItem as String);
            };

            comboBox.PreviewMouseLeftButtonDown += (o, e) =>
            {
                MainWindowModelView.Zybo.RefreshSerialPorts();
            };

            MainWindowModelView.Zybo.Connected += (o, e) =>
            {
                MainWindowModelView.Zybo.SendMultipleLeds(MainWindowModelView.Zybo.FixColorStrip);
            };

            MainWindowModelView.Zybo.Disconnected += (o, e) =>
            {

            };

            MainWindowModelView.Zybo.Fft.RecordingStarted += (o, e) =>
            {
                GradientColor1.IsEnabled = false;
                GradientColor2.IsEnabled = false;
            };

            MainWindowModelView.Zybo.Fft.RecordingStopped += (o, e) =>
            {
                GradientColor1.IsEnabled = true;
                GradientColor2.IsEnabled = true;
                GradientColor1.SelectedColor = Color.FromRgb(255, 0, 0);
                GradientColor2.SelectedColor = Color.FromRgb(0, 0, 255);
            };

            startMusicLeds.Checked += (o, e) => MainWindowModelView.Zybo.Fft.Start();
            startMusicLeds.Unchecked += (o, e) => MainWindowModelView.Zybo.Fft.Stop();

            GradientColor1.SelectedColorChanged += (o, e) =>
            {
                Color1 = e.NewValue.Value;
                MainWindowModelView.Zybo.SetColorGradient(Color1.R, Color1.G, Color1.B, Color2.R, Color2.G, Color2.B);
            };

            GradientColor2.SelectedColorChanged += (o, e) =>
            {
                Color2 = e.NewValue.Value;
                MainWindowModelView.Zybo.SetColorGradient(Color1.R, Color1.G, Color1.B, Color2.R, Color2.G, Color2.B);
            };

            programFpgaButton.Click += (o, e) =>
            {
                _ = MainWindowModelView.Zybo.ProgramDevice();
            };

            programCore0Button.Click += (o, e) =>
            {
                _ = MainWindowModelView.Zybo.ProgramCore0();
            };

            ledRectangles = new Rectangle[MainWindowModelView.Zybo.FixColorStrip.NumberOfLeds];
            for(int i = 0; i < MainWindowModelView.Zybo.FixColorStrip.NumberOfLeds; i++)
            {
                int size = 5;
                
                ledGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ledRectangles[i] = new Rectangle() { Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255) ), Width = size, Height = size, StrokeThickness = 1 };
                ledGrid.Children.Add(ledRectangles[i]);
                Grid.SetColumn(ledRectangles[i], i);
            }

            this.SizeChanged += (o, e) =>
            {
                if (double.IsNaN(ledGrid.RenderSize.Width)) return;
                double size = ledGrid.RenderSize.Width / MainWindowModelView.Zybo.FixColorStrip.NumberOfLeds;
                foreach (var rect in ledRectangles)
                {
                    rect.Width = size;
                    rect.Height = size;
                }
            };

            MainWindowModelView.Zybo.LedColorChanged += (o, e) =>
            {
                for(int i = 0; i < MainWindowModelView.Zybo.FixColorStrip.NumberOfLeds; i++)
                {
                    var led = e[i];
                    ledRectangles[i].Fill = new SolidColorBrush(Color.FromRgb(led.Red, led.Green, led.Blue));
                }
            };

            ConsoleOutText.TextChanged += (o, e) =>
            {
                ConsoleOutText.CaretIndex = ConsoleOutText.Text.Length;
                ConsoleOutText.ScrollToEnd();
            };

            GradientColor1.SelectedColor = Color.FromRgb(255, 0, 0);
            GradientColor2.SelectedColor = Color.FromRgb(0, 0, 255);

        }

        private void ClrPcker_Background_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            MainWindowModelView.Zybo.SetFixColor(e.NewValue.Value.R, e.NewValue.Value.G, e.NewValue.Value.B);
        }
    }
}
