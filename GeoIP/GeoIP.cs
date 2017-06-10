using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Fougerite;
using Fougerite.Events;

namespace GeoIP
{
    public class GeoIP : Fougerite.Module
    {
        public IniParser CountryData;
        public IniParser CountryInfo;
        public static IniParser CityInfo;
        public static IniParser CityData;
        public IniParser Settings;
        public Dictionary<string, string> datas;
        public static Dictionary<int, IPLocationData> ipdatas;
        public static Dictionary<string, IPData> CachedIPs;
        public static Dictionary<string, GCityData> CachedCitys;
        public static GeoIP _instance;
        public static bool GetIPDataOnConnectionBeforePluginsToCache = false;
        //private static bool UseURL;
        public static string URL;

        public override string Name
        {
            get { return "GeoIP"; }
        }

        public override string Author
        {
            get { return "DreTaX, iScripters"; }
        }

        public override string Description
        {
            get { return "GeoIP"; }
        }

        public override Version Version
        {
            get { return new Version("1.4"); }
        }

        public static GeoIP Instance
        {
            get { return _instance; }
        }

        public override void Initialize()
        {
            _instance = this;
            datas = new Dictionary<string, string>();
            ipdatas = new Dictionary<int, IPLocationData>();
            CachedIPs = new Dictionary<string, IPData>();
            CachedCitys = new Dictionary<string, GCityData>();
            if (!File.Exists(Path.Combine(ModuleFolder, "Settings.ini")))
            {
                File.Create(Path.Combine(ModuleFolder, "Settings.ini")).Dispose();
                Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                //Settings.AddSetting("Settings", "ReadCitiesFromURL", "True");
                Settings.AddSetting("Settings", "GetIPDataOnConnectionBeforePluginsToCache", "True");
                Settings.AddSetting("Settings", "URL", "https://stats.pluton.team/PlutonGeoIP/?ip=");
                Settings.Save();
            }
            Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
            URL = Settings.GetSetting("Settings", "URL");
            GetIPDataOnConnectionBeforePluginsToCache = Settings.GetBoolSetting("Settings", "GetIPDataOnConnectionBeforePluginsToCache");
            /*UseURL = Settings.GetBoolSetting("Settings", "ReadCitiesFromURL");
            if (!UseURL && File.Exists(Path.Combine(ModuleFolder, "CityData.ini")))
            {
                
            }
            else
            {
                UseURL = true;
            }*/
            CountryData = new IniParser(Path.Combine(ModuleFolder, "CountryData.ini"));
            CountryInfo = new IniParser(Path.Combine(ModuleFolder, "CountryInfo.ini"));
            CityInfo = new IniParser(Path.Combine(ModuleFolder, "CityInfo.ini"));
            var enumm = CountryData.EnumSection("IPs");

            foreach (var x in enumm)
            {
                var data = CountryData.GetSetting("IPs", x);
                if (!datas.ContainsKey(x))
                {
                    datas.Add(x, data);
                }
            }
            var enumm2 = CountryInfo.EnumSection("Info");
            foreach (var x in enumm2)
            {
                var data = CountryInfo.GetSetting("Info", x);
                int key = int.Parse(x);
                ipdatas[key] = new IPLocationData(key, data);
            }
            Hooks.OnPlayerApproval += OnPlayerApproval;
            Hooks.OnCommand += OnCommand;
        }

        public override void DeInitialize()
        {
            Hooks.OnPlayerApproval -= OnPlayerApproval;
            Hooks.OnCommand -= OnCommand;
        }

        public void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            if (cmd == "geoip")
            {
                if (player.Admin)
                {
                    Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                    URL = Settings.GetSetting("Settings", "URL");
                    GetIPDataOnConnectionBeforePluginsToCache = Settings.GetBoolSetting("Settings", "GetIPDataOnConnectionBeforePluginsToCache");
                    player.Message("Reloaded!");
                }
            }
        }

        public void OnPlayerApproval(PlayerApprovalEvent playerApprovalEvent)
        {
            if (GetIPDataOnConnectionBeforePluginsToCache)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    string ip = playerApprovalEvent.ClientConnection.netUser.playerClient.netPlayer.externalIP;
                    if (!CachedIPs.ContainsKey(ip))
                    {
                        GetDataOfIP(ip);
                    }
                }).Start();
            }
        }

        public IPData GetDataOfIP(string ip)
        {
            if (ip == "127.0.0.1")
            {
                return null;
            }
            try
            {
                IPAddress newip = IPAddress.Parse(ip);
                string keyweneed =
                    (from x in datas.Keys
                        let y = x.Split('-')
                        where new IPAddressRange(y[0], y[1]).IsInRange(newip)
                        select x).FirstOrDefault();
                if (keyweneed != null)
                {
                    if (!CachedIPs.ContainsKey(ip))
                    {
                        var data = new IPData(ip, datas[keyweneed], keyweneed);
                        CachedIPs[ip] = data;
                        return data;
                    }
                    return CachedIPs[ip];
                }
            }
            catch (Exception ex)
            {
                if (CachedIPs.ContainsKey(ip))
                {
                    CachedIPs.Remove(ip);
                }
                if (CachedCitys.ContainsKey(ip))
                {
                    CachedCitys.Remove(ip);
                }
                Logger.LogError("[GeoIP] Error Happened. Check the logs");
                Logger.LogDebug("[GeoIP] " + ex + " IpAddress: " + ip);
            }
            return null;
        }

        public class IPLocationData
        {
            private string _CountryCode, _ContinentShort, _Continent, _CountryShort, _Country; 

            public IPLocationData(int countrycode, string data)
            {
                _CountryCode = countrycode.ToString();
                var ndata = data.Split(Convert.ToChar(","));
                _ContinentShort = ndata[1];
                _Continent = ndata[2];
                _CountryShort = ndata[3];
                _Country = ndata[4];
            }

            public string CountryCode
            {
                get { return _CountryCode; }
            }

            public string ContinentShort
            {
                get { return _ContinentShort; }
            }

            public string Continent
            {
                get { return _Continent; }
            }

            public string CountryShort
            {
                get { return _CountryShort; }
            }

            public string Country
            {
                get { return _Country; }
            }
        }

        public class GCityData
        {
            private string _geoid, _cgeoid, _latitude, _longitude;
            private string _country, _countrys, _continent, _continents;
            private bool _is_anonymous_proxy, _is_satellite_provider;

            public GCityData(string ip)
            {
                string data = "";
                //if (UseURL)
                //{
                    ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
                    using (System.Net.WebClient client = new System.Net.WebClient())
                    {
                        data = client.DownloadString(URL + ip);
                    }
                //}
                string[] split = data.Split(',');
                _geoid = split[0];
                _cgeoid = split[1];
                _is_anonymous_proxy = split[3] != "0";
                _is_satellite_provider = split[4] != "0";
                _latitude = split[6];
                _longitude = split[7];
                string data2 = CityInfo.GetSetting("Info", _geoid);
                string[] split2 = data2.Split(',');
                _country = split2[4];
                _countrys = split2[3];
                _continent = split2[2];
                _continents = split2[1];
            }

            public bool AcceptAllCertifications(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
            {
                return true;
            }

            public string GeoID
            {
                get { return _geoid; }
            }

            public string CountryGeoID
            {
                get { return _cgeoid; }
            }

            public bool AnonymousProxy
            {
                get { return _is_anonymous_proxy; }
            }

            public bool SatelliteProvider
            {
                get { return _is_satellite_provider; }
            }

            public string Latitude
            {
                get { return _latitude; }
            }

            public string Longitude
            {
                get { return _longitude; }
            }

            public string Country
            {
                get { return _country; }
            }

            public string CountryShort
            {
                get { return _countrys; }
            }

            public string Continent
            {
                get { return _continent; }
            }

            public string ContinentShort
            {
                get { return _continents; }
            }
        }

        public class IPData
        {
            private string _CountryCode, _CountryCode2, _IP;
            private bool _IsAnonymousProxy, _IsSatelliteProvider;
            private IPLocationData _IPLocationData;
            private string _keyweneed;
            private GCityData _citydata;

            public IPData(string ip, string data, string keyweneed)
            {
                var ndata = data.Split(Convert.ToChar(","));
                _CountryCode = ndata[0];
                _CountryCode2 = ndata[1];
                _IsAnonymousProxy = ndata[3] != "0";
                _IsSatelliteProvider = ndata[4] != "0";
                _IP = ip;
                _IPLocationData = ipdatas[int.Parse(_CountryCode)];
                _keyweneed = keyweneed;
                if (!CachedCitys.ContainsKey(ip))
                {
                    _citydata = new GCityData(ip);
                    CachedCitys[ip] = _citydata;
                }
                _citydata = CachedCitys[ip];
            }

            public GCityData CityData
            {
                get { return _citydata; }
            }

            public string IPRange
            {
                get { return _keyweneed; }
            }

            public string CountryCode
            {
                get { return _CountryCode; }
            }

            public string RegisteredCountryCode
            {
                get { return _CountryCode2; }
            }

            public bool IsAnonymousProxy
            {
                get { return _IsAnonymousProxy; }
            }

            public bool IsSatelliteProvider
            {
                get { return _IsSatelliteProvider; }
            }

            public IPLocationData IPLocationData
            {
                get { return _IPLocationData; }
            }

            public string Country
            {
                get { return IPLocationData.Country; }
            }

            public string Continent
            {
                get { return IPLocationData.Continent; }
            }

            public string ContinentShort
            {
                get { return IPLocationData.ContinentShort; }
            }

            public string CountryShort
            {
                get { return IPLocationData.CountryShort; }
            }

            public string StoredIP
            {
                get { return _IP; }
            }
        }

        public class IPAddressRange
        {
            readonly AddressFamily addressFamily;
            readonly byte[] lowerBytes;
            readonly byte[] upperBytes;

            public IPAddressRange(string l, string u)
            {
                var lower = IPAddress.Parse(l);
                var upper = IPAddress.Parse(u);
                this.addressFamily = lower.AddressFamily;
                this.lowerBytes = lower.GetAddressBytes();
                this.upperBytes = upper.GetAddressBytes();
            }

            public IPAddressRange(IPAddress lower, IPAddress upper)
            {
                this.addressFamily = lower.AddressFamily;
                this.lowerBytes = lower.GetAddressBytes();
                this.upperBytes = upper.GetAddressBytes();
            }

            public bool IsInRange(string address)
            {
                return IsInRange(IPAddress.Parse(address));
            }

            public bool IsInRange(IPAddress address)
            {
                if (address.AddressFamily != addressFamily)
                {
                    return false;
                }

                byte[] addressBytes = address.GetAddressBytes();

                bool lowerBoundary = true, upperBoundary = true;

                for (int i = 0; i < this.lowerBytes.Length &&
                    (lowerBoundary || upperBoundary); i++)
                {
                    if ((lowerBoundary && addressBytes[i] < lowerBytes[i]) ||
                        (upperBoundary && addressBytes[i] > upperBytes[i]))
                    {
                        return false;
                    }

                    lowerBoundary &= (addressBytes[i] == lowerBytes[i]);
                    upperBoundary &= (addressBytes[i] == upperBytes[i]);
                }

                return true;
            }
        }
    }
}
