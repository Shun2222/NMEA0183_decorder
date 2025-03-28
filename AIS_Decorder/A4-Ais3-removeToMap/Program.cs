using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;

// Mapの時間方向の幅方向はh=HrsRange*2+1のレイヤーを持つ。
// DtIdxのデータはDtIdx mod hのレイヤーで記憶する
//
// init()でMap配列を確保 
//    DtIdxMin,DtIdxMaxを定義←これは終了まで不変
//    tmpDtIdxMin = DtIdxMin
// AddMap(DtIdx,…)では、
//    DtIdxMin ～ DtIdxMaxの範囲外のDtIdxへのアクセスは無視
//    tmpDtIdxMin より小さいDtIdxへのアクセスはエラー
//    tmpDtIdxMin + h 以上へのアクセスがある場合は
//        tmpDtIdxMinを記憶するレイヤーをcsv出力
//        tmpDtIdxMin++ して2行上に戻る
//    DtIdx mod hのレイヤーに書き込み
// Close()では、tmpDtIdxMinからDtIdxMaxまでをcsv出力
class Settei
{
    public const int PoolSize = 3; // PoolSizeの変更で設定しなおす変数：LatIdxMax, LatIdxMin, LonIdxMax, LonIdxMin, outFolder, nan_map_pooled{pool size}.csv
    public const int
        LatIdxMax = 600,//3: 600, 1: 1800, 6:300
        LatIdxMin = 251,//3: 251, 1: 719, 6:126
        LonIdxMax = 1800,//3: 1800, 1: 5401, 6:899
        LonIdxMin = 1404;//3: 1404, 1: 4211, 6:702;
    public static int
        DtIdxMax,
        DtIdxMin;
    static int tmpDtIdxMin;
    static string OutFolder;

    const double
        HalfLat = 0.1,    // 半減距離緯度方向deg
        HalfLon = 0.2,    // 半減距離経度方向deg
        HalfHrs = 0.1;     // 半減距離hrs 12.0 // 時間方向を使わない

    const double ln2 = 0.6931471805599453094172321;//2の自然対数
    static double sq(double x) { return x * x; }

    const double Thres = 0.05;//これ以下の重みになるような距離は無視
    const double degPerMesh = 1.0 / 36; //メッシュあたり緯経度
    const double hrsPerMesh = 1.0;      //メッシュあたり時間

    public static readonly int
        LatRange = (int)(0),    //Thresに到達するメッシュインデックス差（緯度方向）
        LonRange = (int)(0),    //同 経度方向
        HrsRange = (int)(0);    //同 時間方向
    static readonly int h = HrsRange * 2 + 1;

    public static float weight(int DLat, int DLon, int DTime)
    {
        DLat = Math.Abs(DLat); DLon = Math.Abs(DLon); DTime = Math.Abs(DTime);
        if (DLat > LatRange || DLon > LonRange || DTime > HrsRange) return 0;
        return weights[DTime, DLat, DLon];
    }
    static float[,,] weights = new float[HrsRange + 1, LatRange + 1, LonRange + 1];

    public static float[,,,] Map;
    static bool[,] LatLonIsValid = new bool[Settei.LatIdxMax - Settei.LatIdxMin + 1,
                                                   Settei.LonIdxMax - Settei.LonIdxMin + 1];
    public static void init(int dtIdxMax, int dtIdxMin, string outFolder)
    {
        // MapのDtIdxの範囲を定義
        DtIdxMax = dtIdxMax;
        DtIdxMin = dtIdxMin;
        tmpDtIdxMin = DtIdxMin;

        // 出力先フォルダを定義
        OutFolder = outFolder;

        // Map行列を作成
        Map = new float[h,
                        Settei.LatIdxMax - Settei.LatIdxMin + 1,
                        Settei.LonIdxMax - Settei.LonIdxMin + 1,
                        9];//A11,A12,A22,B1,B2

        // 緯度経度の有効無効の二次元マップを作成
        using (StreamReader sr = new StreamReader("nan_map_pooled3.csv"))
        {
            for (int i = Settei.LatIdxMax - Settei.LatIdxMin; i >= 0; i--)
            {
                string[] ss = sr.ReadLine().Split(',');
                //Console.Write("{0}\r", Settei.LonIdxMax - Settei.LonIdxMin);
                for (int j = 0; j <= Settei.LonIdxMax - Settei.LonIdxMin; j++)
                {
                    // if (j> Settei.LonIdxMax - Settei.LonIdxMin)
                    // Console.Write(j);
                    // Console.Write("\r");
                    LatLonIsValid[i, j] = (ss[j].Trim() != "");
                }
            }
        }


        // 重み係数の三次元行列を作成
        for (int hr = 0; hr <= HrsRange; hr++)
        {
            for (int lat = 0; lat <= LatRange; lat++) 
            { 
                for (int lon = 0; lon <= LonRange; lon++) 
                {
                    weights[hr, lat, lon] = (float)Math.Exp(-ln2 * Math.Sqrt // =0.5 ^ √…
                       (sq(lat * degPerMesh / HalfLat)
                      + sq(lon * degPerMesh / HalfLon)
                      + sq(hr * hrsPerMesh / HalfHrs)));
                }
            }
        }
    }

    public static void AddMap(int DtIdx, int LatIdx, int LonIdx, int item, float Value)
    {
        if (DtIdx < DtIdxMin || DtIdxMax < DtIdx) return;
        if (DtIdx < tmpDtIdxMin) { Program.logout(string.Format("Error DtIdx={0} < tmpDtIdxMin={1}", DtIdx, tmpDtIdxMin)); return; }
        while (tmpDtIdxMin + h <= DtIdx) MapToCsvAndClear(tmpDtIdxMin++);
        Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, item] += Value;
    }
    public static void SetMap(int DtIdx, int LatIdx, int LonIdx, int item, float Value)
    {
        if (DtIdx < DtIdxMin || DtIdxMax < DtIdx) return;
        if (DtIdx < tmpDtIdxMin) { Program.logout(string.Format("Error DtIdx={0} < tmpDtIdxMin={1}", DtIdx, tmpDtIdxMin)); return; }
        while (tmpDtIdxMin + h <= DtIdx) MapToCsvAndClear(tmpDtIdxMin++);
        Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, item] = Value;
    }
    public static void Close()
    {
        for (int DtIdx = tmpDtIdxMin; DtIdx <= DtIdxMax; DtIdx++) MapToCsvAndClear(DtIdx);
    }

    static void MapToCsvAndClear(int DtIdx)
    {
        string dtFormat = string.Format("{0:yyyyMMddHH}", new DateTime(2011, 1, 1).AddHours(DtIdx));
        Program.logout("Output :" + dtFormat);
        using (StreamWriter swN = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "N.csv"),
            swE = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "E.csv"),
            swCur1 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "cur1.csv"),
            swCur2 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "cur2.csv"),
            swLambda1 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "lambda1.csv"),
            swLambda2 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "lambda2.csv"),
            swPsi1 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "psi1.csv"),
            swPsi2 = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "psi2.csv")) 
        {
            for (int LatIdx = LatIdxMax; LatIdx >= LatIdxMin; LatIdx--)
            {
                string sN = "", sE = "", sC1 = "", sC2 = "", sL1 = "", sL2 = "", sP1 = "", sP2 = "";
                for (int LonIdx = LonIdxMin; LonIdx <= LonIdxMax; LonIdx++)
                {
                    float n = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 0],
                          e = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 1],
                          c1 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 2],
                          c2 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 3],
                          l1 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 4],
                          l2 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 5],
                          p1 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 6],
                          p2 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 7],
                          num = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 8];

                    if (num > 0)
                    {
                        sN += string.Format("{0:0.00}", n);
                        sE += string.Format("{0:0.00}", e);
                        sC1 += string.Format("{0:0.00}", c1);
                        sC2 += string.Format("{0:0.00}", c2);
                        sL1 += string.Format("{0:0.00}", l1);
                        sL2 += string.Format("{0:0.00}", l2);
                        sP1 += string.Format("{0:0.00}", p1);
                        sP2 += string.Format("{0:0.00}", p2);
                    }
                    else 
                    { 
                        sN += string.Format("");
                        sE += string.Format("");
                        sC1 += string.Format("");
                        sC2 += string.Format("");
                        sL1 += string.Format("");
                        sL2 += string.Format("");
                        sP1 += string.Format("");
                        sP2 += string.Format("");
                    }

                    sN += ",";
                    sE += ",";
                    sC1 += ",";
                    sC2 += ",";
                    sL1 += ",";
                    sL2 += ",";
                    sP1 += ",";
                    sP2 += ",";
                }
                swN.WriteLine(sN.Substring(0, sN.Length - 1));
                swE.WriteLine(sE.Substring(0, sE.Length - 1));
                swCur1.WriteLine(sC1.Substring(0, sC1.Length - 1));
                swCur2.WriteLine(sC2.Substring(0, sC2.Length - 1));
                swLambda1.WriteLine(sL1.Substring(0, sL1.Length - 1));
                swLambda2.WriteLine(sL2.Substring(0, sL2.Length - 1));
                swPsi1.WriteLine(sP1.Substring(0, sP1.Length - 1));
                swPsi2.WriteLine(sP2.Substring(0, sP2.Length - 1));
            }
        }
        //当該部分Clear
        for (int LatIdx = LatIdxMax; LatIdx >= LatIdxMin; LatIdx--)
            for (int LonIdx = LonIdxMin; LonIdx <= LonIdxMax; LonIdx++)
                for (int i = 0; i < 9; i++)
                    Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, i] = 0;
    }
    public static bool IsLatLonValid(int LatIdx, int LonIdx)
    { return LatLonIsValid[LatIdx - LatIdxMin, LonIdx - LonIdxMin]; }
}

class ais4
{
    public int MMSI, DtIdx, LatIdx, LonIdx;
    public float ThetaDeg, Flow, LineCount;
    public ais4(int MMSI, int DtIdx, int LatIdx, int LonIdx, float ThetaDeg, float Flow, float LineCount)
    {
        this.MMSI = MMSI;
        this.DtIdx = DtIdx;
        this.LatIdx = LatIdx;
        this.LonIdx = LonIdx;
        this.ThetaDeg = ThetaDeg;
        this.Flow = Flow;
        this.LineCount = LineCount;
    }
}
class ais4Comparer : IComparer<ais4>
{
    public int Compare(ais4 x, ais4 y) //Dt, Lat, Lon, MMSIの昇順
    {
        if (x == null) return (y == null) ? 0 : -1;
        else if (y == null) return 1;

        int r;
        if ((r = x.DtIdx.CompareTo(y.DtIdx)) != 0) return r;
        if ((r = x.LatIdx.CompareTo(y.LatIdx)) != 0) return r;
        if ((r = x.LonIdx.CompareTo(y.LonIdx)) != 0) return r;
        return x.MMSI.CompareTo(y.MMSI);
    }
}
class ais3
{
    public int DtIdx, LatIdx, LonIdx;
    public float curN, curE, cur1, cur2, lambda1, lambda2, psi1, psi2;
    public ais3(int DtIdx, int LatIdx, int LonIdx, float n, float e, float c1, float c2, float l1, float l2, float p1, float p2)
    {
        this.DtIdx = DtIdx;
        this.LatIdx = LatIdx;
        this.LonIdx = LonIdx;
        this.curN = n;
        this.curE = e;
        this.cur1 = c1;
        this.cur2 = c2;
        this.lambda1 = l1;
        this.lambda2 = l2;
        this.psi1 = p1;
        this.psi2 = p2;
    }
}
class ais3Comparer : IComparer<ais3>
{
    public int Compare(ais3 x, ais3 y) //Dt, Lat, Lon, MMSIの昇順
    {
        if (x == null) return (y == null) ? 0 : -1;
        else if (y == null) return 1;

        int r;
        if ((r = x.DtIdx.CompareTo(y.DtIdx)) != 0) return r;
        if ((r = x.LatIdx.CompareTo(y.LatIdx)) != 0) return r;
        if ((r = x.LonIdx.CompareTo(y.LonIdx)) != 0) return r;
        return -1;
    }
}

class Program
{
    const string inExt = "ais4", outExt = "csv";
    static string logFile;

    static void Main(string[] args)
    {

        List<string> inFiles;
        string outFolder;

        //とりあえず、inFileはひとつにしている。複数あると後処理で同一グリッド・MMSIの複数のレコードがありえることを注意すること
        {
            argparse ap = new argparse(inExt + "ファイルを読み込み、AISのみからなる海流の値を計算して" + outExt + "ファイルを出力する");
            ap.ResisterArgs(args);
            inFiles = ap.getArgs((char)0, "InFile", "入力する" + inExt + "ファイル", kind: argparse.Kind.ExistFile);
            List<string> _outFolder = ap.getArgs('o', "OutFolder", "出力する" + outExt + "ファイルを保存するフォルダ", kind: argparse.Kind.ExistFolder, quantity: argparse.Quantity.Null_Or_1);

            if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; }
            //outFolder = (_outFolder != null && _outFolder.Count > 0) ? _outFolder[0] : Path.GetDirectoryName(inFiles[0]);
            outFolder = (_outFolder != null && _outFolder.Count > 0) ? _outFolder[0] : "../../data/ais/ais_removedBroken_pool3_dummy";
            logFile = outFolder + @"\log.txt";
        }

        int dtMin = int.MaxValue, dtMax = int.MinValue; // 出力するための、入力時刻レンジ
        List<ais3> ais3s = new List<ais3>();
        logout("Start Reading");

        foreach (string inFile in inFiles) using (StreamReader sr = new StreamReader(inFile))
        {
                //1行目はヘッダで読み飛ばし
                sr.ReadLine();
                //3行目以降をaisCmpsにAdd
                int l = 0;
                while (!sr.EndOfStream)
                {
                    Console.Write("{0:0.00}%,{1}lines done\r", 100.0 * sr.BaseStream.Position / sr.BaseStream.Length, l++);
                    string[] ss = sr.ReadLine().Split(',');

                    //int Mmsi,int DtIdx,int LatIdx,int LonIdx,float ThetaDeg,float Flow,float LineCount

                    int DtIdx = int.Parse(ss[0]),
                        LatIdx = int.Parse(ss[1])/Settei.PoolSize,
                        LonIdx = int.Parse(ss[2])/Settei.PoolSize;

                    //if (DtIdx >= 32880) continue;  // 2014/10/01のデータのみ

                    float curN = float.Parse(ss[3]),
                          curE = float.Parse(ss[4]),
                          curLambda1 = float.Parse(ss[5]),
                          curLambda2 = float.Parse(ss[6]),
                          lambda1 = float.Parse(ss[7]),
                          lambda2 = float.Parse(ss[8]),
                          psi1 = float.Parse(ss[9]),
                          psi2 = float.Parse(ss[10]);

                    dtMin = Math.Min(dtMin, DtIdx);
                    dtMax = Math.Max(dtMax, DtIdx);

                    ais3s.Add(new ais3(DtIdx, LatIdx, LonIdx, curN, curE, curLambda1, curLambda2, lambda1, lambda2, psi1, psi2));
                }


        }
        Console.WriteLine("dtMin:{0}, dtMax:{1}", dtMin, dtMax);

        ais3Comparer ac = new ais3Comparer();
        ais3s.Sort(ac);

        Settei.init(dtMax, dtMin, outFolder);

        for (int i = 0; i < ais3s.Count; i++) {
            int DtIdx = ais3s[i].DtIdx,
                LatIdx = ais3s[i].LatIdx,
                LonIdx = ais3s[i].LonIdx;
            float curN = ais3s[i].curN,
                  curE = ais3s[i].curE,
                  cur1 = ais3s[i].cur1,
                  cur2 = ais3s[i].cur2,
                  lambda1 = ais3s[i].lambda1,
                  lambda2 = ais3s[i].lambda2,
                  psi1 = ais3s[i].psi1,
                  psi2 = ais3s[i].psi2;

            if (LatIdx < Settei.LatIdxMin || LatIdx > Settei.LatIdxMax || LonIdx < Settei.LonIdxMin || LonIdx > Settei.LonIdxMax) 
            { 
                Console.WriteLine("Out of range! LatIdx:{0}, LonIdx:{1}", LatIdx, LonIdx);
                continue;
            }
            if (!Settei.IsLatLonValid(LatIdx, LonIdx))
            {
                continue;
            }

            Settei.SetMap(DtIdx, LatIdx, LonIdx, 0, curN);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 1, curE);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 2, cur1);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 3, cur2);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 4, lambda1);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 5, lambda2);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 6, psi1);
            Settei.SetMap(DtIdx, LatIdx, LonIdx, 7, psi2);
            Settei.AddMap(DtIdx, LatIdx, LonIdx, 8, 1);
        }

        logout("Closing");
        Settei.Close();

        logout("Finished");
    }


    static DateTime startTime = DateTime.MaxValue;
    public static void logout(string message, bool linefeed = true)
    {
        DateTime now = DateTime.Now;
        if (startTime == DateTime.MaxValue) startTime = now;

        int secondFromStart = (int)(now - startTime).TotalSeconds;

        string s = string.Format("{0} , {1} , {2}", now.ToString("HH:mm:ss"), secondFromStart, message);
        Console.Write(s + (linefeed ? "\r\n" : "\r"));
        using (StreamWriter sw = new StreamWriter(logFile, append: true)) sw.WriteLine(s);
    }
}


