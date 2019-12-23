using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Chetch.GPS
{
    public class NMEAInterpreter
    {
        // Represents the EN-US culture, used for numers in NMEA sentences
        public static CultureInfo NMEACultureInfo = new CultureInfo("en-US");
        // Used to convert knots into miles per hour
        public static double MPHPerKnot = double.Parse("1.150779",
          NMEACultureInfo);

        public String LastSentenceType;

        #region Delegates
        public delegate void PositionReceivedEventHandler(double latitude, double longitude);
        public delegate void DateTimeChangedEventHandler(System.DateTime dateTime);
        public delegate void BearingReceivedEventHandler(double bearing);
        public delegate void SpeedReceivedEventHandler(double speed);
        public delegate void SpeedLimitReachedEventHandler();
        public delegate void FixObtainedEventHandler();
        public delegate void FixLostEventHandler();
        public delegate void SatellitesReceivedEventHandler(int svsInView, int[][] satellites);
        public delegate void HDOPReceivedEventHandler(double value);
        public delegate void VDOPReceivedEventHandler(double value);
        public delegate void PDOPReceivedEventHandler(double value);
        #endregion

        #region Events
        public event PositionReceivedEventHandler PositionReceived;
        public event DateTimeChangedEventHandler DateTimeChanged;
        public event BearingReceivedEventHandler BearingReceived;
        public event SpeedReceivedEventHandler SpeedReceived;
        public event SpeedLimitReachedEventHandler SpeedLimitReached;
        public event FixObtainedEventHandler FixObtained;
        public event FixLostEventHandler FixLost;
        public event SatellitesReceivedEventHandler SatellitesReceived;
        public event HDOPReceivedEventHandler HDOPReceived;
        public event VDOPReceivedEventHandler VDOPReceived;
        public event PDOPReceivedEventHandler PDOPReceived;
        #endregion

        // Processes information from the GPS receiver
        public bool Parse(string sentence)
        {
            // Discard the sentence if its checksum does not match our
            // calculated checksum
            if (!IsValid(sentence)) return false;

            //remove the checksum digit
            if (sentence.IndexOf("*") > 0)
            {
                sentence = sentence.Substring(0, sentence.IndexOf("*"));
            }

            // Look at the first word to decide where to go next
            LastSentenceType = GetWords(sentence)[0];
            switch (LastSentenceType)
            {
                case "$GPRMC":
                    // A "Recommended Minimum" sentence was found!
                    return ParseGPRMC(sentence);
                case "$GPGSV":
                    // A "Satellites in View" sentence was received
                    return ParseGPGSV(sentence);
                case "$GPGSA":
                    return ParseGPGSA(sentence);
                case "$GPGGA":
                    return ParseGPGGA(sentence);
                case "$GPGLL":
                    return ParseGPGLL(sentence);
                default:
                    // Indicate that the sentence was not recognized
                    return false;
            }
        }

        // Divides a sentence into individual words
        public string[] GetWords(string sentence)
        {
            return sentence.Split(',');
        }

        // Interprets a $GPRMC message
        // Example: $GPRMC,093652.00,A,0843.89597,S,11510.24425,E,0.022,,131018,,,D*6D
        public bool ParseGPRMC(string sentence)
        {
            // Divide the sentence into words
            string[] Words = GetWords(sentence);
            // Do we have enough values to describe our location?
            if (Words[3] != "" && Words[4] != "" &&

              Words[5] != "" && Words[6] != "")
            {
                // Yes. Extract latitude and longitude
                if (false)
                {
                    // Append hours
                    string Latitude = Words[3].Substring(0, 2) + "°";
                    // Append minutes
                    Latitude = Latitude + Words[3].Substring(2) + "\"";
                    // Append hours
                    Latitude = Latitude + Words[4]; // Append the hemisphere
                    string Longitude = Words[5].Substring(0, 3) + "°";
                    // Append minutes
                    Longitude = Longitude + Words[5].Substring(3) + "\"";
                    // Append the hemisphere
                    Longitude = Longitude + Words[6];
                    // Notify the calling application of the change
                    /*if (PositionReceived != null)
                        PositionReceived(Latitude, Longitude);*/
                }
                else
                {
                    double latitude = Double.Parse(Words[3].Substring(0, 2), CultureInfo.InvariantCulture);
                    latitude += Double.Parse(Words[3].Substring(2), CultureInfo.InvariantCulture) / 60.0;
                    if (Words[4].Equals("S")) latitude *= -1;

                    double longitude = Double.Parse(Words[5].Substring(0, 3), CultureInfo.InvariantCulture);
                    longitude += Double.Parse(Words[5].Substring(3), CultureInfo.InvariantCulture) / 60.0;
                    if (Words[6].Equals("W")) longitude *= -1;

                    PositionReceived(latitude, longitude);
                }
            }
            // Do we have enough values to parse satellite-derived time?
            if (Words[1] != "")
            {
                // Yes. Extract hours, minutes, seconds and milliseconds
                int UtcHours = Convert.ToInt32(Words[1].Substring(0, 2));
                int UtcMinutes = Convert.ToInt32(Words[1].Substring(2, 2));
                int UtcSeconds = Convert.ToInt32(Words[1].Substring(4, 2));
                int UtcMilliseconds = 0;
                // Extract milliseconds if it is available
                if (Words[1].Length > 7)
                {
                    UtcMilliseconds = Convert.ToInt32(
                        float.Parse(Words[1].Substring(6), NMEACultureInfo) * 1000);
                }
                // Now build a DateTime object with all values
                System.DateTime Today = System.DateTime.Now.ToUniversalTime();
                System.DateTime SatelliteTime = new System.DateTime(Today.Year,
                  Today.Month, Today.Day, UtcHours, UtcMinutes, UtcSeconds,
                  UtcMilliseconds);
                // Notify of the new time, adjusted to the local time zone
                if (DateTimeChanged != null)
                    DateTimeChanged(SatelliteTime.ToLocalTime());
            }
            // Do we have enough information to extract the current speed?
            if (Words[7] != "")
            {
                // Yes.  Parse the speed and convert it to MPH
                double Speed = double.Parse(Words[7], NMEACultureInfo) *
                  MPHPerKnot;
                // Notify of the new speed
                if (SpeedReceived != null)
                    SpeedReceived(Speed);
                // Are we over the highway speed limit?
                if (Speed > 55)
                    if (SpeedLimitReached != null)
                        SpeedLimitReached();
            }
            // Do we have enough information to extract bearing?
            if (Words[8] != "")
            {
                // Indicate that the sentence was recognized
                double Bearing = double.Parse(Words[8], NMEACultureInfo);
                if (BearingReceived != null)
                    BearingReceived(Bearing);
            }
            // Does the device currently have a satellite fix?
            if (Words[2] != "")
            {
                switch (Words[2])
                {
                    case "A":
                        if (FixObtained != null)
                            FixObtained();
                        break;
                    case "V":
                        if (FixLost != null)
                            FixLost();
                        break;
                }
            }
            // Indicate that the sentence was recognized
            return true;
        }

        //Example: $GPGGA,093651.00,0843.89598,S,11510.24426,E,2,09,0.87,14.6,M,21.3,M,,0000*77
        public bool ParseGPGGA(string sentence)
        {
            string[] Words = GetWords(sentence);
            // Do we have enough values to describe our location?
            if (Words[2] != "" && Words[3] != "" && Words[4] != "" && Words[5] != "")
            {
                double latitude = Double.Parse(Words[2].Substring(0, 2), CultureInfo.InvariantCulture);
                latitude += Double.Parse(Words[2].Substring(2), CultureInfo.InvariantCulture) / 60.0;
                if (Words[3].Equals("S")) latitude *= -1;

                double longitude = Double.Parse(Words[4].Substring(0, 3), CultureInfo.InvariantCulture);
                longitude += Double.Parse(Words[4].Substring(3), CultureInfo.InvariantCulture) / 60.0;
                if (Words[5].Equals("W")) longitude *= -1;

                PositionReceived(latitude, longitude);
            }

            return true;
        }

        //Example: $GPGLL,0843.89598,S,11510.24426,E,093651.00,A,D*71
        public bool ParseGPGLL(string sentence)
        {
            string[] Words = GetWords(sentence);
            // Do we have enough values to describe our location?
            if (Words[1] != "" && Words[2] != "" && Words[3] != "" && Words[4] != "")
            {
                double latitude = Double.Parse(Words[1].Substring(0, 2), CultureInfo.InvariantCulture);
                latitude += Double.Parse(Words[1].Substring(2), CultureInfo.InvariantCulture) / 60.0;
                if (Words[2].Equals("S")) latitude *= -1;

                double longitude = Double.Parse(Words[3].Substring(0, 3), CultureInfo.InvariantCulture);
                longitude += Double.Parse(Words[3].Substring(3), CultureInfo.InvariantCulture) / 60.0;
                if (Words[4].Equals("W")) longitude *= -1;

                PositionReceived(latitude, longitude);
            }

            return true;
        }

        // Interprets a "Satellites in View" NMEA sentence
        public bool ParseGPGSV(string sentence)
        {
            int PseudoRandomCode = 0;
            int Azimuth = 0;
            int Elevation = 0;
            int SignalToNoiseRatio = 0;
            // Divide the sentence into words
            string[] Words = GetWords(sentence);
            // Each sentence contains four blocks of satellite information.
            // Read each block and report each satellite's information
            int[][] satellites = new int[4][];
            int Count = 0;
            int svsInView = Words.Length >= 3 && Words[2] != "" ? Convert.ToInt32(Words[2]) : 0;
            for (Count = 1; Count <= 4; Count++)
            {
                // Does the sentence have enough words to analyze?
                if ((Words.Length - 1) >= (Count * 4 + 3))
                {
                    // Yes.  Proceed with analyzing the block.
                    // Does it contain any information?
                    if (Words[Count * 4] != "" && Words[Count * 4 + 1] != ""

                       && Words[Count * 4 + 2] != "" && Words[Count * 4 + 3] != "")
                    {
                        // Yes. Extract satellite information and report it
                        PseudoRandomCode = System.Convert.ToInt32(Words[Count * 4]);
                        Elevation = Convert.ToInt32(Words[Count * 4 + 1]);
                        Azimuth = Convert.ToInt32(Words[Count * 4 + 2]);
                        SignalToNoiseRatio = Convert.ToInt32(Words[Count * 4 + 3]);
                        // Notify of this satellite's informatio
                        satellites[Count - 1] = new int[4];
                        satellites[Count - 1][0] = PseudoRandomCode;
                        satellites[Count - 1][1] = Elevation;
                        satellites[Count - 1][2] = Azimuth;
                        satellites[Count - 1][3] = SignalToNoiseRatio;
                    }
                }
            }
            if (SatellitesReceived != null)
                SatellitesReceived(svsInView, satellites);

            // Indicate that the sentence was recognized
            return true;
        }

        // Interprets a "Fixed Satellites and DOP" NMEA sentence
        public bool ParseGPGSA(string sentence)
        {
            // Divide the sentence into words
            string[] Words = GetWords(sentence);
            // Update the DOP values
            if (Words[15] != "")
            {
                if (PDOPReceived != null)
                    PDOPReceived(double.Parse(Words[15], NMEACultureInfo));
            }
            if (Words[16] != "")
            {
                if (HDOPReceived != null)
                    HDOPReceived(double.Parse(Words[16], NMEACultureInfo));
            }
            if (Words[17] != "")
            {
                if (VDOPReceived != null)
                    VDOPReceived(double.Parse(Words[17], NMEACultureInfo));
            }
            return true;
        }

        // Returns True if a sentence's checksum matches the
        // calculated checksum
        public bool IsValid(string sentence)
        {
            // Compare the characters after the asterisk to the calculation
            return sentence.Substring(sentence.IndexOf("*") + 1) ==
              GetChecksum(sentence);
        }

        // Calculates the checksum for a sentence
        public string GetChecksum(string sentence)
        {
            // Loop through all chars to get a checksum
            int Checksum = 0;
            foreach (char Character in sentence)
            {
                if (Character == '$')
                {
                    // Ignore the dollar sign
                }
                else if (Character == '*')
                {
                    // Stop processing before the asterisk
                    break;
                }
                else
                {
                    // Is this the first value for the checksum?
                    if (Checksum == 0)
                    {
                        // Yes. Set the checksum to the value
                        Checksum = Convert.ToByte(Character);
                    }
                    else
                    {
                        // No. XOR the checksum with this character's value
                        Checksum = Checksum ^ Convert.ToByte(Character);
                    }
                }
            }
            // Return the checksum formatted as a two-character hexadecimal
            return Checksum.ToString("X2");
        }
    }
}
