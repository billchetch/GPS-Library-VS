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
        
        static public GPSDB Create(System.Configuration.ApplicationSettingsBase settings)
        {
            GPSDB db = DB.Create<GPSDB>(settings);
            return db;
        }

        override public void Initialize()
        {
            AddSelectStatement("gps_positions", null, null, "updated DESC", "250"); 
            
            
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
        
        public List<DBRow> GetLatestPositions()
        {
            return Select("gps_positions", null, null);
        }

        public DBRow GetPosition(String dateTime)
        {
            return null;
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