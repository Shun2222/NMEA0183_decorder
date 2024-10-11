using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

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
    class BlackListManager
    {
        public string fileName = "blackList.txt";
        public List<UInt32> blackList = new List<UInt32>();
        public List<UInt32> allMmsiList = new List<UInt32>();

        public BlackListManager() 
        { 
        }
        public BlackListManager(string fileName) 
        { 
            this.fileName = fileName;
        }
        public void save() 
        {
            using (StreamWriter sw = new StreamWriter(fileName)) 
            {
                string ss = "";
                foreach (UInt32 blackMmsi in blackList) 
                {
                    ss += blackMmsi.ToString() + ","; 
                }
                sw.WriteLine(ss);

                ss = "";
                foreach (UInt32 mmsi in allMmsiList) 
                { 
                    ss += mmsi.ToString() + ","; 
                }
                sw.WriteLine(ss);
            }
        }
        public void load()
        {
            if (File.Exists(fileName))
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    try
                    {
                        String blackListString = sr.ReadLine();
                        string[] sa = blackListString.Split(',');
                        sa = Array.ConvertAll(sa, ss => ss.Trim());
                        foreach (String blackMmsi in sa)
                        {
                            blackList.Add(UInt32.Parse(blackMmsi));
                        }

                        String allMmsiListString = sr.ReadLine();
                        sa = allMmsiListString.Split(',');
                        sa = Array.ConvertAll(sa, ss => ss.Trim());
                        foreach (String mmsi in sa)
                        {
                            allMmsiList.Add(UInt32.Parse(mmsi));
                        }
                    }catch(Exception ex) 
                    { 
                        Console.WriteLine(ex.ToString());
                        Console.WriteLine("Cannot read blacklist.txt!");
                    }
                }
            }
            else 
            {
                using (StreamWriter sw = new StreamWriter(fileName)) { }
            }
        }
        public void update(UInt32 mmsi) 
        { 
            if (!blackList.Contains(mmsi)) 
            { 
                blackList.Add(mmsi);
            }
        }
        public void update(List<UInt32> mmsiList) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                if (!blackList.Contains(mmsi)) 
                { 
                    blackList.Add(mmsi); 
                }
            }
        }
        public bool isContain(UInt32 mmsi) 
        {
            return blackList.Contains(mmsi);
        }
        public void updateAllMmsiList(UInt32 mmsi) 
        {
            if (!allMmsiList.Contains(mmsi))
            {
                allMmsiList.Add(mmsi);
            }
        }
        public void updateAllMmsiList(List<UInt32> mmsiList) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                if (!allMmsiList.Contains(mmsi)) 
                { 
                    allMmsiList.Add(mmsi); 
                }
            }
        }
        public bool isContainInAllMmsi(UInt32 mmsi) 
        {
            return allMmsiList.Contains(mmsi);
        }
    }

    class MmsiInfo 
    { 
        public UInt32 isBroken;
        public UInt32 count;
        public MmsiInfo() 
        { 
            isBroken = 0;
            count = 1;
        }
        public MmsiInfo(UInt32 ib, UInt32 c) 
        { 
            isBroken = ib;
            count = c;
        }
    } 

    class BlackListManager2
    {
        public string fileName = "blackList.txt";
        public List<UInt32> blackList = new List<UInt32>();
        public Dictionary<UInt32, MmsiInfo> mmsiDict = new Dictionary<UInt32, MmsiInfo>();

        public BlackListManager2() 
        { 
        }
        public BlackListManager2(string fileName) 
        { 
            this.fileName = fileName;
        }
        public void save(string fileName) 
        {
            using (StreamWriter sw = new StreamWriter(fileName)) 
            {
                sw.WriteLine("mmsi,isBroken,count");
                string ss;
                foreach (KeyValuePair<UInt32, MmsiInfo> dicItem in mmsiDict)
                {
                    ss = "";
                    ss += dicItem.Key.ToString();
                    ss += ","+dicItem.Value.isBroken.ToString();
                    ss += ","+dicItem.Value.count.ToString();
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
                        Console.WriteLine("Cannot read blacklist.txt!");
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
                                MmsiInfo mmsiInfo = new MmsiInfo(UInt32.Parse(sa[1]), UInt32.Parse(sa[2]));
                                mmsiDict[mmsi] = mmsiInfo;
                                if (mmsiInfo.isBroken == 1)
                                {
                                    blackList.Add(mmsi);
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
        public void updateBrokenMmsi(UInt32 mmsi) 
        {
            mmsiDict[mmsi].isBroken = 1;
            if (!blackList.Contains(mmsi)) 
            { 
                blackList.Add (mmsi);
            }
        }
        public void updateBrokenMmsi(List<UInt32> mmsiList) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                updateBrokenMmsi(mmsi);
            }
        }
        public bool isContain(UInt32 mmsi) 
        {
            return mmsiDict.ContainsKey(mmsi);
        }
        public void update(UInt32 mmsi) 
        {
            if (!mmsiDict.ContainsKey(mmsi))
            {
                MmsiInfo mmsiInfo = new MmsiInfo();
                mmsiDict[mmsi] = mmsiInfo;
            }
            else
            {
                mmsiDict[mmsi].count += 1;
            }
        }
        public void update(List<UInt32> mmsiList) 
        { 
            foreach(UInt32 mmsi in mmsiList) 
            {
                update(mmsi);
            }
        }
        public bool isContainInMmsi(UInt32 mmsi) 
        {
            return mmsiDict.ContainsKey(mmsi);
        }
    }
}