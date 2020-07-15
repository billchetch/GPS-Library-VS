using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Device.Location;
using Chetch.Utilities;

namespace Chetch.GPS
{
    //class for managing GPS
    public class GPSManager
    {
        const int MAX_POSITIONS = 10; //maximum number of data in positions list
        public TraceSource Tracing { get; set; } = null;

        public class GPSPositionData
        {

            public long DBID = 0;
            public double Latitude = 0;
            public double Longitude = 0;
            public double HDOP = 0;
            public double VDOP = 0;
            public double PDOP = 0;
            public double Speed = 0;
            public double Bearing = 0; //in degrees
            String SentenceType = null;
            public long Timestamp = 0; //UTC in millis

            public GPSPositionData()
            {
            }

            public GPSPositionData(double latitude, double longitude, double hdop, double vdop, double pdop, String sentenceType)
            {
                Latitude = latitude;
                Longitude = longitude;
                HDOP = hdop;
                VDOP = vdop;
                PDOP = pdop;
                SentenceType = sentenceType;
                Timestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            }

            public void SetMotionData(GPSPositionData previousPos)
            {
                double elapsed = (double)(Timestamp - previousPos.Timestamp) / 1000.0;
                if (elapsed <= 0) throw new Exception("Bad timestamp diference");

                //cacculate speed
                double distance = Measurement.GetDistance(Latitude, Longitude, previousPos.Latitude, previousPos.Longitude);
                Speed = distance / elapsed;
                Bearing = Measurement.GetFinalBearing(previousPos.Latitude, previousPos.Longitude, Latitude, Longitude);
            }

            public override string ToString()
            {
                return String.Format("Lat/Lon: {0},{1}, Heading: {2}deg @ {3}mps", Latitude, Longitude, Bearing, Speed);
            }
        }

        //TODO: record satellite data
        public class GPSSatelliteData
        {
            public GPSSatelliteData()
            {

            }
        }

        public enum State
        {
            NOT_CONNECTED,
            CONNECTED,
            RECORDING,
            ERROR
        }

        public State CurrentState { get; set; } = State.NOT_CONNECTED;
        public bool IsConnected {
            get {
                if (!device.IsListening)
                {
                    CurrentState = State.NOT_CONNECTED;
                }
                return (CurrentState == State.CONNECTED || CurrentState == State.RECORDING);
            }
        }

        private List<GPSPositionData> positions = new List<GPSPositionData>();
        private GPSPositionData currentPosition = null;
        public GPSPositionData CurrentPosition {  get { return currentPosition; } }
        private List<GPSSatelliteData> satellites = new List<GPSSatelliteData>();
        private long processPositionWait = 500; //in millis

        private GPSSerialDevice device;
        private GPSDB db;
        private NMEAInterpreter nmea;
        private long sentenceLastReceived = -1;
        private String lastSentenceReceived;
        private double currentHDOP;
        private double currentVDOP;
        private double currentPDOP;

        private long positionLastProcessed;
        private long positionLastLogged;
        private int logPositionWait = 30*1000; //in millis
        
        public GPSManager(String deviceDescription, GPSDB db)
        {
            device = new GPSSerialDevice(deviceDescription);
            this.db = db;
            nmea = new NMEAInterpreter();
            nmea.HDOPReceived += OnHDOPReceived;
            nmea.VDOPReceived += OnVDOPReceived;
            nmea.PDOPReceived += OnPDOPReceived;
            nmea.SpeedReceived += OnSpeedReceived;
            nmea.BearingReceived += OnBearingReceived;
            nmea.PositionReceived += OnPositionReceived;
            nmea.SatellitesReceived += OnSatellitesReceived;
            device.NewSentenceReceived += OnNewSentenceReceived;
        }

        public GPSSerialDevice Device { get { return this.device; } }

        public void StartRecording()
        {
            try
            {
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Device ready");
                db?.SaveStatus("not connected");
                CurrentState = State.NOT_CONNECTED;
                sentenceLastReceived = -1;
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Start listening for GPS data...");
                device.StartListening();
                db?.SaveStatus("connected");
                CurrentState = State.CONNECTED;
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Started listening to GPS data");
            } catch (Exception ex)
            {
                db?.SaveStatus("error", ex.Message);
                Tracing?.TraceEvent(TraceEventType.Error, 0, "GPSManager.StartRecording: " + ex.Message);
                CurrentState = State.ERROR;
                throw ex;
            }
        }

        public void StopRecording()
        {
            sentenceLastReceived = -1;
            lastSentenceReceived = null;
            device.StopListening();
            Tracing?.TraceEvent(TraceEventType.Information, 0, "Stopped recording GPS data");
            db?.SaveStatus("not connected");
            CurrentState = State.NOT_CONNECTED;
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
            currentHDOP = value;
        }

        private void OnVDOPReceived(double value)
        {
            currentVDOP = value;
        }

        private void OnPDOPReceived(double value)
        {
            currentPDOP = value;
        }

        private void OnBearingReceived(double value)
        {

        }

        private void OnSpeedReceived(double value)
        {

        }

        private void OnPositionReceived(double latitude, double longitude)
        {
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long elapsed = currentPosition == null ? processPositionWait : now - positionLastProcessed;
            if (currentHDOP > 10.0 * ((double)elapsed / (double)processPositionWait)) return;

            GPSPositionData pos = new GPSPositionData(latitude, longitude, currentHDOP, currentVDOP, currentPDOP, nmea.LastSentenceType);
            if (positions.Count > MAX_POSITIONS)
            {
                positions.RemoveAt(0);
            }
            positions.Insert(0, pos);
            if (positions.Count < MAX_POSITIONS) return;

            if (CurrentState == State.CONNECTED)
            {
                CurrentState = State.RECORDING;
                db?.SaveStatus("recording");
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Started recording");
            }
            GPSPositionData newPos = new GPSPositionData();
            foreach(GPSPositionData pd in positions)
            {
                newPos.Latitude += pd.Latitude;
                newPos.Longitude += pd.Longitude;
                newPos.HDOP += pd.HDOP;
                newPos.VDOP += pd.VDOP;
                newPos.PDOP += pd.PDOP;
                newPos.Timestamp += pd.Timestamp;
            }

            newPos.Latitude /= positions.Count;
            newPos.Longitude /= positions.Count;
            newPos.HDOP /= positions.Count;
            newPos.VDOP /= positions.Count;
            newPos.PDOP /= positions.Count;
            newPos.Timestamp /= positions.Count;

            if (currentPosition != null)
            {
                try
                {
                    newPos.SetMotionData(currentPosition);
                    if (now - positionLastLogged > logPositionWait)
                    {
                        //add this as a new value
                        db?.WritePosition(newPos);
                        positionLastLogged = now;
                    }
                    else
                    {
                        //just update the same value in the database
                        newPos.DBID = currentPosition.DBID;
                        db?.WritePosition(newPos);
                    }
                    positionLastProcessed = now;
                    currentPosition = newPos;
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 0, "OnPositionReceived: {0}", e.Message);
                }
            }
            else
            {
                try
                {
                    db?.WritePosition(newPos);
                    currentPosition = newPos;
                    positionLastProcessed = now;
                    positionLastLogged = now;
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 0, "OnPositionReceived: {0}", e.Message);
                }
            }
        }

        private void OnSatellitesReceived(int svsInView, int[][] satellites)
        {
            if(satellites.Length > 0)
            {
                this.satellites.Clear();
            }
            for (int i = 0; i < satellites.Length; i++)
            {
                int[] satellite = satellites[i];
                if (satellite != null && satellite.Length == 4)
                {
                    //db.SaveSatelliteData(svsInView, satellite[0], satellite[1], satellite[2], satellite[3]);
                    //this.satellites.Add(new GPSSatelliteData(svsInView, satellite));
                }
            } //end looping through satellites
        }
    }
}
