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
            public long Timestamp = 0; //in millis

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
                Timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }

            public void SetMotionData(GPSPositionData previousPos)
            {
                double elapsed = (double)(Timestamp - previousPos.Timestamp) / 1000.0;
                if (elapsed <= 0) throw new Exception("Bad timestamp diference " + elapsed + "ms");

                //cacculate speed
                double distance = Measurement.GetDistance(Latitude, Longitude, previousPos.Latitude, previousPos.Longitude);
                Speed = distance / elapsed;
                Bearing = Measurement.GetFinalBearing(previousPos.Latitude, previousPos.Longitude, Latitude, Longitude);
            }

            public override string ToString()
            {
                String dt = (new DateTime(Timestamp * TimeSpan.TicksPerMillisecond)).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC");
                return String.Format("{0} Lat/Lon: {1},{2}, Heading: {3}deg @ {4}mps", dt, Latitude, Longitude, Bearing, Speed);
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

        private Dictionary<String, GPSPositionData> positionsToAverage = new Dictionary<string, GPSPositionData>();
        public int MinDistance { get; set; } = 20; //in meteres the minimum required distance to travel in order to establish motion data
        public double PDOPThreshold { get; set; } = 3.0; //above this then start requiring an increase in distance for calculating bearing and speed
        private GPSPositionData currentPosition = null;
        public GPSPositionData CurrentPosition {  get { return currentPosition; } }
        private GPSPositionData previousPosition = null;
        public GPSPositionData PreviousPosition { get { return previousPosition; } }

        private List<GPSSatelliteData> satellites = new List<GPSSatelliteData>();
        
        private GPSSerialDevice device;
        private GPSDB db;
        private NMEAInterpreter nmea;
        private long sentenceLastReceived = -1;
        private String lastSentenceReceived;
        private double currentHDOP;
        private double currentVDOP;
        private double currentPDOP;

        private long positionLastProcessed = 0;
        private long positionLastLogged = 0;
        public int LogPositionWait { get; set; } = 30 * 1000; //in millis
        
        public GPSManager(String deviceDescription, GPSDB db = null)
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
                db?.SaveStatus("not connected");
                CurrentState = State.NOT_CONNECTED;
                sentenceLastReceived = -1;
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Start listening for GPS data...");
                device.StartListening();
                db?.SaveStatus("connected");
                CurrentState = State.CONNECTED;
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Started listening for GPS data");
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
                long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                sentenceLastReceived = now;
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
            try
            {
                positionsToAverage[nmea.LastSentenceType] = new GPSPositionData(latitude, longitude, currentHDOP, currentVDOP, currentPDOP, nmea.LastSentenceType);

                long elapsed = now - positionLastProcessed;
                if (elapsed < 1000)
                {
                    return;
                }

                GPSPositionData newPos = new GPSPositionData();
                foreach (GPSPositionData pos in positionsToAverage.Values)
                {
                    newPos.Latitude += pos.Latitude;
                    newPos.Longitude += pos.Longitude;
                    newPos.HDOP += pos.HDOP;
                    newPos.VDOP += pos.VDOP;
                    newPos.PDOP += pos.PDOP;
                    newPos.Timestamp += pos.Timestamp;
                }
                newPos.Latitude /= positionsToAverage.Count;
                newPos.Longitude /= positionsToAverage.Count;
                newPos.HDOP /= positionsToAverage.Count;
                newPos.VDOP /= positionsToAverage.Count;
                newPos.PDOP /= positionsToAverage.Count;
                newPos.Timestamp /= positionsToAverage.Count;
                if (currentPosition != null) //use previous values, if we have moved enough then these will be updated below
                {
                    newPos.Speed = currentPosition.Speed;
                    newPos.Bearing = currentPosition.Bearing;
                    newPos.DBID = currentPosition.DBID;
                }
                positionsToAverage.Clear();

                //set new poition as the current position
                currentPosition = newPos;
                positionLastProcessed = now;

                //now determine previous position based on distance to set motion data
                if (previousPosition == null)
                {
                    previousPosition = currentPosition;
                    CurrentState = State.RECORDING;
                    Tracing?.TraceEvent(TraceEventType.Information, 0, "Recording starting at " + currentPosition.ToString());
                    db?.SaveStatus("recording");
                }
                else
                {
                    double distance = Measurement.GetDistance(previousPosition.Latitude, previousPosition.Longitude, currentPosition.Latitude, currentPosition.Longitude);
                    if ((PDOPThreshold * distance / System.Math.Max(currentPDOP, PDOPThreshold)) > MinDistance)
                    {
                        currentPosition.SetMotionData(previousPosition);
                        previousPosition = currentPosition;

                        //Console.WriteLine("Distance {0} and PDOP {1} exceed {2} so we update motion data: {3}", distance, currentPDOP, MinDistance, currentPosition.ToString());
                    }
                }
            } catch (Exception e)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 0, "OnPositionReceived: {0}", e.Message);
                return;
            }

            //here we log to database
            if (db != null)
            {
                try
                {
                    if (now - positionLastLogged > LogPositionWait)
                    {
                        //add this as a new value
                        currentPosition.DBID = 0;
                        db.WritePosition(currentPosition);
                        positionLastLogged = now;
                    }
                    else
                    {
                        //just update the same value in the database
                        db.WritePosition(currentPosition);
                    }
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 0, "OnPositionReceived: loggin to db gives {0}", e.Message);
                }
            } //end log to db
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
