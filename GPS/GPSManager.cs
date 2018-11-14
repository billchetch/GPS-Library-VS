using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPS
{
    public class GPSManager
    {
        private GPSSerialDevice device;
        private GPSDB db;
        private NMEAInterpreter nmea;
        private long sentenceLastReceived = -1;
        private String lastSentenceReceived;
        private double currentDOP;
        private long positionLastSaved;
        private int savePositionWait = 5;
        private long satelliteLastSaved;
        private int saveSatelliteWait = 5;
        
        public GPSManager(String deviceDescription, String server, String database, String username, String password)
        {
            device = new GPSSerialDevice(deviceDescription);
            db = new GPSDB(server, database, username, password);
            nmea = new NMEAInterpreter();
            nmea.HDOPReceived += OnHDOPReceived;
            nmea.PositionReceived += OnPositionReceived;
            nmea.SatellitesReceived += OnSatellitesReceived;
            device.NewSentenceReceived += OnNewSentenceReceived;
        }

        public GPSSerialDevice Device { get { return this.device; } }

        public void StartRecording()
        {
            try
            {

                db.SaveStatus("ready");
                db.EmptyPositionsTable();
                db.EmptySatellitesTable();
                sentenceLastReceived = -1;
                device.StartListening();
                db.SaveStatus("recording");
            } catch (Exception ex)
            {
                db.SaveStatus("error", ex.Message);
                throw ex;
            }
        }

        public void StopRecording()
        {
            sentenceLastReceived = -1;
            lastSentenceReceived = null;
            device.StopListening();
            db.SaveStatus("ready");
        }

        public void OnNewSentenceReceived(String sentence)
        {
            if (sentence != null && !sentence.Equals(""))
            {
                sentenceLastReceived = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                lastSentenceReceived = sentence;
                nmea.Parse(sentence);
            }
        }

        public String LastSentenceReceived { get { return this.lastSentenceReceived; } }

        public long TimeSinceLastReceived
        {
            get
            {
                if (sentenceLastReceived == -1)
                {
                    return -1;
                }
                else
                {
                    long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    return milliseconds - sentenceLastReceived;
                }
            }
        }

        private void OnHDOPReceived(double value)
        {
            // Remember the current HDOP value
            currentDOP = value;
        }

        private void OnPositionReceived(double latitude, double longitude)
        {
            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (positionLastSaved == 0 || (milliseconds - positionLastSaved > savePositionWait * 1000))
            {
                positionLastSaved = milliseconds;
                db.AddPositionData(latitude, longitude, currentDOP, nmea.LastSentenceType);
            }

        }

        private void OnSatellitesReceived(int svsInView, int[][] satellites)
        {
            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (satelliteLastSaved == 0 || (milliseconds - satelliteLastSaved > saveSatelliteWait * 1000))
            {
                satelliteLastSaved = milliseconds;
                for (int i = 0; i < satellites.Length; i++)
                {
                    int[] satellite = satellites[i];
                    if (satellite != null && satellite.Length == 4)
                    {
                        db.SaveSatelliteData(svsInView, satellite[0], satellite[1], satellite[2], satellite[3]);
                    }
                } //end looping through satellites
            }
        }
    }
}
