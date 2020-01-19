# synesthesia
### Sync PC audio with WS812B addressable LED strips

Using the Stereo Mix audio input device in Windows, the project processes outgoing audio PCM data, performs a FFT to get frequency data, and returns frequency amplitudes, represented by colors on the LED strip. Video to come soon. I also used Windows Task Scheduler to run a build of the project on startup.

On my Ryzen 5 1600 CPU at 3.85Ghz, controlling 300 LEDs at 30fps consumes <10% utilization. This project is fairly resource-heavy! I'll try to improve its efficiency soon.

### Other projects referenced
- https://github.com/swharden/Csharp-Data-Visualization (projects/18-09-19_microphone_FFT_revisited)
- https://github.com/beakdan/NeoPixelUsbBridge

### Hardware used
- FT232R
- 2x WS812B strips (2x150 LEDs = 300 pixels)
- 2x 5v/2a USB power supplies, USB cables repurposed as powerlines to the strips

### Dependencies
- Accord Audio (3.8.0)
- NAudio (1.8.4)
