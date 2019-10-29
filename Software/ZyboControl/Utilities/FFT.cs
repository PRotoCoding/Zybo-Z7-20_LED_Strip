using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Windows.Forms;
using System.Timers;

namespace Utilities
{
    public class FFT
    {
        // MICROPHONE ANALYSIS SETTINGS
        private int RATE = 44100; // sample rate of the sound card
        public int BUFFERSIZE = (int)Math.Pow(2, 11); // must be a multiple of 2
        System.Windows.Forms.Timer timer;

        public event EventHandler<double[]> NewFftDataAvailable;

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;

        // prepare class objects
        public BufferedWaveProvider bwp;
        public WaveIn WaveIn;

        public FFT()
        {
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 10;
            timer.Tick += Timer_Tick;
            StartListeningToMicrophone();
        }

        public void StartListeningToMicrophone(int audioDeviceNumber = 0)
        {
            WaveIn = new WaveIn
            {
                DeviceNumber = audioDeviceNumber,
                WaveFormat = new NAudio.Wave.WaveFormat(RATE, 1),
                BufferMilliseconds = (int)((double)BUFFERSIZE / (double)RATE * 1000.0)
            };
            WaveIn.DataAvailable += new EventHandler<WaveInEventArgs>(AudioDataAvailable);
            bwp = new BufferedWaveProvider(WaveIn.WaveFormat)
            {
                BufferLength = BUFFERSIZE * 2,
                DiscardOnBufferOverflow = true
            };
        }

        void AudioDataAvailable(object sender, WaveInEventArgs e)
        {
            bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Enabled = false;
            DoStuff();
            timer.Enabled = true;
        }

        void DoStuff()
        {
            // check the incoming microphone audio
            int frameSize = BUFFERSIZE;
            var audioBytes = new byte[frameSize];
            bwp.Read(audioBytes, 0, frameSize);

            // return if there's nothing new to plot
            if (audioBytes.Length == 0)
                return;
            if (audioBytes[frameSize - 2] == 0)
                return;
            
            // create double arrays to hold the data we will graph
            double[] pcm = new double[frameSize / 2];
            double[] fft = new double[frameSize / 2];

            // populate Xs and Ys with double data
            for (int i = 0; i < BUFFERSIZE / 2; i++)
            {
                // read the int16 from the two bytes
                Int16 val = BitConverter.ToInt16(audioBytes, i * 2);

                // store the value in Ys as a percent (+/- 100% = 200%)
                pcm[i] = (double)(val) / Math.Pow(2, 16) * 200.0;
            }

            // calculate the full FFT
            fft = CalculateFFT(pcm);

            NewFftDataAvailable.Invoke(this, fft);
        }

        public double[] CalculateFFT(double[] data)
        {
            double[] fft = new double[data.Length];
            System.Numerics.Complex[] fftComplex = new System.Numerics.Complex[data.Length];
            for (int i = 0; i < data.Length; i++)
                fftComplex[i] = new System.Numerics.Complex(data[i], 0.0);
            Accord.Math.FourierTransform.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);
            for (int i = 0; i < data.Length; i++)
                fft[i] = fftComplex[i].Magnitude;
            return fft;
        }

        public void Stop() {
            Console.WriteLine("Audio recording stopped");
            timer.Stop();
            WaveIn.StopRecording();
            RecordingStopped?.Invoke(this, new EventArgs());
        }

        public void Start() {
            try
            {
                WaveIn.StartRecording();
                timer.Start();
                RecordingStarted?.Invoke(this, new EventArgs());
                Console.WriteLine("Audio recording started");
            }
            catch
            {
                string msg = "Could not record from audio device!\n\n";
                msg += "Is your microphone plugged in?\n";
                msg += "Is it set as your default recording device?";
                MessageBox.Show(msg, "ERROR");
            }
        }
    }
}
