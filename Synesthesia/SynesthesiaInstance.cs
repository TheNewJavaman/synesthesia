using NAudio.Wave;
using System;
using System.IO.Ports;
using System.Windows.Forms;
using System.Globalization;

namespace Synesthesia
{
    class SynesthesiaInstance
    {
        private int RATE = 44800;
        private int BUFFERSIZE = (int)Math.Pow(2, 11);
        const int BYTESPERPIXEL = 8;
        static BufferedWaveProvider _bwp;

        static int _pixelCount;
        static SerialPort _serialPort;
        static byte[] _uartBuffer;

        static double[] _lastFrame;
        static int[] _lowColor;
        static int[] _highColor;
        static int[] _colorDiffs;

        static readonly byte[] bitTriplets = new byte[]
        {
            0x5b, 0x1b, 0x53, 0x13,
            0x5a, 0x1a, 0x52, 0x12
        };

        public SynesthesiaInstance(int audioDeviceNumber = 1, int fps = 30, String commPort = "COM11", int pixelCount = 32, int baudRate = 2400000, String lowColor = "#000018", String highColor = "#E00030")
        {
            _pixelCount = pixelCount;
            _uartBuffer = new byte[_pixelCount * BYTESPERPIXEL];

            WaveInEvent wi = new WaveInEvent();
            wi.DeviceNumber = audioDeviceNumber;
            wi.WaveFormat = new WaveFormat(RATE, 1);
            wi.BufferMilliseconds = (int)((double)BUFFERSIZE / (double)RATE * 1000.0);
            wi.DataAvailable += new EventHandler<WaveInEventArgs>(AudioDataAvailable);

            _bwp = new BufferedWaveProvider(wi.WaveFormat);
            _bwp.BufferLength = BUFFERSIZE * 2;
            _bwp.DiscardOnBufferOverflow = true;

            try
            {
                wi.StartRecording();
            }
            catch
            {
                Console.WriteLine("Cannot read data from audio device");
            }

            int delayMS = (int)(1000f / (float)fps);
            Timer timer = new Timer();
            timer.Tick += new EventHandler(GetAudioData);
            timer.Interval = delayMS;

            using (_serialPort = new SerialPort(commPort, baudRate, Parity.None, 7, StopBits.One))
            {
                if (!_serialPort.IsOpen)
                    _serialPort.Open();
                ClearPixels();

                _lastFrame = new double[_pixelCount];
                _lowColor = HexToInts(lowColor);
                _highColor = HexToInts(highColor);
                _colorDiffs = new int[]
                {
                    _highColor[0] - _lowColor[0],
                    _highColor[1] - _lowColor[1],
                    _highColor[2] - _lowColor[2]
                };

                timer.Start();
                while (true)
                {
                    Application.DoEvents();
                }
            }

        }

        public void ClearPixels()
        {
            int[] pixels = new int[_pixelCount];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] =
                    (0 << 8) |
                    (0 << 16) |
                    (0);
            }

            WriteColorData(pixels);
        }

        void AudioDataAvailable(object sender, WaveInEventArgs e)
        {
            _bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        public void GetAudioData(object sender, EventArgs e)
        {
            int frameSize = BUFFERSIZE;
            var audioBytes = new byte[frameSize];
            _bwp.Read(audioBytes, 0, frameSize);

            if (audioBytes.Length == 0)
                return;
            if (audioBytes[frameSize - 2] == 0)
                return;

            int BYTES_PER_POINT = 2;
            int graphPointCount = audioBytes.Length / BYTES_PER_POINT;

            double[] pcm = new double[graphPointCount];
            double[] fft = new double[graphPointCount];
            double[] fftReal = new double[graphPointCount / 2];

            for (int i = 0; i < graphPointCount; i++)
            {
                Int16 val = BitConverter.ToInt16(audioBytes, i * 2);
                pcm[i] = (double)(val) / Math.Pow(2, 16) * 200.0;
            }

            fft = FFT(pcm);
            Array.Copy(fft, fftReal, fftReal.Length);

            ProcessPixelData(fftReal);
        }

        public void ProcessPixelData(double[] fftData)
        {
            double[] fftDataReverse = new double[fftData.Length];
            for (int i = 0; i < fftData.Length; i++)
            {
                fftDataReverse[i] = fftData[fftData.Length - 1 - i];
            }

            double[] fftDataMirror = new double[fftDataReverse.Length * 2];
            for (int i = 0; i < fftDataReverse.Length; i++)
            {
                fftDataMirror[i] = fftDataReverse[i];
            }
            for (int i = 0; i < fftDataReverse.Length; i++)
            {
                fftDataMirror[i + fftDataReverse.Length] = fftDataReverse[fftDataReverse.Length - 1 - i];
            }

            double dataPointsPerPixel = (float)fftDataMirror.Length / (float)_pixelCount;
            double[] visualizableData = new double[_pixelCount];
            for (int i = 0; i < _pixelCount; i++)
            {
                int startDataPoint = (int)(i * dataPointsPerPixel);
                int stopDataPoint = (int)((i + 1) * dataPointsPerPixel);
                while (stopDataPoint >= fftDataMirror.Length)
                {
                    stopDataPoint--;
                }

                int dataPointCount = stopDataPoint - startDataPoint + 1;
                double average = 0;
                for (int j = startDataPoint; j <= stopDataPoint; j++)
                {
                    average += fftDataMirror[j] / dataPointCount;
                }

                visualizableData[i] = average / 0.2 * 255;

                if (visualizableData[i] > 255)
                {
                    visualizableData[i] = 255;
                }

                visualizableData[i] = (visualizableData[i] + _lastFrame[i]) / 2.0;
            }

            _lastFrame = visualizableData;

            ProcessColorData(visualizableData);
        }

        public void ProcessColorData(double[] data)
        {
            int[] pixels = new int[_pixelCount];
            for (int i = 0; i < pixels.Length; i++)
            {
                int value = (int)data[i];
                int[] colorDiff = new int[]
                {
                    (int)(value / 255f * _colorDiffs[0]),
                    (int)(value / 255f * _colorDiffs[1]),
                    (int)(value / 255f * _colorDiffs[2])
                };

                if (data[i] < 0)
                {
                    pixels[i] =
                        (0 << 8) |
                        (0 << 16) |
                        (0);
                } else
                {
                    pixels[i] =
                        ((colorDiff[0] + _lowColor[0]) << 8) |
                        ((colorDiff[1] + _lowColor[1]) << 16) |
                        ((colorDiff[2] + _lowColor[2]));
                }
            }

            WriteColorData(pixels);
        }

        public void WriteColorData(int[] colorData)
        {
            TranslateColors(colorData, _uartBuffer);
            _serialPort.BaseStream.Write(_uartBuffer, 0, _uartBuffer.Length);
            _serialPort.BaseStream.Flush();
        }

        public double[] FFT(double[] data)
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

        static void TranslateColors(int[] colors, byte[] UartData)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var pixOffset = i * BYTESPERPIXEL;

                UartData[pixOffset] = bitTriplets[(color >> 21) & 0x07];
                UartData[pixOffset + 1] = bitTriplets[(color >> 18) & 0x07];
                UartData[pixOffset + 2] = bitTriplets[(color >> 15) & 0x07];
                UartData[pixOffset + 3] = bitTriplets[(color >> 12) & 0x07];
                UartData[pixOffset + 4] = bitTriplets[(color >> 9) & 0x07];
                UartData[pixOffset + 5] = bitTriplets[(color >> 6) & 0x07];
                UartData[pixOffset + 6] = bitTriplets[(color >> 3) & 0x07];
                UartData[pixOffset + 7] = bitTriplets[color & 0x07];
            }
        }

        static int[] HexToInts(String hexColor)
        {
            int[] intColors = new int[3];

            intColors[0] = int.Parse(hexColor.Substring(1, 2), NumberStyles.HexNumber);
            intColors[1] = int.Parse(hexColor.Substring(3, 2), NumberStyles.HexNumber);
            intColors[2] = int.Parse(hexColor.Substring(5, 2), NumberStyles.HexNumber);

            return intColors;
        }
    }
}