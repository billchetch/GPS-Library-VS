﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Chetch.Database;

namespace Chetch.GPS
{
    //specific database for recording gps data.
    public class GPSDB : Chetch.Database.DB
    {
        static public GPSDB Create(System.Configuration.ApplicationSettingsBase settings)
        {
            GPSDB db = DB.Create<GPSDB>(settings);
            return db;
        }

        public class GPSPositionRow : DBRow
        {
            public override void AddField(string fieldName, object fieldValue)
            {
                switch (fieldName)
                {
                    case "timestamp":
                        base.AddField(fieldName, fieldValue);
                        break;

                    default:
                        base.AddField(fieldName, fieldValue);
                        break;
                }
            }

            public void Read(GPSManager.GPSPositionData pos)
            {
                ID = pos.DBID;
                AddField("latitude", pos.Latitude);
                AddField("longitude", pos.Longitude);
                AddField("hdop", pos.HDOP);
                AddField("vdop", pos.VDOP);
                AddField("pdop", pos.PDOP);
                AddField("bearing", pos.Bearing);
                AddField("speed", pos.Speed);
                AddField("timestamp", pos.Timestamp);
            }

            public void Assign(GPSManager.GPSPositionData pos)
            {
                pos.DBID = this.ID;
                pos.Latitude = System.Convert.ToDouble(this["latitude"]);
                pos.Longitude = System.Convert.ToDouble(this["longitude"]);
                pos.Bearing = System.Convert.ToDouble(this["bearing"]);
                pos.Speed = System.Convert.ToDouble(this["speed"]);
                pos.HDOP = System.Convert.ToDouble(this["hdop"]);
                pos.VDOP = System.Convert.ToDouble(this["vdop"]);
                pos.PDOP = System.Convert.ToDouble(this["pdop"]);
                pos.Timestamp = GetDateTime("timestamp");
            }
        }
        
        override public void Initialize()
        {
            AddSelectStatement("gps_positions", null, null, "timestamp DESC", "250");
            AddSelectStatement("gps_nearest_position", "*,abs(time_to_sec(timediff('{0}', timestamp))) as secs_diff", "gps_positions", null, "secs_diff, timestamp DESC", "250");

            base.Initialize();
        }

        public void SaveStatus(String status, String statusMessage = "")
        {
            SysInfo si = GetSysInfo("gps_device_status");
            if(si == null)
            {
                si = new SysInfo();
            }

            si.SetValue("status", status);
            si.SetValue("message", statusMessage);

            SaveSysInfo(si);
        }
        
        public List<GPSPositionRow> GetLatestPositions()
        {
            return Select<GPSPositionRow>("gps_positions", null, null);
        }

        public GPSPositionRow GetLatestPosition()
        {
            GPSPositionRow row = SelectRow<GPSPositionRow>("gps_positions", null, null);
            return row;
        }

        public GPSPositionRow GetNearestPosition(String dateTime)
        {
            GPSPositionRow row = SelectRow<GPSPositionRow>("gps_nearest_position", "*", dateTime);
            return row;
        }
        
        public long WritePosition(GPSManager.GPSPositionData pos)
        {
            GPSPositionRow row = new GPSPositionRow();
            row.Read(pos);

            pos.DBID = Write("gps_positions", row);
            return pos.DBID;
        }
    }
}