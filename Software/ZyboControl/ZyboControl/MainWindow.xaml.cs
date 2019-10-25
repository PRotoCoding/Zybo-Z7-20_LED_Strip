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
        Zybo zybo;

        public MainWindow()
        {
            InitializeComponent();
            zybo = new Zybo();
            zybo.SerialPortsChanged += (o, e) =>
            {
                comboBox.Items.Clear();
                e.ForEach(delegate (String item) { comboBox.Items.Add(item); });
            };

            comboBox.SelectionChanged += (o, e) =>
            {
                zybo.SetupPort(comboBox.SelectedItem as String);
            };

            comboBox.PreviewMouseLeftButtonDown += (o, e) =>
            {
                zybo.RefreshSerialPorts();
            };

            zybo.Connected += (o, e) =>
            {
                sendButton.IsEnabled = true;
            };

            zybo.Disconnected += (o, e) =>
            {
                sendButton.IsEnabled = false;
            };

            sendButton.Click += (o, e) => zybo.SendData();
        }
    }
}
