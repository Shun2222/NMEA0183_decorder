using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using flo = System.Single;

class cnst
{
    public const double thresWeight = 0.01;
    static readonly double
        Ln2 = Math.Log(2),              // exp(-Ln2*d)=(1/2)^d
        LnthW = Math.Log(thresWeight),
        keisha = 1.0 / 3.0;             // 太平洋側海岸線の傾斜
    public static readonly double
        thD =-LnthW/Ln2,              // d>thD ⇔ (1/2)^(d)<thresWeight
        unitvector_n = keisha / Math.Sqrt(1 + keisha * keisha),     //海岸線方向単位ベクトルの緯度方向成分
        unitvector_e = 1 / Math.Sqrt(1 + keisha * keisha);          //海岸線方向単位ベクトルの経度方向成分
    public static double 二分の一のx乗(double x) { return Math.Exp(-Ln2 * x); }
}

// DictionaryのkeyにできるLatIdxとLonIdxのセット
struct LatLonIdx
{
    //フィールド
    public int LatIdx,
               LonIdx;
    //コンストラクタ
    public LatLonIdx(int latIdx, int lonIdx)
    {
        LatIdx = latIdx;
        LonIdx = lonIdx;
    }
    public static LatLonIdx GetLatLonIdxFromDeg(flo Lat, flo Lon)
    {
        Lat = Lat % 360;
        Lon = ((Lon - 20) % 360) + 20;
        return new LatLonIdx((int)(Lat * 30), (int)(Lon * 30));
    }

    //プロパティ
    public int LatIdxFromZero { get { return LatIdx - Area.LatIdxMin; } }
    public int LonIdxFromZero { get { return LonIdx - Area.LonIdxMin; } }
    public byte AreaStatus { get { return Area.AreaStatus(this); } }

    //静的メソッド
    //解析対象矩形全体
    public static IEnumerable<LatLonIdx> InMap()
    {
        LatLonIdx llidx;
        foreach (int LatIdx in LatIdxs())
                foreach (int LonIdx in LonIdxs())
                    {
                        llidx.LatIdx = LatIdx;
                        llidx.LonIdx = LonIdx;
                        yield return llidx;
                    }
    }
    

    public static IEnumerable<int> LatIdxs() { return Enumerable.Range(Area.LatIdxMin, Area.LatIdxMax - Area.LatIdxMin + 1).Reverse(); }
    public static IEnumerable<int> LonIdxs() { return Enumerable.Range(Area.LonIdxMin, Area.LonIdxMax - Area.LonIdxMin + 1); }
    public LatLonIdx  GetAddedLLIdx(int deltaLat,int deltaLon)    { return new LatLonIdx(LatIdx + deltaLat, LonIdx + deltaLon); }
}

//空間的重み付けを定義する
//まずSetWeightで半減距離（単位はメッシュ幅）を入力して初期設定する
//WeightでLatIdxの差、LonIdxの差を入力すると重みが返る
//Idxの差は絶対値にしないこと（重みは線対称ではないため）
class SpatialWeight
{


    static flo[,] weight;
    static int LatRange,LonRange;
    public static List<Tuple<int, int>> LLIdxList = new List<Tuple<int, int>>();
    public static void SetWeight(flo HalfMeshNorth, flo HalfMeshEast, bool RenewLLIdxList)
    {
        if (RenewLLIdxList)
        {
            LatRange = (int)(cnst.thD * HalfMeshNorth);
            LonRange = (int)(cnst.thD * HalfMeshEast);
            weight = new flo[LatRange * 2 + 1, LonRange * 2 + 1];
            LLIdxList.Clear();
            for (int n = -LatRange; n <= LatRange; n++) for (int e = -LonRange; e <= LonRange; e++)
                {
                    double nDist = (n *cnst.unitvector_e - e * cnst.unitvector_n) / HalfMeshNorth,
                           eDist = (n * cnst.unitvector_n + e * cnst.unitvector_e) / HalfMeshEast,
                           dist = Math.Sqrt(nDist * nDist + eDist * eDist);
                    flo w =(flo) cnst.二分の一のx乗(dist);
                    if (w > 0.1)
                    {
                        weight[n + LatRange, e + LonRange] = w;
                        LLIdxList.Add(new Tuple<int, int>(n, e));
                    }
                    else weight[n + LatRange, e + LonRange] = 0;
                }
        }
        else
        {
            foreach (Tuple<int, int> p in LLIdxList)
            {
                int n = p.Item1, e = p.Item2;
                double nDist = (n * cnst.unitvector_e - e* cnst.unitvector_n) / HalfMeshNorth,
                       eDist = (n * cnst.unitvector_n + e * cnst.unitvector_e) / HalfMeshEast,
                       dist = Math.Sqrt(nDist * nDist + eDist * eDist);
                flo w =(flo) cnst.二分の一のx乗(dist);

                weight[n + LatRange, e + LonRange] = w;
            }
        }
    }
    public static flo Weight(Tuple<int, int> DeltaLatLonIdx)
    {
        int n = DeltaLatLonIdx.Item1, e = DeltaLatLonIdx.Item2;
        if (n< -LatRange || LatRange <n || e < -LonRange || LonRange < e) return 0;
        return weight[n + LatRange, e + LonRange];
    }
}

class Time
{
    //単位変換
    static readonly DateTime epocDT = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public const flo HoursPerMesh = 1;
    public static int DtIdx(DateTime dt)    {        return (int)(Math.Round((dt - epocDT).TotalHours / HoursPerMesh));     }
    public static DateTime DT(int DtIdx)    {        return epocDT.AddHours(DtIdx * HoursPerMesh);    }
    //Weight
    static flo halfDT;
    static bool useFuture;
    public static IEnumerable<int> DtIdxList;
    public static void SetWeight(flo HalfDT, bool UseFuture, bool RenewDtIdxList)
    {
        halfDT = HalfDT; useFuture = UseFuture;
        if (RenewDtIdxList)
        {
            int Lowest = (int)(-cnst.thD * halfDT);
            int Highest = UseFuture ? (int)(cnst.thD * halfDT) : 0;
            DtIdxList = Enumerable.Range(Lowest, Highest - Lowest + 1);
        }
    }
    public static flo Weight(int deltaDtIdx)
    {        return (flo) cnst.二分の一のx乗(Math.Abs(deltaDtIdx /halfDT));    }    
}

class Area
{
    //解析対象とするエリア。JAM海流と同じ矩形
    public const int
        LatIdxMax = 1500,
        LatIdxMin = 600,
        LonIdxMax = 4500,
        LonIdxMin = 3510;

    //太平洋沿岸エリア (32,130)～(30,130)～(34, 142)～(36, 142)に入っているかどうか
    public static bool IsRoi(LatLonIdx LLidx) { return IsRoi(LLidx.LatIdx, LLidx.LonIdx); }
    public static bool IsRoi(int LatIdx, int LonIdx)
    {
        if (LonIdx < 130 * 30 || 142 * 30 < LonIdx) return false;
        if (LonIdx < 3 * LatIdx + 34 * 30 || 3 * LatIdx + 40 * 30 < LonIdx) return false;
        return true;
    }

    //海と陸地の区別を記憶する配列。nullは未初期化
    static byte[,] areaStatus = null;

    //LatIdx,LonIdxで表す座標がなんであるかを返す。以下のOR値を返す。
    // 0x01:解析対象矩形内
    // 0x02:海
    // 0x04:Roi
    public static byte AreaStatus(LatLonIdx LLidx)
    {
        if (areaStatus == null)  //初期化していなかったら初期化する
        {
            areaStatus = new byte[LatIdxMax - LatIdxMin + 1, LonIdxMax - LonIdxMin + 1];
            
            using (StreamReader sr = new StreamReader("cur.csv"))
            {
                foreach (int LatIdx in LatLonIdx.LatIdxs())
                {
                    string[] ss = sr.ReadLine().Split(',');
                    foreach (int LonIdx in LatLonIdx.LonIdxs())
                    {
                        LatLonIdx llidx = new LatLonIdx(LatIdx, LonIdx);
                        byte _areaStatus = 1;
                        if (ss[llidx.LonIdxFromZero].Trim() != "") _areaStatus |= 2;
                        if (IsRoi(llidx)) _areaStatus |= 4;
                        areaStatus[llidx.LatIdxFromZero,llidx.LonIdxFromZero] = _areaStatus;
                    }
                }
            }
        }
        if (LLidx.LatIdx < LatIdxMin || LatIdxMax < LLidx.LatIdx || LLidx.LonIdx < LonIdxMin || LonIdxMax < LLidx.LonIdx)
            return 0;
        else return areaStatus[LLidx.LatIdxFromZero, LLidx.LonIdxFromZero];
    }
}

