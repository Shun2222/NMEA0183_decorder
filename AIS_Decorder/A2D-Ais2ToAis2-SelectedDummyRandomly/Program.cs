//#define SatAIS        //衛星AISの時は有効にする

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using System.IO;
using System.Runtime.InteropServices;

namespace CurrentEstim
{


    class DefineMeshArea
    {
        //public const Single DegPerCell = 1F / 30;
        public const Single DegPerCell = 1F / 36;
        public static readonly TimeSpan TsPerCell = new TimeSpan(1, 0, 0);  //hr, min, sec
        public static readonly DateTime epocDT = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 時空座標、MMSIからインデックス化する。
        /// </summary>
        public struct GridIdx : IComparable<GridIdx>
        {
            public readonly Int16 latIdx;
            public readonly UInt16 lonIdx;
            public readonly UInt16 dtIdx;
            public readonly UInt32 mmsi;
            readonly int hash;
            public GridIdx(Single lat, Single lon, DateTime dt, UInt32 mmsi)
            {
                if (lat < -90) lat = -90; else if (lat > 90) lat = 90;
                while (lon < 20) lon += 360; while (lon >= 380) lon -= 360;  // 20<= lon < 380

                latIdx = (Int16)Math.Round(lat / DegPerCell);
                lonIdx = (UInt16)Math.Round(lon / DegPerCell);
                dtIdx = (UInt16)Math.Round((dt - epocDT).Ticks * 1.0 / TsPerCell.Ticks);
                this.mmsi = mmsi;
                hash = latIdx.GetHashCode() ^ lonIdx.GetHashCode() ^ dtIdx.GetHashCode() ^ mmsi.GetHashCode();
            }
            public override int GetHashCode() { return hash; }
            public int CompareTo(GridIdx other)
            {
                return
                    Math.Sign(mmsi.CompareTo(other.mmsi)) * 8 +
                    Math.Sign(dtIdx.CompareTo(other.dtIdx)) * 4 +
                    Math.Sign(latIdx.CompareTo(other.latIdx)) * 2 +
                    Math.Sign(lonIdx.CompareTo(other.lonIdx));
            }
        }
    }

    class Program
    {
        const string nextExt = "ais2dr";

        class grid
        {
            public Single A, B, C, D, E, F;
            public uint count;
        }
        static Dictionary<DefineMeshArea.GridIdx, grid> dicGrids = new Dictionary<DefineMeshArea.GridIdx, grid>();

        static string logFile;

        //List<string> 
        static void Main(string[] args)
        {
            List<string> inFiles;
            string outFile, errFile;
            int maxLine;
            {
                argparse ap = new argparse("ais2ファイルを読み込み、船速や回転などのフィルタをかけて時空間グリッド・MMSI別のA～Fの値を計算して" + nextExt + "ファイルに出力する。");
                ap.ResisterArgs(args);
                inFiles = ap.getArgs((char)0, "InFile", "入力するais2ファイル", kind: argparse.Kind.ExistFile, canBeMulti: true);
                List<string> _outFiles = ap.getArgs('o', "OutFile", "出力する" + nextExt + "ファイル（省略時は最初のInFileの拡張子を" + nextExt + "にしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                List<string> _errFiles = ap.getArgs('e', "ErrorFile", "エラー出力ファイル（省略時はOutFileの拡張子を" + nextExt + "errにしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                List<string> _logFiles = ap.getArgs('g', "LogFile", "ログ出力ファイル（省略時はOutFileの拡張子を" + nextExt + "logにしたもの）", canOmit: true);
                List<string> _Lines = ap.getArgs('l', "Lines", "読み込む最大行数（省略時は最後まで）", kind: argparse.Kind.IsInt, canOmit: true);

                if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; }

                outFile = (_outFiles.Count() > 0) ? _outFiles[0] : UnexistFilePath.ChangeExt(inFiles[0], nextExt);
                errFile = (_errFiles.Count() > 0) ? _errFiles[0] : UnexistFilePath.ChangeExt(outFile, nextExt + "err");
                logFile = (_logFiles.Count() > 0) ? _logFiles[0] : UnexistFilePath.ChangeExt(outFile, nextExt + "log");
                maxLine = (_Lines.Count() > 0) ? int.Parse(_Lines[0]) : int.MaxValue;
            }

            logout("Start Reading");

            using (StreamWriter swErr = new StreamWriter(errFile, true))
            {
                //AISリストを作る

                //ファイルの読み込み
                Dictionary<UInt32, int> mmsiCount = new Dictionary<UInt32, int>();
                List<string[]> strings = new List<string[]>();
                foreach (string inFile in inFiles)
                    using (StreamReader sr = new StreamReader(inFile))
                    {
                        int line = 0;
                        AIS aisr;
                        // Read lines
                        do
                        {
                            string aisx = sr.ReadLine();
                            aisr = new AIS(aisx, swErr); line++;
                            string[] sa = aisx.Split(',');
                            sa = Array.ConvertAll(sa, ss => ss.Trim());
                            strings.Add(sa);

                        } while (!aisr.Valid);

                        UInt32 mmsi = aisr.Mmsi;
                        do
                        {
                            List<AIS> AISList = new List<AIS>();

                            //単一MMSIについての時系列AISListを作成する
                            do
                            {
                                if (aisr.Valid) AISList.Add(aisr);
                                if (sr.EndOfStream)
                                {
                                    aisr = null;
                                    break;
                                }
                                string aisx = sr.ReadLine();
                                aisr = new AIS(aisx, swErr); line++;
                                string[] sa = aisx.Split(',');
                                sa = Array.ConvertAll(sa, ss => ss.Trim());
                                strings.Add(sa);

                                var saMmsi = UInt32.Parse(sa[2]);
                                if (line % 10000 == 0) logout(string.Format("{0} lines loaded", line), false);
                            } while ((!aisr.Valid || aisr.Mmsi == mmsi) && line < maxLine);  //←修正
                            if (aisr == null) break;
                            mmsi = aisr.Mmsi;
                        } while (line < maxLine);
                    }

                List<UInt32> mmsiHistory = new List<UInt32>();
                using (StreamWriter sw = new StreamWriter(outFile))
                {
                    UInt32 dummyMmsiNumber = 900000000;
                    Random rand = new Random();
                    List<string[]> dummyStrings = new List<string[]>();
                    UInt32 dummyCount = 0;
                    foreach(string[] sa in strings)
                    {
                        sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", sa[0], sa[1], sa[2], sa[3], sa[4], sa[5], sa[6], sa[7], sa[8], sa[9]);

                        // Create dummy
                        if (rand.NextDouble() < 0.05)  
                        {
                            string[] sa2 = new string[sa.Length] ;
                            Array.Copy(sa , sa2, sa.Length);
                            sa2[2] = dummyMmsiNumber.ToString();
                            sa2[8] = ((UInt16)(double.Parse(sa[6]) + 14)).ToString(); 
                            dummyStrings.Add(sa2);
                            dummyCount++;
                        }
                    }

                    foreach(string[] sa2 in dummyStrings)
                    {
                        sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", sa2[0], sa2[1], sa2[2], sa2[3], sa2[4], sa2[5], sa2[6], sa2[7], sa2[8], sa2[9]);
                    }
                    Console.WriteLine("dummy rate: {0}/{1}={2}", dummyCount, strings.Count, (double)dummyCount/(double)strings.Count);
                }
                logout("Finished");
            }
        }


        static DateTime startTime = DateTime.MaxValue;
        public static void logout(string message, bool linefeed = true)
        {
            DateTime now = DateTime.Now;
            if (startTime == DateTime.MaxValue) startTime = now;

            int secondFromStart = (int)(now - startTime).TotalSeconds;

            string s = string.Format("{0} , {1} , {2}", now.ToString("HH:mm:ss"), secondFromStart, message);
            Console.Write(s + (linefeed ? "\r\n" : "\r"));
            using (StreamWriter log = new StreamWriter(logFile, true))
            {
                log.WriteLine(s);
            }
        }
        public static double differenceBetween2Angle(double deg1, double deg2)
        {
            double deg = Math.Abs(deg1 - deg2);
            while (deg >= 360) deg -= 360;
            return Math.Min(deg, 360 - deg);
        }
    }
}