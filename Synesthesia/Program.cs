using System;
using System.Threading;

namespace Synesthesia
{
    class Program
    {
        static void Main()
        {
            new Thread(delegate ()
            {
                int deviceID = 2;
                int fps = 30;
                String commPort = "COM7";
                int pixelCount = 300;
                int baudRate = 2400000;
                String lowColor = "#000018";
                String highColor = "#E00030";

                new SynesthesiaInstance(deviceID, fps, commPort, pixelCount, baudRate, lowColor, highColor);
            }).Start();
        }
    }
}