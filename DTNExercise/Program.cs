using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace DTNExercise
{
    class Program
    {
        static readonly string lightningFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data","Lightning Strike.json");
        static readonly string assetFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets","Assets.json");

        static readonly string notificationLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs","Notification.txt");      
        static readonly string notFoundQuadKeyLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs","NotFoundQuadKey.txt");
        static readonly string connectionLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs","Connection.txt");
        static readonly string errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs","Error.txt");
        

        private const double EarthRadius = 6378137;
        private const double MinLatitude = -85.05112878;
        private const double MaxLatitude = 85.05112878;
        private const double MinLongitude = -180;
        private const double MaxLongitude = 180;
        static void Main(string[] args)
        {
            var LightningStrike = new LightningStrike();
            var AssetOwners = new List<AssetOwner>();
            var NotifiedAssetOwners = new List<string>();
            DateTime startingDate = new DateTime(1970, 1, 1);
            string line = string.Empty;
            string QuadKey = string.Empty;
            int pixelX, pixelY;
            int tileX, tileY;         

            try
            {
                CreateDirectoriesIfNotExist();
                if (File.Exists(assetFile))
                {           
                    //Convert AssetOwners JSON file into list of objects(AssetOwner)        
                    AssetOwners = JsonConvert.DeserializeObject<List<AssetOwner>>(File.ReadAllText(assetFile));
                 
                    if (File.Exists(lightningFile))
                    {                       
                        using (StreamReader file = new StreamReader(lightningFile))
                        {                            
                            while ((line = file.ReadLine()) != null)
                            {
                               //get each line of json file and convert it to Lightningstrike object
                               LightningStrike = JsonConvert.DeserializeObject<LightningStrike>(line);

                               if (LightningStrike.flashType == 0 || LightningStrike.flashType == 1)
                                {
                                    //convert lat/long format to quadkey
                                    LatLongToPixelXY(LightningStrike.latitude, LightningStrike.longitude, 12, out pixelX, out pixelY);
                                    PixelXYToTileXY(pixelX, pixelY, out tileX, out tileY);
                                    QuadKey = TileXYToQuadKey(tileX, tileY, 12);

                                    if (AssetOwners.Any(x => x.quadKey == QuadKey))
                                    {
                                        //get assetowner based from quadkey
                                        AssetOwner AssetOwnerToBeNotified = AssetOwners.Find(x => x.quadKey == QuadKey);

                                        //check if asset owner is already notified
                                        if (!NotifiedAssetOwners.Contains(AssetOwnerToBeNotified.assetOwner))
                                        {
                                            Console.WriteLine(String.Concat("Lightning alert for ", AssetOwnerToBeNotified.assetOwner, ":", AssetOwnerToBeNotified.assetName));
                                            //Log Notification
                                            File.AppendAllText(notificationLog, String.Concat(DateTime.Now.ToString(), "- ", 
                                                "ReceivedTime: ", startingDate.AddMilliseconds(LightningStrike.receivedTime),
                                                " AssetOwner: ", AssetOwnerToBeNotified.assetOwner,
                                                " AssetName: ", AssetOwnerToBeNotified.assetName,
                                                Environment.NewLine));
                                            //add assetowner to this list to stop receiving notification
                                            NotifiedAssetOwners.Add(AssetOwnerToBeNotified.assetOwner);
                                        }  
                                    }
                                    else
                                    {
                                        //Log lightning strike for not found asset owner
                                        File.AppendAllText(notFoundQuadKeyLog, String.Concat(DateTime.Now.ToString(), "- ",
                                              "ReceivedTime: ", startingDate.AddMilliseconds(LightningStrike.receivedTime),
                                              " Latitude: ", LightningStrike.latitude,
                                              " Longitude: ", LightningStrike.longitude,
                                              Environment.NewLine));                                       
                                    }
                                }
                               else
                                {
                                    if (LightningStrike.flashType == 9)
                                        //Logs Connection Test
                                        File.AppendAllText(connectionLog, String.Concat(DateTime.Now.ToString(), "- ",
                                            "Connection Test ",
                                            "ReceivedTime: ", startingDate.AddMilliseconds(LightningStrike.receivedTime),                                         
                                            Environment.NewLine));
                                    else
                                        //Logs invalid flash type
                                        File.AppendAllText(errorLog, String.Concat(DateTime.Now.ToString(), "- ",
                                            "Error: Invalid flash type",
                                            "ReceivedTime: ", startingDate.AddMilliseconds(LightningStrike.receivedTime),
                                            Environment.NewLine));
                                }                               
                            }
                            file.Close();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No files found");
                        Console.ReadLine();
                    }
                }
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                //Log Error encountered
                File.AppendAllText(errorLog, String.Concat(DateTime.Now.ToString(), "- ",
                                           "Error:", ex.Message,
                                           Environment.NewLine));
            }
            
        }
        #region QuadKey Conversion
        private static double Clip(double n, double minValue, double maxValue)
        {
            return Math.Min(Math.Max(n, minValue), maxValue);
        }
        public static uint MapSize(int levelOfDetail)
        {
            return (uint)256 << levelOfDetail;
        }
        public static void LatLongToPixelXY(double latitude, double longitude, int levelOfDetail, out int pixelX, out int pixelY)
        {
            latitude = Clip(latitude, MinLatitude, MaxLatitude);
            longitude = Clip(longitude, MinLongitude, MaxLongitude);

            double x = (longitude + 180) / 360;
            double sinLatitude = Math.Sin(latitude * Math.PI / 180);
            double y = 0.5 - Math.Log((1 + sinLatitude) / (1 - sinLatitude)) / (4 * Math.PI);

            uint mapSize = MapSize(levelOfDetail);
            pixelX = (int)Clip(x * mapSize + 0.5, 0, mapSize - 1);
            pixelY = (int)Clip(y * mapSize + 0.5, 0, mapSize - 1);
        }
        public static void PixelXYToTileXY(int pixelX, int pixelY, out int tileX, out int tileY)
        {
            tileX = pixelX / 256;
            tileY = pixelY / 256;
        }
        public static string TileXYToQuadKey(int tileX, int tileY, int levelOfDetail)
        {
            StringBuilder quadKey = new StringBuilder();
            for (int i = levelOfDetail; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((tileX & mask) != 0)
                {
                    digit++;
                }
                if ((tileY & mask) != 0)
                {
                    digit++;
                    digit++;
                }
                quadKey.Append(digit);
            }
            return quadKey.ToString();
        }    
            #endregion
        private static void CreateDirectoriesIfNotExist()
        {
            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data")))           
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"));

            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"));

            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"));
        }



        private class LightningStrike
        {
            public int flashType { get; set; }
            public ulong strikeTime { get; set; }
            public double longitude { get; set; }
            public double latitude { get; set; }
            public int peakAmps { get; set; }
            public string reserved { get; set; }
            public int icHeight { get; set; }
            public ulong receivedTime { get; set; }
            public int numberOfSensors { get; set; }
            public int multiplicity { get; set; }
        }
        private class AssetOwner
        {
            public string assetName { get; set; }
            public string quadKey { get; set; }
            public string assetOwner { get; set; }
        }

    }
}
