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
//using Combination;
using System.Threading;
using System.Security.Cryptography.X509Certificates;

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
        const string nextExt = "ais3_2";

        class oneGridElem
        {
            public double hdg;
            public Single tanHdg, vogN, vogE;
            public UInt32 mmsi;
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
        class VEC
        {
            public double norm;
            public double direction;
            public double lambda;

            public VEC(){}
            public VEC(double X, double Y, double Z) 
            {
                norm = X;
                direction = Y;
                lambda = Z;
            }
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
            public List<UInt32> getMmsi()
            {
                List<UInt32> mmsis = new List<UInt32>();
                foreach (oneGridElem elem in elems) 
                {
                    if (!mmsis.Contains(elem.mmsi))
                        mmsis.Add(elem.mmsi);
                }
                return mmsis;
            }

            public Dictionary<UInt32, List<Single>> getMmsiSumValue()
            {
                var mmsiDict = new Dictionary<UInt32, List<Single>>();
                for (int i = 0; i < elems.Count; i++)
                {
                    if (!mmsiDict.ContainsKey(elems[i].mmsi))
                    {
                        mmsiDict[elems[i].mmsi] = new List<Single>();
                        for (int j = 0; j < 6; j++)
                        {
                            mmsiDict[elems[i].mmsi].Add(0);
                        }
                    }
                    double hdg = elems[i].hdg;
                    Single vogN = elems[i].vogN,
                           vogE = elems[i].vogE;
                    Single
                        SinHdg = (Single)Math.Sin(hdg * Math.PI / 180),
                        CosHdg = (Single)Math.Cos(hdg * Math.PI / 180),
                        SinSin = SinHdg * SinHdg,
                        CosCos = CosHdg * CosHdg,
                        SinCos = SinHdg * CosHdg;

                    var value = mmsiDict[elems[i].mmsi];

                    value[0] += CosCos;
                    value[1] += SinCos;
                    value[2] += SinSin;
                    value[3] += (SinCos * vogE - SinSin * vogN);
                    value[4] += (SinCos * vogN - CosCos * vogE);
                    value[5] += (Single)Math.Pow(vogN * SinHdg - vogE * CosHdg, 2);
                    mmsiDict[elems[i].mmsi] = value;
                }
                return mmsiDict;
            }

            public XY calcCurLSM(Dictionary<UInt32, List<Single>> mmsiDict)
            {
                var sumValue = new List<Single>();
                for (int j = 0; j < 6; j++)
                {
                    sumValue.Add(0);
                }
                foreach (KeyValuePair<UInt32, List<Single>> dicItem in mmsiDict)
                {
                    var value = mmsiDict[dicItem.Key];
                    for (int i = 0; i<6; i++)
                    {
                        sumValue[i] += value[i];
                    }
                }
                XY xy = new XY();
                Single 
                    //A11 = sumValue[0],
                    //A12 = sumValue[1],
                    //A22 = sumValue[2],
                    A22 = sumValue[0],
                    A12 = -sumValue[1],
                    A11 = sumValue[2],
                    B1 = sumValue[3],
                    B2 = sumValue[4],
                    F = sumValue[5];
                Single D = A11 * A22 - A12 * A12;
                if (D >= 1)
                {
                    xy.x = (A22 * B1 - A12 * B2) / D;
                    xy.y = (-A12 * B1 + A11 * B2) / D;
                    //Console.WriteLine("Values: {0}, {1}, {2}, {3}, {4}, {5}", A11, A12, A22, B1, B2, F); // REMOVE
                    //Console.WriteLine("lsm cur:{0},{1} D:{2}", xy.x, xy.y, D); // REMOVE
                }
                else 
                {
                    xy.x = 999;
                    xy.y = 999;
                }
                return xy;
            }
            public List<List<UInt32>> mmsiSelection(List<UInt32> brokenMmsi)
            {
                List<List<UInt32>> whiteBlackMmsi = new List<List<UInt32>>();
                List<UInt32> whiteMmsi = new List<UInt32>();
                List<UInt32> blackMmsi = new List<UInt32>();
                List<UInt32> mmsis = getMmsi();

                foreach (UInt32 mmsi in mmsis) 
                { 
                    if (brokenMmsi.Contains(mmsi)) 
                    { 
                        if (!blackMmsi.Contains(mmsi))
                            blackMmsi.Add(mmsi);
                    }
                    else
                    {
                        if (!whiteMmsi.Contains(mmsi))
                            whiteMmsi.Add(mmsi);
                    }
                }
                whiteBlackMmsi.Add(whiteMmsi);
                whiteBlackMmsi.Add(blackMmsi);
                return whiteBlackMmsi;
            }
            public ArrayList brokenMmsi(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                ArrayList allres = new ArrayList();
                List<UInt32> res = new List<UInt32>();
                string s = "ALL Mmsi";
                string ss = "";

                int num_mmsi = (int)mmsiVecDict.Count;
                if (num_mmsi < 4)
                {
                    allres.Add(res);
                    allres.Add(ss);
                    return allres;
                }

                List<UInt32> keymap = new List<UInt32>();
                List<int> mmsiGoodCount = new List<int>();
                List<int> mmsiBadCount = new List<int>();
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict) 
                {
                    s += dicItem.Key.ToString() + ", ";
                    keymap.Add(dicItem.Key);
                    mmsiGoodCount.Add(0);
                    mmsiBadCount.Add(0);
                }
                //Console.WriteLine(s); // REMOVE

                List<XY> xylist = new List<XY>();
                XY xy = new XY();
                bool isExistGoodPair = false;
                bool isExistBadPair = false;
                List<List<int>> comb = Combination.Generate(num_mmsi, num_mmsi-1, false);
                foreach (List<int> list in comb) 
                {
                    List<UInt32> mmsis = new List<UInt32>();
                    foreach (int mmsikey in list) 
                    {
                        mmsis.Add(keymap[mmsikey]);
                    }

                    xy = calcCurLSMTargetMmsi(mmsiVecDict, mmsis);

                    //Console.WriteLine(s); // REMOVE

                    if (Math.Abs(xy.x) < 4 && Math.Abs(xy.y) < 4)
                    {
                        ss += "Mmsi:";
                        foreach (int mmsikey in list)
                        {
                            ss += keymap[mmsikey].ToString() + ", ";
                            mmsiGoodCount[mmsikey] += 1;
                        }
                        ss += "curN:" + xy.x.ToString() + ", curE:" + xy.y.ToString() + '\n';
                        isExistGoodPair = true;
                    }
                    //else if (xy.x != 999) 
                    else
                    {
                        if (xy.x != 999)
                        {
                            ss += "Mmsi:";
                            foreach (int mmsikey in list)
                            {
                                ss += keymap[mmsikey].ToString() + ", ";
                            }
                            ss += "curN:" + xy.x.ToString() + ", curE:" + xy.y.ToString() + '\n';
                        }

                        foreach (int mmsikey in list)
                        {
                            mmsiBadCount[mmsikey] += 1;
                        }
                        isExistBadPair = true;
                    }
                }

                if (isExistGoodPair && isExistBadPair)
                {
                    ss += "BadMmsi:";
                    for (int i = 0; i < mmsiGoodCount.Count; i++)
                    {
                        if (mmsiGoodCount[i] == 0 && mmsiBadCount[i] > 0)
                        {
                            res.Add(keymap[i]);
                            ss += keymap[i].ToString() + ", ";
                        }
                    }
                }
                if (res.Count > 0) ss.Substring(0, ss.Length - 2);
                allres.Add(res);
                allres.Add(ss);

                //Console.WriteLine(s); // REMOVE
                return allres;
            }
            public List<UInt32> brokenMmsiOneoutComb(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                //  すべてのMMSIの組み合わせで偏流値を計算し、あるMMSIを含むMMSIがすべて異常な偏流値である場合、そのMMSIを異常船と判定する

                List<UInt32> res = new List<UInt32>();
                string s = "ALL Mmsi";

                // mmsiの数が４つ以下は異常判定しない（船が少ないとまともな偏流値が計算できず、誤って異常判定する可能性があるため）
                int num_mmsi = (int)mmsiVecDict.Count;
                if (num_mmsi < 4)
                {
                    return res;
                }

                List<UInt32> keymap = new List<UInt32>();
                List<XY> xylist = new List<XY>();
                XY xy = new XY();
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict) 
                {
                    s += dicItem.Key.ToString() + ", ";
                    keymap.Add(dicItem.Key);
                }

                //  一つ抜きのMMSIの組み合わせで偏流値を計算し、偏流値が正常・異常を判定 
                for (int i = 0; i < num_mmsi; i++) 
                { 
                    var list = new List<int>();
                    for (int j = 0; j < num_mmsi; j++) 
                    {
                        if (j != i) list.Add(j);
                    }

                    List<UInt32> mmsis = new List<UInt32>();
                    foreach (int mmsikey in list)
                    {
                        mmsis.Add(keymap[mmsikey]);
                    }

                    // 組み合わせのMMSIで偏流値を計算
                    xy = calcCurLSMTargetMmsi(mmsiVecDict, mmsis);

                    // 偏流値が正常なら排除したMMSIを異常とする
                    if (isValidCurValue(xy))
                    {
                        res.Add(keymap[i]);
                    }
                }

                //Console.WriteLine(s); // REMOVE
                return res;
            }
            public ArrayList brokenMmsiOneoutCombDebug(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                ArrayList allres = new ArrayList();
                List<UInt32> res = new List<UInt32>();
                string s = "ALL Mmsi";
                string ss = "";

                int num_mmsi = (int)mmsiVecDict.Count;
                if (num_mmsi < 4)
                {
                    allres.Add(res);
                    allres.Add(ss);
                    return allres;
                }

                List<UInt32> keymap = new List<UInt32>();
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict) 
                {
                    s += dicItem.Key.ToString() + ", ";
                    keymap.Add(dicItem.Key);
                }

                List<XY> xylist = new List<XY>();
                XY xy = new XY();
                //  一つ抜きのMMSIの組み合わせで偏流値を計算し、偏流値が正常・異常を判定 
                for (int i = 0; i < num_mmsi; i++)
                {
                    List<UInt32> mmsis = new List<UInt32>();
                    for (int j = 0; j < num_mmsi; j++)
                    {
                        if (j != i) 
                            mmsis.Add(keymap[j]);
                    }

                    xy = calcCurLSMTargetMmsi(mmsiVecDict, mmsis);

                    // 偏流値が正常なら排除したMMSIを異常とする
                    if (isValidCurValue(xy))
                    {
                        ss += "Mmsi:";
                        foreach (UInt32 mmsi in mmsis)
                        {
                            ss += mmsis.ToString() + ", ";
                        }
                        ss += "curN:" + xy.x.ToString() + ", curE:" + xy.y.ToString() + '\n';
                        res.Add(keymap[i]);
                    }
                }

                ss += "BadMmsi:";
                foreach (var badmmsi in res)
                {
                    ss += badmmsi.ToString() + ", ";
                }

                if (res.Count > 0) ss.Substring(0, ss.Length - 2);
                allres.Add(res);
                allres.Add(ss);

                //Console.WriteLine(s); // REMOVE
                return allres;
            }

            public List<UInt32> brokenMmsiAllComb(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                //  すべてのMMSIの組み合わせで偏流値を計算し、あるMMSIを含むMMSIがすべて異常な偏流値である場合、そのMMSIを異常船と判定する

                List<UInt32> res = new List<UInt32>();
                string s = "ALL Mmsi";

                // mmsiの数が４つ以下は異常判定しない（船が少ないとまともな偏流値が計算できず、誤って異常判定する可能性があるため）
                int num_mmsi = (int)mmsiVecDict.Count;
                if (num_mmsi < 4)
                {
                    return res;
                }

                List<UInt32> keymap = new List<UInt32>();
                List<int> mmsiGoodCount = new List<int>(); // 正常と判定された回数
                List<int> mmsiBadCount = new List<int>(); // 異常と判定された回数
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict)
                {
                    s += dicItem.Key.ToString() + ", ";
                    keymap.Add(dicItem.Key);
                    mmsiGoodCount.Add(0);
                    mmsiBadCount.Add(0);
                }

                List<XY> xylist = new List<XY>();
                XY xy = new XY();
                bool isExistGoodPair = false;
                bool isExistBadPair = false;

                // 1~num_mmsi-1 の個のMMSIの全組み合わせで偏流値を計算し、偏流値が正常・異常を判定 (1個のMMSIの組み合わせはいるのか？)
                for (int i = num_mmsi - 1; i >= 0; i--)
                {
                    // 組み合わせの生成
                    List<List<int>> comb = Combination.Generate(num_mmsi, i, false);
                    foreach (List<int> list in comb)
                    {
                        // 組み合わせ内のすべてのMMSIが一回以上正常と判定されていた場合は異常調査しない
                        List<UInt32> mmsis = new List<UInt32>();
                        bool isAllGoodMmsi = true;
                        foreach (int mmsikey in list)
                        {
                            mmsis.Add(keymap[mmsikey]);
                            if (mmsiGoodCount[mmsikey] == 0)
                            {
                                isAllGoodMmsi = false;
                            }
                        }

                        if (isAllGoodMmsi) continue;

                        // 組み合わせのMMSIで偏流値を計算
                        xy = calcCurLSMTargetMmsi(mmsiVecDict, mmsis);

                        // 偏流値が正常ならMMSIの正常と判定された回数を１増やす
                        if (isValidCurValue(xy))
                        {
                            foreach (int mmsikey in list)
                            {
                                mmsiGoodCount[mmsikey] += 1;
                            }
                            isExistGoodPair = true;
                        }
                        else
                        {
                            foreach (int mmsikey in list)
                            {
                                mmsiBadCount[mmsikey] += 1;
                            }
                            isExistBadPair = true;
                        }
                    }
                }

                // 一度も正常と判定されず、一回以上異常と判定された場合は異常船とする
                if (isExistGoodPair && isExistBadPair)
                {
                    for (int i = 0; i < mmsiGoodCount.Count; i++)
                    {
                        if (mmsiGoodCount[i] == 0 && mmsiBadCount[i] > 0)
                        {
                            res.Add(keymap[i]);
                        }
                    }
                }

                //Console.WriteLine(s); // REMOVE
                return res;
            }
            public ArrayList brokenMmsiAllCombDebug(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                ArrayList allres = new ArrayList();
                List<UInt32> res = new List<UInt32>();
                string s = "ALL Mmsi";
                string ss = "";

                int num_mmsi = (int)mmsiVecDict.Count;
                if (num_mmsi < 4)
                {
                    allres.Add(res);
                    allres.Add(ss);
                    return allres;
                }

                List<UInt32> keymap = new List<UInt32>();
                List<int> mmsiGoodCount = new List<int>();
                List<int> mmsiBadCount = new List<int>();
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict) 
                {
                    s += dicItem.Key.ToString() + ", ";
                    keymap.Add(dicItem.Key);
                    mmsiGoodCount.Add(0);
                    mmsiBadCount.Add(0);
                }
                //Console.WriteLine(s); // REMOVE

                List<XY> xylist = new List<XY>();
                XY xy = new XY();
                bool isExistGoodPair = false;
                bool isExistBadPair = false;
                for (int i = num_mmsi-1; i >= 0; i--)
                {
                    List<List<int>> comb = Combination.Generate(num_mmsi, i, false);
                    foreach (List<int> list in comb) 
                    {
                        List<UInt32> mmsis = new List<UInt32>();
                        bool isAllGoodMmsi = true;
                        foreach (int mmsikey in list) 
                        {
                            mmsis.Add(keymap[mmsikey]);
                            if (mmsiGoodCount[mmsikey]==0) 
                            {
                                isAllGoodMmsi = false;
                            }
                        }

                        if (isAllGoodMmsi) continue;

                        xy = calcCurLSMTargetMmsi(mmsiVecDict, mmsis);

                        //Console.WriteLine(s); // REMOVE

                        if (isValidCurValue(xy))
                        {
                            ss += "Mmsi:";
                            foreach (int mmsikey in list)
                            {
                                ss += keymap[mmsikey].ToString() + ", ";
                                mmsiGoodCount[mmsikey] += 1;
                            }
                            ss += "curN:" + xy.x.ToString() + ", curE:" + xy.y.ToString() + '\n';
                            isExistGoodPair = true;
                        }
                        else
                        {
                            if (xy.x != 999)
                            {
                                ss += "Mmsi:";
                                foreach (int mmsikey in list)
                                {
                                    ss += keymap[mmsikey].ToString() + ", ";
                                }
                                ss += "curN:" + xy.x.ToString() + ", curE:" + xy.y.ToString() + '\n';
                            }

                            foreach (int mmsikey in list)
                            {
                                mmsiBadCount[mmsikey] += 1;
                            }
                            isExistBadPair = true;
                        }
                    }
                }

                if (isExistGoodPair && isExistBadPair)
                {
                    ss += "BadMmsi:";
                    for (int i = 0; i < mmsiGoodCount.Count; i++)
                    {
                        if (mmsiGoodCount[i] == 0 && mmsiBadCount[i] > 0)
                        {
                            res.Add(keymap[i]);
                            ss += keymap[i].ToString() + ", ";
                        }
                    }
                }
                if (res.Count > 0) ss.Substring(0, ss.Length - 2);
                allres.Add(res);
                allres.Add(ss);

                //Console.WriteLine(s); // REMOVE
                return allres;
            }
            public XY calcCurLSMTargetMmsi(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict, List<UInt32> mmsis)
            {
                var sumValue = new List<Single>();
                for (int j = 0; j < 6; j++)
                {
                    sumValue.Add(0);
                }
                foreach (UInt32 mmsi in mmsis)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        (VEC, VEC) values = mmsiVecDict[mmsi];
                        VEC vec;
                        if (i == 0)
                        {
                            vec = values.Item1;
                        }
                        else 
                        { 
                            vec = values.Item2;
                        }

                        double lambda = vec.lambda;
                        if (lambda < 0.5) continue;

                        Single
                            Sin = (Single)Math.Sin(vec.direction * Math.PI / 180),
                            Cos = (Single)Math.Cos(vec.direction * Math.PI / 180),
                            SinSin = Sin * Sin,
                            CosCos = Cos * Cos,
                            SinCos = Sin * Cos;
                        Single w = (Single)lambda;
                        sumValue[0] += w * CosCos;
                        sumValue[1] += w * SinCos;
                        sumValue[2] += w * SinSin;
                        sumValue[3] += w * Cos * (Single)vec.norm;
                        sumValue[4] += w * Sin * (Single)vec.norm;
                        //Console.WriteLine("view:{0},{1}, {2}", w, lambda, (Single)vec.norm); // REMOVE
                    }
                }
                XY xy = new XY();
                Single 
                    A11 = sumValue[0],
                    A12 = sumValue[1],
                    A22 = sumValue[2],
                    B1 = sumValue[3],
                    B2 = sumValue[4],
                    F = sumValue[5];
                Single D = A11 * A22 - A12 * A12;

                if (D >= 1)
                {
                    xy.x = (A22 * B1 - A12 * B2) / D;
                    xy.y = (-A12 * B1 + A11 * B2) / D;
                }
                else 
                {
                    xy.x = 999;
                    xy.y = 999;
                }

                /*if (isExist)
                {
                    xy.x = (A22 * B1 - A12 * B2) / D;
                    xy.y = (-A12 * B1 + A11 * B2) / D;
                }*/
                //Console.WriteLine("Values: {0}, {1}, {2}, {3}, {4}, {5}", A11, A12, A22, B1, B2, F); // REMOVE
                //Console.WriteLine("lsm cur:{0},{1} D:{2}", xy.x, xy.y, D); // REMOVE
                return xy;
            }

            public (XY, XY, XY) calcLambdaTargetMmsi(XY cur, Dictionary<UInt32, (VEC, VEC)> mmsiVecDict, List<UInt32> mmsis)
            {
                var sumValue = new List<Single>();
                for (int j = 0; j < 3; j++)
                {
                    sumValue.Add(0);
                }
                foreach (UInt32 mmsi in mmsis)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        (VEC, VEC) values = mmsiVecDict[mmsi];
                        VEC vec;
                        if (i == 0)
                        {
                            vec = values.Item1;
                        }
                        else 
                        { 
                            vec = values.Item2;
                        }

                        double lambda = vec.lambda;
                        if (lambda < 0.5) continue;

                        Single
                            Sin = (Single)Math.Sin(2 * vec.direction * Math.PI / 180),
                            Cos = (Single)Math.Cos(2 * vec.direction * Math.PI / 180);
                        Single w = (Single)lambda;
                        sumValue[0] += w * Sin;
                        sumValue[1] += w * Cos;
                        sumValue[2] += w;
                        //Console.WriteLine("view:{0},{1}, {2}", w, lambda, (Single)vec.norm); // REMOVE
                    }
                }
                XY lambda12 = new XY();
                lambda12.x = (Single)(sumValue[2] - Math.Sqrt(Math.Pow((double)sumValue[0], 2) + Math.Pow((double)sumValue[1], 2)))/2;
                lambda12.y = (Single)(sumValue[2] + Math.Sqrt(Math.Pow((double)sumValue[0], 2) + Math.Pow((double)sumValue[1], 2)))/2;

                XY psi = new XY();
                psi.x = (Single)(Math.Atan2((double)sumValue[0], (double)sumValue[1])*180/(2*Math.PI));
                while (psi.x < 0)
                {
                    psi.x += 360;
                }
                while (psi.x > 360) 
                {
                    psi.x -= 360;
                }

                psi.y = psi.x + 90;
                while (psi.y > 360) 
                {
                    psi.y -= 360;
                }

                XY curLambda = new XY();
                curLambda.x = cur.x * (Single)Math.Cos(lambda12.x) + cur.y * (Single)Math.Sin(lambda12.x);
                curLambda.y = cur.x * (Single)Math.Cos(lambda12.y) + cur.y * (Single)Math.Sin(lambda12.y);

                return (curLambda, lambda12, psi);
            }
            public XY calcCurLSM2(Dictionary<UInt32, (VEC, VEC)> mmsiVecDict)
            {
                var sumValue = new List<Single>();
                for (int j = 0; j < 6; j++)
                {
                    sumValue.Add(0);
                }
                foreach (KeyValuePair<UInt32, (VEC, VEC)> dicItem in mmsiVecDict)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        VEC vec;
                        if (i == 0)
                        {
                            vec = mmsiVecDict[dicItem.Key].Item1;
                        }
                        else 
                        { 
                            vec = mmsiVecDict[dicItem.Key].Item2;
                        }

                        double lambda = vec.lambda;
                        if (lambda < 0.5) continue;

                        Single
                            Sin = (Single)Math.Sin(vec.direction * Math.PI / 180),
                            Cos = (Single)Math.Cos(vec.direction * Math.PI / 180),
                            SinSin = Sin * Sin,
                            CosCos = Cos * Cos,
                            SinCos = Sin * Cos;
                        Single w = (Single)lambda;
                        sumValue[0] += w * CosCos;
                        sumValue[1] += w * SinCos;
                        sumValue[2] += w * SinSin;
                        sumValue[3] += w * Cos * (Single)vec.norm;
                        sumValue[4] += w * Sin * (Single)vec.norm;
                        //Console.WriteLine("view:{0},{1}, {2}", w, lambda, (Single)vec.norm); // REMOVE
                    }
                }
                XY xy = new XY();
                Single 
                    A11 = sumValue[0],
                    A12 = sumValue[1],
                    A22 = sumValue[2],
                    B1 = sumValue[3],
                    B2 = sumValue[4],
                    F = sumValue[5];
                Single D = A11 * A22 - A12 * A12;

                if (D >= 1)
                {
                    xy.x = (A22 * B1 - A12 * B2) / D;
                    xy.y = (-A12 * B1 + A11 * B2) / D;
                }
                else 
                {
                    xy.x = 999;
                    xy.y = 999;
                }

                /*if (isExist)
                {
                    xy.x = (A22 * B1 - A12 * B2) / D;
                    xy.y = (-A12 * B1 + A11 * B2) / D;
                }*/
                //Console.WriteLine("Values: {0}, {1}, {2}, {3}, {4}, {5}", A11, A12, A22, B1, B2, F); // REMOVE
                //Console.WriteLine("lsm cur:{0},{1} D:{2}", xy.x, xy.y, D); // REMOVE
                return xy;
            }

            public Dictionary<UInt32, XY> calcCurMmsiLSM(Dictionary<UInt32, List<Single>> mmsiDict) 
            {
                var mmsiVecDict = new Dictionary<UInt32, XY>();
                foreach (KeyValuePair<UInt32, List<Single>> dicItem in mmsiDict) 
                {
                    XY xy = new XY();
                    var value = mmsiDict[dicItem.Key];
                    Single 
                        //A11 = value[0],
                        //A12 = value[1],
                        //A22 = value[2],
                        A22 = value[0],
                        A12 = -value[1],
                        A11 = value[2],
                        B1 = value[3],
                        B2 = value[4],
                        F = value[5];
                    Single D = A11 * A22 - A12 * A12;
                    if (D >= 1)
                    {
                        xy.x = (A22 * B1 - A12 * B2) / D;
                        xy.y = (-A12 * B1 + A11 * B2) / D;
                        //Console.WriteLine("MMSI: {0},  cur:{1},{2}, D:{3}", dicItem.Key, xy.x, xy.y, D); // REMOVE
                    }
                    else 
                    {
                        xy.x = 999;
                        xy.y = 999;
                    }
                    mmsiVecDict[dicItem.Key] = xy;
                }
                return mmsiVecDict;
            }
            public (VEC, VEC) calcCurLambda(Dictionary<UInt32, List<Single>> mmsiDict)
            {
                var sumValue = new List<Single>();
                for (int j = 0; j < 6; j++)
                {
                    sumValue.Add(0);
                }
                foreach (KeyValuePair<UInt32, List<Single>> dicItem in mmsiDict)
                {
                    var value = mmsiDict[dicItem.Key];
                    for (int i = 0; i<6; i++)
                    {
                        sumValue[i] += value[i];
                    }
                }
                XY xy = new XY();
                Single 
                    C = sumValue[0],
                    B = -sumValue[1],
                    A = sumValue[2],
                    D = sumValue[3],
                    E = sumValue[4],
                    F = sumValue[5];
                double
                    θ1Rad = (Math.Abs(C - A) < 1e-08) ? Math.PI / 2 : Math.Atan2(-2 * B, C - A) / 2,
                    θ2Rad = θ1Rad + Math.PI / 2,
                    Cosθ1 = Math.Cos(θ1Rad),
                    Sinθ1 = Math.Sin(θ1Rad),
                    Cosθ2 = Math.Cos(θ2Rad),
                    Sinθ2 = Math.Sin(θ2Rad);

                // 軸方向の固有値
                double
                    Lambda1 = (Math.Abs(Cosθ1) > Math.Abs(Sinθ1)) ? A + B * Sinθ1 / Cosθ1 : B * Cosθ1 / Sinθ1 + C,
                    Lambda2 = (Math.Abs(Cosθ2) > Math.Abs(Sinθ2)) ? A + B * Sinθ2 / Cosθ2 : B * Cosθ2 / Sinθ2 + C;

                VEC vec1 = new VEC();
                double F1 = -(D * Cosθ1 + E * Sinθ1) / Lambda1;// 軸方向の海流ベクトル長さ
                if (F1 < 0)
                {
                    F1 *= -1;
                    θ1Rad += Math.PI;
                }
                vec1.norm = F1;
                vec1.direction = θ1Rad*180/Math.PI;
                vec1.lambda = Lambda1;
                //Console.WriteLine("lambda1 norm:{0}, direction:{1}, lambda:{2}", vec1.norm, vec1.direction, Lambda1);  

                VEC vec2 = new VEC();
                double F2 = -(D * Cosθ2 + E * Sinθ2) / Lambda2;// 軸方向の海流ベクトル長さ
                if (F2 < 0)
                {
                    F2 *= -1;
                    θ2Rad += Math.PI;
                }
                vec2.norm = F2;
                vec2.direction = θ2Rad*180/Math.PI;
                vec2.lambda = Lambda2;
                //Console.WriteLine("lambda2 norm:{0}, direction:{1}, lambda:{2}", vec2.norm, vec2.direction, Lambda2);  
                return (vec1, vec2);
            }

            public Dictionary<UInt32, (VEC, VEC)> calcCurMmsiLambda(Dictionary<UInt32, List<Single>> mmsiDict) 
            {
                var mmsiCurDict = new Dictionary<UInt32, (VEC, VEC)>();
                foreach (KeyValuePair<UInt32, List<Single>> dicItem in mmsiDict) 
                {
                    var value = mmsiDict[dicItem.Key];
                    Single 
                        C = value[0],
                        B = -value[1],
                        A = value[2],
                        D = value[3],
                        E = value[4],
                        F = value[5];
                    double
                        θ1Rad = (Math.Abs(C - A) < 1e-08) ? Math.PI / 2 : Math.Atan2(-2 * B, C - A) / 2,
                        θ2Rad = θ1Rad + Math.PI / 2,
                        Cosθ1 = Math.Cos(θ1Rad),
                        Sinθ1 = Math.Sin(θ1Rad),
                        Cosθ2 = Math.Cos(θ2Rad),
                        Sinθ2 = Math.Sin(θ2Rad);

                    // 軸方向の固有値
                    double
                        Lambda1 = (Math.Abs(Cosθ1) > Math.Abs(Sinθ1)) ? A + B * Sinθ1 / Cosθ1 : B * Cosθ1 / Sinθ1 + C,
                        Lambda2 = (Math.Abs(Cosθ2) > Math.Abs(Sinθ2)) ? A + B * Sinθ2 / Cosθ2 : B * Cosθ2 / Sinθ2 + C;

                    VEC vec1 = new VEC();
                    double F1 = -(D * Cosθ1 + E * Sinθ1) / Lambda1;// 軸方向の海流ベクトル長さ
                    if (F1 < 0)
                    {
                        F1 *= -1;
                        θ1Rad += Math.PI;
                    }
                    vec1.norm = F1;
                    vec1.direction = θ1Rad*180/Math.PI;
                    vec1.lambda = Lambda1;
                    //Console.WriteLine("MMSI:{0}, lambda1 norm:{1}, direction:{2}, lambda:{3}", dicItem.Key, vec1.norm, vec1.direction, Lambda1);  

                    VEC vec2 = new VEC();
                    double F2 = -(D * Cosθ2 + E * Sinθ2) / Lambda2;// 軸方向の海流ベクトル長さ
                    if (F2 < 0)
                    {
                        F2 *= -1;
                        θ2Rad += Math.PI;
                    }
                    vec2.norm = F2;
                    vec2.direction = θ2Rad*180/Math.PI;
                    vec2.lambda = Lambda2;
                    //Console.WriteLine("MMSI:{0}, lambda2 norm:{1}, direction:{2}, lambda:{3}", dicItem.Key, vec2.norm, vec2.direction, Lambda2);  
                    mmsiCurDict[dicItem.Key] = (vec1, vec2);
                }
                return mmsiCurDict;
            }

            public List<XY> calcCurWithInsecs()
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
        static bool isValidCurValue(XY xy)
        {
            if (Math.Abs(xy.x) < 4 && Math.Abs(xy.y) < 4)
            {
                return true;
            }
            else 
            {
                return false;
            }
        }

        static Dictionary<DefineMeshArea.GridIdx, grid> dicGrids = new Dictionary<DefineMeshArea.GridIdx, grid>();

        static string logFile;

        //List<string> 
        static void Main(string[] args)
        {

            List<UInt32> dummyMMSIList = new List<UInt32> {999999990, 999999991, 999999992};
            bool DEBUG = false; // true;
            int maxTidx = 100000;
            List<string> inFiles;
            string outFile, errFile;
            BrokenShipManager blm = new BrokenShipManager();
            blm.load();
            string nextExt2 = (DEBUG)? nextExt+"_debug": nextExt;

            int maxLine;
            {
                argparse ap = new argparse("ais2ファイルを読み込み、船速や回転などのフィルタをかけて時空間グリッド・MMSI別のA～Fの値を計算して" + nextExt2 + "ファイルに出力する。");
                ap.ResisterArgs(args);
                inFiles = ap.getArgs((char)0, "InFile", "入力するais2ファイル", kind: argparse.Kind.ExistFile, canBeMulti: true);
                List<string> _outFiles = ap.getArgs('o', "OutFile", "出力する" + nextExt2 + "ファイル（省略時は最初のInFileの拡張子を" + nextExt2 + "にしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                List<string> _errFiles = ap.getArgs('e', "ErrorFile", "エラー出力ファイル（省略時はOutFileの拡張子を" + nextExt2 + "errにしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                List<string> _logFiles = ap.getArgs('g', "LogFile", "ログ出力ファイル（省略時はOutFileの拡張子を" + nextExt2 + "logにしたもの）", canOmit: true);
                List<string> _Lines = ap.getArgs('l', "Lines", "読み込む最大行数（省略時は最後まで）", kind: argparse.Kind.IsInt, canOmit: true);

                if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; }

                outFile = (_outFiles.Count() > 0) ? _outFiles[0] : UnexistFilePath.ChangeExt(inFiles[0], nextExt2);
                errFile = (_errFiles.Count() > 0) ? _errFiles[0] : UnexistFilePath.ChangeExt(outFile, nextExt2 + "err");
                logFile = (_logFiles.Count() > 0) ? _logFiles[0] : UnexistFilePath.ChangeExt(outFile, nextExt2 + "log");
                maxLine = (_Lines.Count() > 0) ? int.Parse(_Lines[0]) : int.MaxValue;
            }

            logout("Start Reading");

            if (DEBUG)
            {
                logout("DEBUG mode = true");
            }
            else
            {
                logout("DEBUG mode = false");
            }


            using (StreamWriter swErr = new StreamWriter(errFile, true))
            {
                //AISリストを作る

                //ファイルの読み込み
                UInt16 maxDT = 0;
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
                            foreach (AIS a in AISList) if (a.SOG10 < 80 || differenceBetween2Angle(a.COG10 / 10.0, a.Hdg) >= 15) a.Valid = false;

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
                            int count = 0;
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
                                    oge.mmsi = ais.Mmsi;

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
                                    count++;
                                    if (gi.dtIdx > maxDT) 
                                    { 
                                        maxDT = gi.dtIdx;
                                    }
                                }
                            if (dummyMMSIList.Contains(mmsi)) 
                            {
                                Console.WriteLine("MMSI:{0}, valid Count:{1}\n", mmsi, count);
                            }
                            if (aisr == null) break;
                            mmsi = aisr.Mmsi;
                        } while (line < maxLine);
                    }

                using (StreamWriter sw = new StreamWriter(outFile))
                {
                    List<DefineMeshArea.GridIdx> tidxs = new List<DefineMeshArea.GridIdx>(dicGrids.Keys);
                    tidxs.Sort();
                    int count = 0;
                    Single dataCount = 0;
                    int numTidxs = tidxs.Count;
                    Dictionary<DefineMeshArea.GridIdx, string> brokenMmsiLog  = new Dictionary<DefineMeshArea.GridIdx, string>();
                    Dictionary<DefineMeshArea.GridIdx, int> brokenMmsiCount  = new Dictionary<DefineMeshArea.GridIdx, int>();

                    sw.WriteLine("dtidx,latidx,lonidx,curN,curE,curLambda1,curLambda2,lambda1,lambda2,psi1,psi2");
                    // ログの出力
                    foreach (DefineMeshArea.GridIdx tidx in tidxs)
                    {
                        if (blm.mmsiDict.Count>0)
                            dataCount = 100*(Single)blm.brokenShip.Count/(Single)blm.mmsiDict.Count;
                        logout(string.Format("{0}/{1} tidx done (Detected broken rate={2}%, broken ship count={3})", count, numTidxs, dataCount, blm.brokenShip.Count), false);
                        count += 1;
                        if (DEBUG && count>maxTidx) break;
                        grid g = dicGrids[tidx];


                        // MMSIごとに固有値，固有ベクトルを計算
                        var mmsiSumValue = g.getMmsiSumValue();
                        var mmsiVecDict = g.calcCurMmsiLambda(mmsiSumValue);
                        XY cur = g.calcCurLSMTargetMmsi(mmsiVecDict, g.getMmsi());

                        // 出現したMMSI一覧リストの更新
                        var mmsis = g.getMmsi();
                        blm.add(mmsis);
                        if (mmsis.Count>=4)
                            blm.update(mmsis, tidx.dtIdx);

                        var whiteMmsi = g.getMmsi();  
                        var brokenMmsi = new List<UInt32>();
                        if (!isValidCurValue(cur)) 
                        { 
                            // 悪いMMSIの検出
                            var allres = g.brokenMmsiOneoutCombDebug(mmsiVecDict);
                            brokenMmsi = (List<UInt32>)allres[0];

                            // 異常船リストの更新
                            if (brokenMmsi.Count > 0)
                            {
                                blm.updateBrokenMmsi(brokenMmsi, tidx.dtIdx);
                                // 異常船リストのMMSIを排除して偏流値の計算
                            }
                        }

                        HashSet<UInt32> uniqueBroeknMmsiHash = new HashSet<UInt32>(brokenMmsi);
                        uniqueBroeknMmsiHash.UnionWith(blm.brokenShip);
                        List<UInt32> uniqueBrokenMmsiList = uniqueBroeknMmsiHash.ToList();
                        var whiteBlackMmsi = g.mmsiSelection(uniqueBrokenMmsiList);
                        whiteMmsi = whiteBlackMmsi[0];  
                        if (whiteBlackMmsi[1].Count > 0)
                            cur = g.calcCurLSMTargetMmsi(mmsiVecDict, whiteMmsi);


                        string addBlackMmsiString = "";

                        (XY, XY, XY) curLambda;
                        curLambda = g.calcLambdaTargetMmsi(cur, mmsiVecDict, whiteMmsi);

                        if (cur.x == 999) continue;

                        // debug用のログ出力
                        if (DEBUG)
                        {
                            // if (whiteBlackMmsi[1].Count > 0 && Math.Abs(curLambda.Item1.x)>8 && curLambda.Item2.x > 10)
                            //Console.WriteLine("is valid cur:{0}, lambda:{1}, blackCount:{2}, whiteCount:{3}", isValidCurValue(cur), curLambda.Item2.x, whiteBlackMmsi[1].Count, whiteMmsi.Count);
                            //if (isValidCurValue(cur) && curLambda.Item2.x > 10 && whiteBlackMmsi[1].Count==0 && whiteMmsi.Count>4)
                            //if (!isValidCurValue(cur) && whiteBlackMmsi[1].Count>0)
                            // if (brokenMmsi.Count>0)
                            if (g.getMmsi().Any(item => dummyMMSIList.Contains(item)))
                            {
                                // 交点を利用した偏流計算
                                List<XY> curInsec = g.calcCurWithInsecs();
                                var lsmMmsiCur = g.calcCurMmsiLSM(mmsiSumValue);
                                var lsmCur = g.calcCurLSM(mmsiSumValue);
                                var lambdaMmsiCur = g.calcCurMmsiLambda(mmsiSumValue);
                                //var lambdaCur = g.calcCurLambda(mmsiSumValue);
                                var lambdaCur = g.calcCurLSM2(lambdaMmsiCur);
                                // if (!isValidCurValue(lambdaCur)) continue;


                                sw.WriteLine("dtidx:{0}, latidx:{1}, lonidx:{2}", tidx.dtIdx, tidx.latIdx, tidx.lonIdx);
                                for (int i = 0; i < g.elems.Count; i++)
                                {
                                    sw.WriteLine("elem{0}, Hdg:{1}, tanHdg:{2}, vogN:{3}, vogE:{4}, mmsi:{5}", i, g.elems[i].hdg, g.elems[i].tanHdg, g.elems[i].vogN, g.elems[i].vogE, g.elems[i].mmsi);
                                 }

                                List<List<(XY, XY)>> insecs = g.calcInsecs();
                                for (int i = 0; i < insecs.Count; i++)
                                {
                                    for (int j = i + 1; j < insecs.Count; j++)
                                    {
                                        if (i == j) continue;
                                        sw.WriteLine("{0}-{1}, insecX:{2}, insecY:{3}, numX:{4}, numY:{5}", i, j, insecs[i][j].Item1.x, insecs[i][j].Item1.y, insecs[i][j].Item2.x, insecs[i][j].Item2.y);
                                    }
                               }
                                sw.WriteLine("NoBrokenLSM: curN:{0}, curE:{1}", cur.x, cur.y);
                                sw.WriteLine("Min: curN:{0}, curE:{1}", curInsec[1].x, curInsec[1].y);

                                //brokenMmsiCount[tidx] = blm.brokenShip.Count;
                                //if (brokenMmsiCount[tidx] != 0)
                                //{
                                //    sw.WriteLine(brokenMmsiLog[tidx]);
                                //}
                                string ss = "AllBadMmsi";
                                if (brokenMmsi.Count > 0) 
                                {
                                    foreach (UInt32 bMmsi in brokenMmsi) 
                                    { 
                                        ss += bMmsi.ToString();
                                        ss += ", ";
                                    }
                                    sw.WriteLine(ss);
                                }

                                sw.WriteLine(addBlackMmsiString);
                                /*foreach (KeyValuePair<UInt32, XY> dicItem in lsmMmsiCur)
                                {
                                    sw.WriteLine("MMSI:{0}, curN:{1}, curE:{2}", dicItem.Key, dicItem.Value.x, dicItem.Value.y);
                                }*/
                                sw.WriteLine("HdgLSM: curN:{0}, curE:{1}", lsmCur.x, lsmCur.y);
                                sw.WriteLine("LambdaLSM: curN:{0}, curE:{1}", lambdaCur.x, lambdaCur.y);
                                sw.WriteLine("\n");
                                blm.save();
                            }
                        }
                        else 
                        {
                            // 11/20 追記，再実行・検証はまだしていない
                            if (!isValidCurValue(cur)) continue;
                            sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", tidx.dtIdx, tidx.latIdx, tidx.lonIdx, cur.x, cur.y, curLambda.Item1.x, curLambda.Item1.y, curLambda.Item2.x, curLambda.Item2.y, curLambda.Item3.x, curLambda.Item3.y);
                        }

                    }
                }
                blm.save();
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