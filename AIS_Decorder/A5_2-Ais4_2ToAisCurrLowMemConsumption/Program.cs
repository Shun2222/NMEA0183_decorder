using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
    public const int
        LatIdxMax = 1800,
        LatIdxMin = 750,
        LonIdxMax = 5401,
        LonIdxMin = 4211;
    //public const int
    //    LatIdxMax = 1801,
    //    LatIdxMin = 719,
    //    LonIdxMax = 5401,
    //    LonIdxMin = 4211;
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
        LatRange = (int)(-HalfLat * Math.Log(Thres) / ln2 / degPerMesh),    //Thresに到達するメッシュインデックス差（緯度方向）
        LonRange = (int)(-HalfLon * Math.Log(Thres) / ln2 / degPerMesh),    //同 経度方向
        HrsRange = (int)(-HalfHrs * Math.Log(Thres) / ln2 / hrsPerMesh);    //同 時間方向
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
        Console.Write("tes1\r");
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
                        5];//A11,A12,A22,B1,B2

        // 緯度経度の有効無効の二次元マップを作成
        using (StreamReader sr = new StreamReader("cur.csv"))
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
        Console.Write("test2");


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
        Console.Write("test3");
    }

    public static void AddMap(int DtIdx, int LatIdx, int LonIdx, int item, float Value)
    {
        if (DtIdx < DtIdxMin || DtIdxMax < DtIdx) return;
        if (DtIdx < tmpDtIdxMin) { Program.logout(string.Format("Error DtIdx={0} < tmpDtIdxMin={1}", DtIdx, tmpDtIdxMin)); return; }
        while (tmpDtIdxMin + h <= DtIdx) MapToCsvAndClear(tmpDtIdxMin++);
        Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, item] += Value;
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
            swD = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "D.csv"),
            swSpd = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "Spd.csv"),
            swDeg = new StreamWriter(OutFolder + @"\AisCurr" + dtFormat + "Deg.csv"))
        {
            for (int LatIdx = LatIdxMax; LatIdx >= LatIdxMin; LatIdx--)
            {
                string sN = "", sE = "", sD = "", sSpd = "", sDeg = "";
                for (int LonIdx = LonIdxMin; LonIdx <= LonIdxMax; LonIdx++)
                {
                    float A11 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 0],
                          A12 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 1],
                          A22 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 2],
                          B1 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 3],
                          B2 = Map[DtIdx % h, LatIdx - LatIdxMin, LonIdx - LonIdxMin, 4],
                          D = A11 * A22 - A12 * A12;

                    if (D >= 1)
                    {
                        double
                            x = (A22 * B1 - A12 * B2) / D,
                            y = (-A12 * B1 + A11 * B2) / D,
                            Spd = Math.Sqrt(x * x + y * y),
                            Deg = Math.Atan2(y, x) / Math.PI * 180;
                        sN += string.Format("{0:0.00}", x);
                        sE += string.Format("{0:0.00}", y);
                        sD += string.Format("{0:0.00}", D);
                        sSpd += string.Format("{0:0.00}", Spd);
                        sDeg += string.Format("{0:0.00}", Deg);
                    }
                    sN += ",";
                    sE += ",";
                    sD += ",";
                    sSpd += ",";
                    sDeg += ",";
                }
                swN.WriteLine(sN.Substring(0, sN.Length - 1));
                swE.WriteLine(sE.Substring(0, sE.Length - 1));
                swD.WriteLine(sD.Substring(0, sD.Length - 1));
                swSpd.WriteLine(sSpd.Substring(0, sSpd.Length - 1));
                swDeg.WriteLine(sDeg.Substring(0, sDeg.Length - 1));
            }
        }
        //当該部分Clear
        for (int LatIdx = LatIdxMax; LatIdx >= LatIdxMin; LatIdx--)
            for (int LonIdx = LonIdxMin; LonIdx <= LonIdxMax; LonIdx++)
                for (int i = 0; i < 5; i++)
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
            outFolder = (_outFolder != null && _outFolder.Count > 0) ? _outFolder[0] : Path.GetDirectoryName(inFiles[0]);
            logFile = outFolder + @"\log.txt";
        }

        int dtMin = int.MaxValue, dtMax = int.MinValue; // 出力するための、入力時刻レンジ
        List<ais4> ais4s = new List<ais4>();
        logout("Start Reading");

        foreach (string inFile in inFiles) using (StreamReader sr = new StreamReader(inFile))
            {
                //1行目はそのまま
                sr.ReadLine();
                //2行目はヘッダで読み飛ばし
                sr.ReadLine();
                //3行目以降をaisCmpsにAdd
                int l = 0;
                while (!sr.EndOfStream)
                {
                    Console.Write("{0:0.00}%,{1}lines done\r", 100.0 * sr.BaseStream.Position / sr.BaseStream.Length, l++);
                    string[] ss = sr.ReadLine().Split(',');

                    //int Mmsi,int DtIdx,int LatIdx,int LonIdx,float ThetaDeg,float Flow,float LineCount

                    int MMSI = int.Parse(ss[0]),
                        DtIdx = int.Parse(ss[1]),
                        LatIdx = int.Parse(ss[2]),
                        LonIdx = int.Parse(ss[3]);

                    //if (DtIdx >= 32880) continue;  // 2014/10/01のデータのみ

                    float ThetaDeg = float.Parse(ss[4]),
                           F = float.Parse(ss[5]),
                           LineCount = float.Parse(ss[6]);
                    dtMin = Math.Min(dtMin, DtIdx);
                    dtMax = Math.Max(dtMax, DtIdx);

                    ais4s.Add(new ais4(MMSI, DtIdx, LatIdx, LonIdx, ThetaDeg, F, LineCount));
                }
            }
        logout("Sort");
        ais4Comparer ac = new ais4Comparer();
        ais4s.Sort(ac);

        logout("Start Map");

        Settei.init(dtMax, dtMin, outFolder);
        for (int i = 0; i < ais4s.Count; i++)
        {
            ais4 a = ais4s[i];
            Console.Write("{0:0.00}%,{1}lines done\r", 100.0 * i / ais4s.Count, i);

            float theta = a.ThetaDeg * (float)Math.PI / 180,
                sin = (float)Math.Sin(theta),
                cos = (float)Math.Cos(theta),
                coscos = cos * cos,
                sincos = sin * cos,
                sinsin = sin * sin,
                cosF = cos * a.Flow,
                sinF = sin * a.Flow;
            for (int dtidx = a.DtIdx - Settei.HrsRange; dtidx <= a.DtIdx + Settei.HrsRange; dtidx++)
                for (int latidx = a.LatIdx - Settei.LatRange; latidx <= a.LatIdx + Settei.LatRange; latidx++)
                    for (int lonidx = a.LonIdx - Settei.LonRange; lonidx <= a.LonIdx + Settei.LonRange; lonidx++)
                    {
                        if (latidx < Settei.LatIdxMin || Settei.LatIdxMax < latidx
                            || lonidx < Settei.LonIdxMin || Settei.LonIdxMax < lonidx
                            || dtidx < Settei.DtIdxMin || Settei.DtIdxMax < dtidx)
                            continue;
                        if (!Settei.IsLatLonValid(latidx, lonidx)) continue;

                        float w = Settei.weight(latidx - a.LatIdx, lonidx - a.LonIdx, dtidx - a.DtIdx) * a.LineCount;
                        Settei.AddMap(dtidx, latidx, lonidx, 0, w * coscos);
                        Settei.AddMap(dtidx, latidx, lonidx, 1, w * sincos);
                        Settei.AddMap(dtidx, latidx, lonidx, 2, w * sinsin);
                        Settei.AddMap(dtidx, latidx, lonidx, 3, w * cosF);
                        Settei.AddMap(dtidx, latidx, lonidx, 4, w * sinF);
                    }
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


