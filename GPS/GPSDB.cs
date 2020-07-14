using System;
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
        public class SysInfo : DBRow
        {
            public DateTime Updated { get; internal set; }
            
            override public void AddField(String fieldName, Object fieldValue)
            {
                base.AddField(fieldName, fieldValue);

                switch (fieldName)
                {
                    case "updated":
                        //TODO: parse in to date object
                        break;
                }
            }
        }

        const String STATUS_FILTER = "data_name='gps_device_status'";

        static public GPSDB Create(System.Configuration.ApplicationSettingsBase settings)
        {
            GPSDB db = DB.Create<GPSDB>(settings);
            return db;
        }

        override public void Initialize()
        {
            AddSelectStatement("gps_positions", null, null, "updated DESC", "250"); 
            
            AddInsertStatement("gps_positions", "latitude={0},longitude={1},accuracy={2}");
            AddInsertStatement("gps_device_status", "sys_info", STATUS_FILTER + ", data_value='{0}'");
            
            AddUpdateStatement("gps_device_status", "sys_info", "data_value='{0}'", STATUS_FILTER);
            
            base.Initialize();
        }

        public void SaveStatus(String status, String statusMessage)
        {
            
            String statusData = "{\"status\": \"" + status + "\", \"message\": \"" + statusMessage + "\"}";
            
            if (Count("sys_info", STATUS_FILTER) == 0)
            {
                Insert("gps_device_status", statusData);
            } else
            {
                Update("gps_device_status", statusData);
            }
        }

        public void SaveStatus(String status)
        {
            SaveStatus(status, "");
        }

        public List<DBRow> GetLatestPositions()
        {
            return Select("gps_positions", null, null);
        }
        


        public long WritePosition(GPSManager.GPSPositionData pos)
        {
            DBRow row = new DBRow();
            row.AddField("latitude", pos.Latitude);
            row.AddField("longitude", pos.Longitude);
            row.AddField("hdop", pos.HDOP);
            row.AddField("vdop", pos.VDOP);
            row.AddField("pdop", pos.PDOP);
            row.AddField("bearing", pos.Bearing);
            row.AddField("speed", pos.Speed);
            if(pos.DBID == 0)
            {
                pos.DBID = Insert("gps_positions", row);
                return pos.DBID;
            } else
            {
                Update("gps_positions", row, pos.DBID);
                return pos.DBID;
            }
        }
    }
}