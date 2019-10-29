using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZyboLogic;
using System.ComponentModel;
using OxyPlot.Axes;
using NAudio.Wave;
using System.Diagnostics;

namespace ZyboControl
{
    public class MainWindowModelView : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets the plot model.
        /// </summary>
        public PlotModel Model { get; private set; }

        public int maxAudioPoints = 200;
        public PlotModel AudioSignalModel { get; private set; }
        private readonly Stopwatch watch = new Stopwatch();

        public event PropertyChangedEventHandler PropertyChanged;

        public PlotModel FrequencyModel { get; private set; }
        

        /// <summary>
        /// Zybo logic object
        /// </summary>
        public Zybo Zybo;

        public MainWindowModelView()
        {

            Zybo = new Zybo();
            this.watch.Start();
            Zybo.Fft.NewFftDataAvailable += this.OnFftDataAvailable;
            Zybo.Fft.WaveIn.DataAvailable += OnAudioDataAvailable;

            var tmp = new PlotModel { Title = "Live Audio Data" };
            var series = new LineSeries { MarkerType = MarkerType.None };
            //for (int i = 0; i < 1024; i++) series.Points.Add(new DataPoint(i, 0));
            tmp.Series.Add(series);
            //tmp.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 5000 });
            AudioSignalModel = tmp;

            tmp = new PlotModel { Title = "Live FFT Data" };
            var series2 = new LineSeries();
            for (int i = 0; i < 1024; i++) series2.Points.Add(new DataPoint(i, 0));
            tmp.Series.Add(series2);
            tmp.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 0.1 });
            this.FrequencyModel = tmp;
        }

        public void OnFftDataAvailable(object sender, double[] e)
        {
            for(int i = 0; i < e.Length; i++)
            {
                (FrequencyModel.Series.First() as LineSeries).Points[i] = new DataPoint(i, e[i]);
                //(FrequencyModel.Series.First() as LineSeries).Points.Add(new DataPoint(i, e[i]));
            }
            FrequencyModel.InvalidatePlot(true);
        }

        public void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded < 2000) return;
            int bytesPerSample = Zybo.Fft.WaveIn.WaveFormat.BitsPerSample / 8;
            int samplesRecorded = e.BytesRecorded / bytesPerSample;
            Int16[] lastBuffer = new Int16[samplesRecorded];
            for (int i = 0; i < samplesRecorded; i++)
            {
                int index = i * bytesPerSample;
                //if(e.Buffer[index] != 0xFF )
                lastBuffer[i] = BitConverter.ToInt16(e.Buffer, index);
            }
            int lastBufferAmplitude = lastBuffer.Max() - lastBuffer.Min();
            lastBufferAmplitude = lastBufferAmplitude > 10000 ? 0 : lastBufferAmplitude;
            int max = lastBuffer.Max();
            int min = lastBuffer.Min();
            double t = this.watch.ElapsedMilliseconds * 0.001;
            if ((AudioSignalModel.Series.First() as LineSeries).Points.Count > maxAudioPoints)
            {
                (AudioSignalModel.Series.First() as LineSeries).Points.RemoveAt(0);
            }
            (AudioSignalModel.Series.First() as LineSeries).Points.Add(new DataPoint(t, lastBufferAmplitude));
            AudioSignalModel.InvalidatePlot(true);
        }

    }
}
