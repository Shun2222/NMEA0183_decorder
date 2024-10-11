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
            // public readonly UInt32 mmsi;
            readonly int hash;
            public GridIdx(Single lat, Single lon, DateTime dt)
            {
                if (lat < -90) lat = -90; else if (lat > 90) lat = 90;
                while (lon < 20) lon += 360; while (lon >= 380) lon -= 360;  // 20<= lon < 380

                latIdx = (Int16)Math.Round(lat / DegPerCell);
                lonIdx = (UInt16)Math.Round(lon / DegPerCell);
                dtIdx = (UInt16)Math.Round((dt - epocDT).Ticks * 1.0 / TsPerCell.Ticks);
                // this.mmsi = mmsi;
                hash = latIdx.GetHashCode() ^ lonIdx.GetHashCode() ^ dtIdx.GetHashCode(); // ^ mmsi.GetHashCode()
            }
            public override int GetHashCode() { return hash; }
            public int CompareTo(GridIdx other)
            {
                //Math.Sign(mmsi.CompareTo(other.mmsi)) * 8 +
                return
                    Math.Sign(dtIdx.CompareTo(other.dtIdx)) * 4 +
                    Math.Sign(latIdx.CompareTo(other.latIdx)) * 2 +
                    Math.Sign(lonIdx.CompareTo(other.lonIdx));
            }
        }
    }

    class Program
    {
        const string nextExt = "ais3_2detail";

        class oneGridElem
        {
            public double hdg;
            public Single tanHdg, vogN, vogE;
            public Single getInsecY(Single X)
            {
                return (X-vogN) * tanHdg + vogE;
            }
            public Single getInsecX(Single Y)
            {
                return (Y-vogE) / tanHdg + vogN;
            }
            public Single getDistance(Single X, Single Y) 
            {
                return (float)Math.Abs(-X * tanHdg + Y - vogE + tanHdg * vogN) / (float)Math.Sqrt(tanHdg * tanHdg + 1);
            }
        }
        class XY
        {
            public Single x;
            public Single y;

            public XY(){}
            public XY(Single X, Single Y)
            {
                x = X;
                y = Y;
            }
        }
        double GetDistance(XY xy1, XY xy2)
        {
            double d2 = Math.Pow(xy2.x - xy1.x, 2) + Math.Pow(xy2.y - xy1.y, 2);
            return Math.Sqrt(d2);
        }
        // 参考：https://lets-csharp.com/traveling-salesman-dp/
        double GetSolution(XY[] xylist, ref int[] path)
        {
            int n = xylist.Length;

            // 1 << n　１ビット左にシフト 2^n
            // d[i, b]  (b は 集合S のビット列による表現)
            double[,] d = new double[n, 1 << n];
            int[,] before = new int[n, 1 << n]; // 経路を保存する

            // d の初期化
            for (int i = 0; i < n; i++)
            {
                for (int b = 0; b < 1 << n; b++)
                {
                    d[i, b] = double.MaxValue;
                    before[i, 1 << i] = 0;
                }
            }
            // 集合 S のサイズが 1 の場合（集合の要素はi番目の都市だけのとき）
            for (int i = 0; i < n; i++)
                d[i, 1 << i] = GetDistance(xylist[0], xylist[i]);// (都市 s から 都市 i への距離);

            // 集合のサイズを1つずつ大きくしていく
            for (int b = 0; b < 1 << n; b++)
            {
                for (int i = 0; i < n; i++)
                {
                    if ((b >> i & 1) == 0) // 集合Sのなかにi番目の都市は存在しない
                        continue;

                    for (int j = 0; j < n; j++)
                    {
                        if ((b >> j & 1) == 1)
                            continue;

                        // 都市 i から都市 j への距離を計算して配列内の値と比較して小さければ更新する
                        double tmp = d[i, b] + GetDistance(xylist[i], xylist[j]);
                        if (tmp < d[j, b | (1 << j)])
                        {
                            d[j, b | (1 << j)] = tmp;
                            before[j, b | (1 << j)] = i;
                        }
                    }
                }
            }

            int[] tour = new int[n];
            int pos = 0;
            int bit = (1 << n) - 1;
            for (int i = 0; i < n; i++)
            {
                tour[i] = before[pos, bit];
                bit = bit ^ (1 << pos);
                pos = tour[i];
            }
            // この時点で、tour は最小の巡回路長を達成する巡回路になっている
            path = tour;

            return d[0, (1 << n) - 1];
        }

    class grid
        {
            public Single A, B, C, D, E, F;
            public uint count;
            public List<oneGridElem> elems = new List<oneGridElem>();

            public void addElem(oneGridElem oge)
            {
                elems.Add(oge);
            }

            public List<XY> calcCur()
            {
                List<List<(XY, XY)>> insecs = calcInsecs();

                List<(XY, XY)> xyxy = new List<(XY, XY)>();
                XY xy = new XY(999, 999);
                xyxy.Add((xy, xy));

                for (int i = 0; i < insecs.Count; i++)
                {
                    for (int j = i+1; j < insecs.Count; j++)
                    {
                        if (i==j) continue;
                        if ((Math.Abs(xyxy[0].Item2.x) + Math.Abs(xyxy[0].Item2.y)) > (Math.Abs(insecs[i][j].Item2.x) + Math.Abs(insecs[i][j].Item2.y)))
                        {
                            //Console.WriteLine("clear, ({0}, {1})", insecs[i][j].Item1.x, insecs[i][j].Item1.y);
                            xyxy.Clear();
                            xyxy.Add(insecs[i][j]);
                        }else if ((Math.Abs(xyxy[0].Item2.x) + Math.Abs(xyxy[0].Item2.y)) == (Math.Abs(insecs[i][j].Item2.x) + Math.Abs(insecs[i][j].Item2.y)) && xyxy[0].Item2.x!=999)
                        {
                            //Console.WriteLine("count+");
                            xyxy.Add(insecs[i][j]);
                        }
                    }
                }
                Single meanX = 0, 
                       meanY = 0;
                for (int i = 0; i < xyxy.Count; i++)
                {
                    meanX += xyxy[i].Item1.x;
                    meanY += xyxy[i].Item1.y;
                    //Console.WriteLine("(xy, num xy) = (({0}, {1}), ({2}, {3}))", xyxy[i].Item1.x, xyxy[i].Item1.y, xyxy[i].Item2.x, xyxy[i].Item2.y);
                    //string input = Console.ReadLine();
                }
                meanX /= xyxy.Count;
                meanY /= xyxy.Count;
                XY meanXY = new XY(meanX, meanY);
                //Console.WriteLine(" count:{0}, mean cur:({1}, {2})\n", xyxy.Count, meanX, meanY);

                Single mind2 = 0;
                int minidx = -1;
                for (int i = 0; i < xyxy.Count; i++)
                {
                    Single d2 = 0;
                    for (int j = 0; j < elems.Count; j++)
                    {
                        Single dist = elems[j].getDistance(xyxy[i].Item1.x, xyxy[i].Item1.y);
                        //Console.WriteLine("\ndist: {0}", dist);
                        d2 += dist*dist;
                    }
                    if (minidx == -1)
                    {
                        mind2 = d2;
                        minidx = i;
                    }
                    if (mind2 > d2)
                    {
                        mind2 = d2;
                        minidx = i;
                    }
                    //Console.WriteLine("\nd2: {0}, idx:{1}", d2, i);
                }
                //Console.WriteLine("d2: {0}, minIdx:{1}\n", mind2, minidx);
                XY minXY = new XY(xyxy[minidx].Item1.x, xyxy[minidx].Item1.y);
                List<XY> res = new List<XY>();
                res.Add(meanXY);
                res.Add(minXY);
                return res;
            }

            public List<List<(XY, XY)>> calcInsecs()
            {
                List<List<(XY, XY)>> res = new List<List<(XY, XY)>>();
                for (int i = 0; i < elems.Count; i++)
                {
                    List<(XY, XY)> xyxy = new List<(XY, XY)>();
                    for (int j = 0; j < elems.Count; j++)
                    {
                        if (i == j)
                        {
                            XY defa = new XY(999, 999);
                            xyxy.Add((defa, defa));
                        }
                        else if (i > j)
                        {
                            xyxy.Add(res[j][i]);
                        }
                        else
                        {
                            XY xy = calcInsec(elems[i], elems[j]);

                            Single numX = 0,
                                   numY = 0;
                            if (Math.Pow(xy.x, 2) + Math.Pow(xy.y, 2) > 5*5)
                            {
                                numX = 999;
                                numY = 999;
                            }
                            else
                            {
                                for (int k = 0; k < elems.Count; k++)
                                {
                                    if (i == k || j == k) continue;
                                    if (elems[k].getInsecX(xy.y)-xy.x > 0)
                                    {
                                        numX += 1;
                                    }
                                    else if (elems[k].getInsecX(xy.y)-xy.x < 0)
                                    {
                                        numX -= 1;
                                    }

                                    if (elems[k].getInsecY(xy.x)-xy.y > 0)
                                    {
                                        numY += 1;
                                    }
                                    else if (elems[k].getInsecY(xy.x)-xy.y < 0)
                                    {
                                        numY -= 1;
                                    }
                                }
                            }

                            XY numXY = new XY();
                            numXY.x = numX;
                            numXY.y = numY;
                            xyxy.Add((xy, numXY));
                            //Console.WriteLine("\n xyxy: (({0}, {1}), ({2}, {3}))", xy.x, xy.y, numX, numY);
                            //string input = Console.ReadLine();
                        }
                    }
                    res.Add(xyxy);
                }
                return res;
            }

            private XY calcInsec(oneGridElem a1, oneGridElem a2)
            {
                double hdg1 = a1.hdg, 
                       hdg2 = a2.hdg;
                Single tanHdg1 = a1.tanHdg,
                       tanHdg2 = a2.tanHdg,
                       vogN1 = a1.vogN,
                       vogN2 = a2.vogN,
                       vogE1 = a1.vogE,
                       vogE2 = a2.vogE;

                XY xy = new XY();
                // if ((tanHdg1 - tanHdg2) < 0.00001 || (1 / tanHdg1 - 1 / tanHdg2) < 0.00001)
                if (tanHdg1==tanHdg2)
                {
                    //Console.WriteLine("Division by zero. Same data is compared.");
                    xy.x = 999;
                    xy.y = 999;
                }
                else
                {
                    Single x = ((vogN1 * tanHdg1 - vogN2 * tanHdg2) + (vogE2 - vogE1)) / (tanHdg1 - tanHdg2);
                    Single y = ((vogE1 / tanHdg1 - vogE2 / tanHdg2) + (vogN2 - vogN1)) / (1 / tanHdg1 - 1 / tanHdg2);

                    xy.x = x;
                    xy.y = y;
                }
                //Console.WriteLine("x:{0}, tanHdg1:{1}, tanHdg2:{2}, vN1:{3}, vN2:{4}, vE1:{5}, vE2:{6}", xy.x, tanHdg1, tanHdg2, vogN1, vogN2, vogE1, vogE2);
                //string input = Console.ReadLine();
                return xy;
            }

            /*public List<int> calcInsecNum()
            {
                List<XY> insecNum = new List<XY>();
                int numX, numY;
                for (int i = 0; i < insec.Count; i++)
                {
                    XY l1 = insec[i];
                    List<XY> res = new List<XY>();
                    numX = 0;
                    numY = 0;
                    for (int j = 0; j < insec.Count; j++)
                    {
                        if (i==j)
                        {
                            continue;min
                        }

                        XY l2 = insec[j];
                        if (l1.x>l2.x)
                        {
                            numX += 1;
                        }else if(l1.x<l2.x)
                        {
                            numX -= 1;
                        }
                        if (l1.y>l2.y)
                        {
                            numY += 1;
                        }else if(l1.y<l2.y)
                        {
                            numY -= 1;
                        }
                    }
                    XY xy = new XY();
                    xy.x = numX;
                    xy.y = numY;
                    insecNum.Add(xy);
                }
                return insecNum;
            }*/
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
                foreach (string inFile in inFiles)
                    using (StreamReader sr = new StreamReader(inFile))
                    {
                        int line = 0;
                        AIS aisr;
                        do
                        {
                            aisr = new AIS(sr.ReadLine(), swErr); line++;
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
                                aisr = new AIS(sr.ReadLine(), swErr); line++;
                                if (line % 10000 == 0) logout(string.Format("{0} lines loaded", line), false);
                            } while ((!aisr.Valid || aisr.Mmsi == mmsi) && line < maxLine);  //←修正

                            //AISListをフィルタリング
                            // 8kt以下は無効 、 COGとHDGの差が5度以上は無効
                            foreach (AIS a in AISList) if (a.SOG10 < 80 || differenceBetween2Angle(a.COG10 / 10.0, a.Hdg) >= 5) a.Valid = false;

                            // 3分以内に5度以上のCOG、HDG変化があればその間無効
                            TimeSpan Δt = new TimeSpan(0, 3, 0);
                            for (int i = 0; i < AISList.Count; i++)
                            {
                                // j←(iからΔt以内である最終のidx)+1
                                int j;
                                for (j = i + 1; j < AISList.Count; j++) if (AISList[j].LatLonDT.DT - AISList[i].LatLonDT.DT > Δt) break;//←修正
                                // j-1からiまでなぞりながらais[i]と角度が5度以上開いているkを見つけ、i～kを無効にする
                                for (int k = j - 1; k > i; k--)
                                    if (differenceBetween2Angle(AISList[i].Hdg, AISList[k].Hdg) > 5 ||
                                        differenceBetween2Angle(AISList[i].COG10 / 10.0, AISList[k].COG10 / 10.0) > 5)
                                    {
                                        for (int l = i; l <= k; l++) AISList[l].Valid = false;
                                        break;
                                    }
                            }
                            foreach (AIS ais in AISList) if (ais.Valid)
                                {
                                    //gridを作成
                                    // DefineMeshArea.GridIdx gi = new DefineMeshArea.GridIdx(ais.LatLonDT.Lat, ais.LatLonDT.Lon, ais.LatLonDT.DT, ais.Mmsi);
                                    DefineMeshArea.GridIdx gi = new DefineMeshArea.GridIdx(ais.LatLonDT.Lat, ais.LatLonDT.Lon, ais.LatLonDT.DT);
                                    if (!dicGrids.ContainsKey(gi)) dicGrids.Add(gi, new grid());
                                    grid g = dicGrids[gi];

                                    //行列式が0にならないよう、Hdgを左右にごくわずか振る
                                //    double hdg = ais.Hdg + 0.000001 * ((g.count % 3) - 1);
                                    double hdg = ais.Hdg;

                                    oneGridElem oge = new oneGridElem();
                                    oge.hdg = hdg;
                                    oge.tanHdg = (Single)Math.Tan(hdg * Math.PI / 180);
                                    oge.vogN = ais.Vog.North;
                                    oge.vogE = ais.Vog.East;

                                    g.addElem(oge);

                                    Single
                                        SinHdg = (Single)Math.Sin(hdg * Math.PI / 180),
                                        CosHdg = (Single)Math.Cos(hdg * Math.PI / 180),
                                        SinSin = SinHdg * SinHdg,
                                        CosCos = CosHdg * CosHdg,
                                        SinCos = SinHdg * CosHdg;
                                    g.A += SinSin;
                                    g.B -= SinCos;
                                    g.C += CosCos;
                                    g.D += (SinCos * ais.Vog.East - SinSin * ais.Vog.North);
                                    g.E += (SinCos * ais.Vog.North - CosCos * ais.Vog.East);
                                    g.F += (float)Math.Pow(ais.Vog.North * SinHdg - ais.Vog.East * CosHdg, 2);
                                    g.count++;
                                }
                            if (aisr == null) break;
                            mmsi = aisr.Mmsi;
                        } while (line < maxLine);
                    }
                using (StreamWriter sw = new StreamWriter(outFile))
                {
                    List<DefineMeshArea.GridIdx> tidxs = new List<DefineMeshArea.GridIdx>(dicGrids.Keys);
                    tidxs.Sort();
                    foreach (DefineMeshArea.GridIdx tidx in tidxs)
                    {
                        grid g = dicGrids[tidx];
                        List<XY> curs = g.calcCur();
                        XY cur = curs[0];
                        if (cur.x == 999) continue;

                        sw.WriteLine("dtidx:{0}, latidx:{1}, lonidx:{2}", tidx.dtIdx, tidx.latIdx, tidx.lonIdx);
                        for (int i = 0; i < g.elems.Count; i++) 
                        {
                            sw.WriteLine("elem{0}, Hdg:{1}, tanHdg:{2}, vogN:{3}, vogE:{4}", i, g.elems[i].hdg, g.elems[i].tanHdg, g.elems[i].vogN, g.elems[i].vogE);
                        }

                        List<List<(XY, XY)>> insecs = g.calcInsecs();
                        for (int i = 0; i < insecs.Count; i++)
                        {
                            for (int j = i + 1; j < insecs.Count; j++)
                            {
                                if (i==j) continue;
                                sw.WriteLine("{0}-{1}, insecX:{2}, insecY:{3}, numX:{4}, numY:{5}", i, j, insecs[i][j].Item1.x, insecs[i][j].Item1.y, insecs[i][j].Item2.x, insecs[i][j].Item2.y);
                            }
                        }
                        sw.WriteLine("Mean: curN:{0}, curE:{1}", cur.x, cur.y);
                        sw.WriteLine("Min: curN:{0}, curE:{1}", curs[1].x, curs[1].y);
                        sw.WriteLine("\n");
                    }
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