using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Security.Cryptography;

namespace CurrentEstim
{
    class AIS
    {
        public readonly LatLonDT LatLonDT;
        public readonly UInt32 Mmsi;

        public readonly UInt16 COG10;      // northup,deg
        public readonly byte SOG10;      // knot
        public readonly Coord Vog;    // knot
        public readonly UInt16 Hdg;      // northup,deg
        public bool Valid;

        public AIS(string aisx,StreamWriter swErr=null)
        {
            string[] sa = aisx.Split(',');
            sa = Array.ConvertAll(sa, ss => ss.Trim());
            try
            {
                // 動的データ && under way using engine なら有効
                if (sa[1] == "Dyn" && int.Parse(sa[3]) == 0)
                {
                    Mmsi = UInt32.Parse(sa[2]);

                    DateTime dt = DateTime.SpecifyKind(DateTime.Parse(sa[0]), DateTimeKind.Utc);    // 入力ファイルの日時はUTCとする
                    LatLonDT = new LatLonDT(Single.Parse(sa[4]), Single.Parse(sa[5]), dt);
                    SOG10 = (byte)(double.Parse(sa[7]) * 10);
                    COG10 = (UInt16)(double.Parse(sa[6]) * 10);
                    Hdg = UInt16.Parse(sa[8]);
                    Vog = new Coord((Single)(0.1 * SOG10 * Math.Cos(0.1 * COG10 * Math.PI / 180)), (Single)(0.1 * SOG10 * Math.Sin(0.1 * COG10 * Math.PI / 180)));
                    Valid = true;

                }
                else
                {
                    Valid = false;
                    if (swErr != null) swErr.WriteLine("静的か力行以外," + aisx);
                }
            }
            catch (Exception e)
            {
                Program.logout(e.Message + "\r\n" + aisx);
                Valid = false;
                if (swErr != null) swErr.WriteLine("不明なエラー," + aisx);
            }
        }
    }

    class MmsiInfo 
    { 
        public UInt32 brokenCount;
        public UInt32 count;
        public List<UInt16> brokenCountLog;
        public List<UInt16> countLog;
        public string status;
        public MmsiInfo() 
        { 
            brokenCount = 0;
            count = 0;
            brokenCountLog = new List<UInt16>();
            countLog = new List<UInt16>();
            status = "NotBroken";
        }
        public MmsiInfo(UInt32 ib, UInt32 c) 
        { 
            brokenCount = ib;
            count = c;
        }
        public MmsiInfo(UInt32 ib, UInt32 c, List<UInt16> bcl, List<UInt16> cl, string s) 
        { 
            brokenCount = ib;
            count = c;
            brokenCountLog = bcl;
            countLog = cl;
            status = s;
        }
    } 

    class BrokenShipManager
    {
        public string fileName = "brokenShip.txt";
        public List<UInt32> brokenShip = new List<UInt32>();
        public Dictionary<UInt32, MmsiInfo> mmsiDict = new Dictionary<UInt32, MmsiInfo>();
        public int countThres = 10;
        public Single brokenRateThres = (Single)0.5;
        public UInt16 dtThres= 30; //30 days

        public BrokenShipManager() 
        { 
        }
        public BrokenShipManager(int cThres, Single brThres) 
        {
            this.countThres = cThres;
            this.brokenRateThres = brThres;
        }
        public BrokenShipManager(string fileName) 
        { 
            this.fileName = fileName;
        }
        public void save(string fileName) 
        {
            using (StreamWriter sw = new StreamWriter(fileName)) 
            {
                sw.WriteLine("mmsi,brokenCount,count,brokenCountLog, countLog");
                string ss;
                foreach (KeyValuePair<UInt32, MmsiInfo> dicItem in mmsiDict)
                {
                    ss = "";
                    ss += dicItem.Key.ToString();
                    ss += ","+dicItem.Value.brokenCount.ToString();
                    ss += ","+listToString(dicItem.Value.brokenCountLog);
                    ss += ","+listToString(dicItem.Value.countLog);
                    ss += ","+dicItem.Value.status;
                    sw.WriteLine(ss);
                }
            }
            Console.WriteLine("Save {0}", fileName);
        }
        public void save() 
        {
            save(fileName);
        }
        public void load()
        {
            if (File.Exists(fileName))
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    bool readFile = true;
                    try { 
                        sr.ReadLine();
                    }catch(Exception ex) 
                    { 
                        readFile = false;
                        Console.WriteLine(ex.ToString());
                        Console.WriteLine("Cannot read brokenShip.txt!");
                    }

                    if (readFile)
                    {
                        bool error = false;
                        while (!error)
                        {
                            try
                            {
                                String str = sr.ReadLine();
                                string[] sa = str.Split(',');
                                sa = Array.ConvertAll(sa, ss => ss.Trim());

                                UInt32 mmsi = UInt32.Parse(sa[0]);
                                MmsiInfo mmsiInfo = new MmsiInfo(UInt32.Parse(sa[1]), UInt32.Parse(sa[2]), stringToList(sa[3]), stringToList(sa[4]), sa[5]);
                                mmsiDict[mmsi] = mmsiInfo;
                                int count = mmsiInfo.countLog.Count;
                                if (count > 0) 
                                {
                                    if (judge(mmsi, mmsiInfo.countLog[count-1]))
                                    {
                                        brokenShip.Add(mmsi);
                                    } 
                                }
                            }
                            catch (Exception ex) { error = true; }
                        }
                    }
                }
                save(fileName + ".bak");
            }
            else 
            {
                using (StreamWriter sw = new StreamWriter(fileName)) { }
            }
        }
        public void updateBrokenMmsi(UInt32 mmsi, UInt16 dtidx) 
        {
            mmsiDict[mmsi].brokenCount += 1;
            mmsiDict[mmsi].brokenCountLog.Add(dtidx);
            if (judge(mmsi, dtidx))
            {
                brokenShip.Add(mmsi);
                mmsiDict[mmsi].status = "Broken";
            }
            else 
            {
                if (brokenShip.Contains(mmsi)) brokenShip.Remove(mmsi);
            }
        }
        public void updateBrokenMmsi(List<UInt32> mmsiList, UInt16 dtidx) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                updateBrokenMmsi(mmsi, dtidx);
            }
        }
        public bool isContain(UInt32 mmsi) 
        {
            return mmsiDict.ContainsKey(mmsi);
        }
        public void update(UInt32 mmsi, UInt16 dtidx) {
            if (!mmsiDict.ContainsKey(mmsi))
            {
                MmsiInfo mmsiInfo = new MmsiInfo();
                mmsiDict[mmsi] = mmsiInfo;
            }

            mmsiDict[mmsi].count += 1;
            mmsiDict[mmsi].countLog.Add(dtidx);
            if (!judge(mmsi, dtidx))
            {
                if (brokenShip.Contains(mmsi)) brokenShip.Remove(mmsi);
                mmsiDict[mmsi].status = "NotBroken";
            }
        }
        public void update(List<UInt32> mmsiList, UInt16 dtidx) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                update(mmsi, dtidx);
            }
        }
        public void add(UInt32 mmsi) {
            if (!mmsiDict.ContainsKey(mmsi))
            {
                MmsiInfo mmsiInfo = new MmsiInfo();
                mmsiDict[mmsi] = mmsiInfo;
            }
        }
        public void add(List<UInt32> mmsiList) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                add(mmsi);
            }
        }
        public bool isContainInMmsi(UInt32 mmsi) 
        {
            return mmsiDict.ContainsKey(mmsi);
        }
        public bool judge(UInt32 mmsi, UInt16 dtidx) 
        {
            int count = mmsiDict[mmsi].countLog.Count(n => dtidx-n <= dtThres && dtidx-n>=0);
            if (count < this.countThres)
            {
                return false;
            }
            else
            {
                int brokenCount = mmsiDict[mmsi].brokenCountLog.Count(n => dtidx-n <= dtThres && dtidx-n>=0);
                return (Single)(brokenCount)/(Single)(count) > this.brokenRateThres;
            }
        }
        public string listToString(List<UInt16> list) 
        {
            string ss = "";
            int count = 0;
            foreach (var log in list) 
            { 
                ss += log.ToString();
                if (count+1!=list.Count()) { 
                    ss += " ";
                }
                count++;
            }
            return ss;
        }
        public List<UInt16> stringToList(string str) 
        {
            List<UInt16> list = new List<UInt16>();
            string[] sa = str.Split(' ');
            sa = Array.ConvertAll(sa, ss => ss.Trim());
            foreach (string s in sa) 
            {
                list.Add(Convert.ToUInt16(s));
            }
            return list;
        }
    }
}