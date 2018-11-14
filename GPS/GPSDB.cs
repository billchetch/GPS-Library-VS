using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace GPS
{
    //specific database for recording gps data.
    public class GPSDB : Database.DB
    {
        const String STATUS_FILTER = "data_name='gps_device_status'";
        
        private bool overwritePositionData = false;
        private bool overwriteSatelliteData = false;

        public GPSDB(String server, String db, String username, String password) : base(server, db, username, password)
        {
            this.AddInsertStatement("gps_positions", "latitude={0},longitude={1},accuracy={2},sentence_type='{3}'");
            this.AddSelectStatement("gps_positions", "*", null, null);
            this.AddDeleteStatement("gps_positions", "updated IS NOT NULL", null, null); //delete all
            this.AddDeleteStatement("first_gps_position", "gps_positions", null, "updated ASC", "1"); //delect most ancient recorded position (for queue structure to table)

            this.AddInsertStatement("gps_device_status", "sys_info", "data_name='gps_device_status', data_value='{0}'");
            this.AddUpdateStatement("gps_device_status", "sys_info", "data_value='{0}', updated=NOW()", STATUS_FILTER);

            this.AddInsertStatement("gps_satellites", "sv_count={0},prn={1},elevation={2},azimuth={3},snr={4}");
            this.AddSelectStatement("gps_satellites", "*", null, null);
            this.AddDeleteStatement("gps_satellites", "updated IS NOT NULL", null, null);
            this.AddDeleteStatement("first_gps_satellite", "gps_satellites", null, "updated ASC", "1"); //delect most ancient recorded position (for queue structure to table)
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

        public void EmptyPositionsTable()
        {
            Delete("gps_positions");
        }

        public void AddPositionData(double latitude, double longitude, double accuracy, String sentenceType)
        {
            if (!overwritePositionData && Count("gps_positions") >= 10)
            {
                overwritePositionData = true;
            }
            if (overwritePositionData)
            {
                Delete("first_gps_position");
            }
            Insert("gps_positions", latitude.ToString(CultureInfo.InvariantCulture), longitude.ToString(CultureInfo.InvariantCulture), accuracy.ToString(CultureInfo.InvariantCulture), sentenceType);
        }

        public void EmptySatellitesTable()
        {
            Delete("gps_satellites");
        }

        public void SaveSatelliteData(int svsInView, int prn, int elevation, int azimuth, int snr)
        {
            if (!overwriteSatelliteData && Count("gps_satellites") >= 10)
            {
                overwriteSatelliteData = true;
            }
            if (overwriteSatelliteData)
            {
                Delete("first_gps_satellite");
            }
            Insert("gps_satellites", svsInView.ToString(CultureInfo.InvariantCulture), prn.ToString(CultureInfo.InvariantCulture), elevation.ToString(CultureInfo.InvariantCulture), azimuth.ToString(CultureInfo.InvariantCulture), snr.ToString(CultureInfo.InvariantCulture));
        }
    }
}