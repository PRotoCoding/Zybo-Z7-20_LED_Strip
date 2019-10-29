using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ZyboControl
{
    class ConsoleWriter : TextWriter
    {
        private TextBox textBox;

        public ConsoleWriter(TextBox textBox)
        {
            this.textBox = textBox;
        }

        public override void Write(char value)
        {
            Application.Current.Dispatcher.Invoke(() => textBox.Text += value);
        }

        public override void Write(string value)
        {
            Application.Current.Dispatcher.Invoke(() => textBox.Text += value);
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
