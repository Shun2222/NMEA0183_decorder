using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using flo = System.Single;

class CurrentmapStaticClass
{
    static bool inited = false;
    public static Log log;
    public static void Init()
    {
        if (inited) return;
        inited = true;
        log = new Log(@"distancefromship.csv");
        log.WriteLine("ShipName,Head,DtIdx,LatIdx,LonIdx,DriftN,DriftE,FOPN,FOPE,FOPerr,AISN,AISE,AISerr,AISLogDet,Valid,a,b,c,d,e");
    }
}
class CurrentMap
{
    static readonly flo Ln2 = (flo)Math.Log(2);

    public readonly ABCDE[,] map = new ABCDE[Area.LatIdxMax - Area.LatIdxMin + 1, Area.LonIdxMax - Area.LonIdxMin + 1];
    public int DtIdx { get; private set; }
    public flo HalfHour { get; private set; }
    public flo HalfMeshNorth { get; private set; }
    public flo HalfMeshEast { get; private set; }

    public CurrentMap(AIS ais, int dtIdx, flo halfDtMesh, flo halfMeshNorth, flo halfMeshEast, bool RenewRegion, Ship ship = null)
    {
        DtIdx = dtIdx; HalfMeshNorth = halfMeshNorth; HalfMeshEast = halfMeshEast;
        CurrentmapStaticClass.Init();

        //計測値のある座標
        IEnumerable<LatLonIdx> obsLocation = null;
        if (ship != null)
        {
            if (!ship.Dt_Observatories.ContainsKey(DtIdx) || ship.Dt_Observatories[DtIdx] == null || ship.Dt_Observatories[DtIdx].Count == 0) return;
            else obsLocation = ship.Dt_Observatories[DtIdx].Select(obs => obs.llidx).Distinct();
        }

        //weight定義
        SpatialWeight.SetWeight(halfMeshNorth, halfMeshEast, RenewRegion);
        Time.SetWeight(halfDtMesh, UseFuture: true, RenewDtIdxList: RenewRegion);

        //AISデータをまわす
        //時刻でまわす

        foreach (int deltadt in Time.DtIdxList)
        {
            int dt = deltadt + dtIdx;
            if (ais.ABCDESumByMesh.ContainsKey(dt))
            {
                flo weightDT = Time.Weight(deltadt);                      //時間的なweight低減
                //それぞれのAISデータについて
                foreach (KeyValuePair<LatLonIdx, ABCDE> AIS_and_Position in ais.ABCDESumByMesh[dt])
                {
                    LatLonIdx AISPosition = AIS_and_Position.Key;
                    ABCDE AIS = AIS_and_Position.Value;
                    //空間でまわす
                    IEnumerable<LatLonIdx> loop;
                    loop = ship==null ? SpatialWeight.LLIdxList.Select(x => AISPosition.GetAddedLLIdx(x.Item1, x.Item2)): obsLocation;

                    foreach (LatLonIdx llidxToStore in loop)
                        if ((Area.AreaStatus(llidxToStore) & 3) == 3)  //海なら
                        {
                            flo weightSpace = SpatialWeight.Weight(
                                new Tuple<int,int>(AISPosition.LatIdx - llidxToStore.LatIdx, AISPosition.LonIdx - llidxToStore.LonIdx)); //空間的なweight低減
                            if (weightSpace != 0)
                            {
                                if (map[llidxToStore.LatIdxFromZero, llidxToStore.LonIdxFromZero] == null)
                                    map[llidxToStore.LatIdxFromZero, llidxToStore.LonIdxFromZero] = new global::ABCDE();
                                map[llidxToStore.LatIdxFromZero, llidxToStore.LonIdxFromZero].AddElement(AIS, weightSpace * weightDT);

                            }
                        }
                }
            }
        }
    }
    
    public ABCDE Map(LatLonIdx llidx)
    {
        return map[llidx.LatIdxFromZero, llidx.LonIdxFromZero];
    }

    public void CsvOut(string filename,flo SpeedMultiplier)
    {
        //Log mapoutN = new Log(filename + "N.csv", flush: false);
        //Log mapoutE = new Log(filename + "E.csv", flush: false);
        //Log mapoutD = new Log(filename + "D.csv", flush: false);
        string[] ss = new string[] { "NS", "EW", "LogDT","Sp","NS4","EW4","Sp4","a","b","c","d","e" };
        Log[] csvlog = new Log[ss.Count()];
        int i = 0;
        foreach (string s in ss)
        {
            csvlog[i++] = new Log(filename + s + ".csv", flush: true, sjis: true);
        }

        foreach (int LatIdx in LatLonIdx.LatIdxs())
        {

            //string sN = "", sE = "", sD = "";
            string[] s = new string[ss.Count()];
            foreach (int LonIdx in LatLonIdx.LonIdxs())
            {
                LatLonIdx llidx = new LatLonIdx(LatIdx, LonIdx);

                if (Map(llidx) != null)
                {
                    flo ns = Map(llidx).Flow(0) * SpeedMultiplier,
                        ew = Map(llidx).Flow(90) * SpeedMultiplier,
                        dt = Map(llidx).LogDet,
                        sp = (flo)Math.Sqrt(ns * ns + ew * ew),
                        a = Map(llidx).a,
                        b = Map(llidx).b,
                        c = Map(llidx).c,
                        d = Map(llidx).d,
                        e = Map(llidx).e;

                    s[0] += $"{ns:0.00}";
                    s[1] += $"{ew:0.00}";
                    s[2] += $"{dt:0.00}";
                    s[3] += $"{sp:0.00}";
                    if(dt>4)
                    {
                        s[4] += $"{ns:0.00}";
                        s[5] += $"{ew:0.00}";
                        s[6] += $"{sp:0.00}";
                    }
                    s[7] += $"{a:0.00}";
                    s[8] += $"{b:0.00}";
                    s[9] += $"{c:0.00}";
                    s[10] += $"{d:0.00}";
                    s[11] += $"{e:0.00}";
                }
                else if ((llidx.AreaStatus & 0x02) ==0 )//JWA海流の入ってないところ→X
                {
                    for(i=0;i<s.Count();i++) s[i] += "X";
                }
                for (i = 0; i < ss.Count(); i++) s[i] += ",";
            }
            for (i = 0; i < ss.Count(); i++) csvlog[i].WriteLine(s[i]);
        }
    }

    //計測値との距離の総和を測る 当日の計測値がなければfalseを返す
    public bool DistanceFromShip(Ship ship, bool RenewObsValid,flo MultiplierCurrent, string logHead,out flo sumAbsEr, out flo SumSqEr, out int count)
    {
        SumSqEr = 0; sumAbsEr = 0;         count = 0;
        if (ship.Dt_Observatories.ContainsKey(DtIdx))
        {
            foreach (Observatory obs in ship.Dt_Observatories[DtIdx])
            {
                LatLonIdx llidx = obs.llidx;
                if (llidx.AreaStatus == 7)
                {
                    string s="";
                    flo SqErFOP = (obs.DriftN - obs.FOPN) * (obs.DriftN - obs.FOPN) + (obs.DriftE - obs.FOPE) * (obs.DriftE - obs.FOPE);
                    if(logHead!=null) s = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                         obs.shipName, logHead, DtIdx, llidx.LatIdx, llidx.LonIdx, obs.DriftN, obs.DriftE, obs.FOPN, obs.FOPE, Math.Sqrt(SqErFOP));


                    ABCDE pointOfMap = Map(llidx);
                    if (pointOfMap != null)
                    {
                        flo AISFlowN = pointOfMap.Flow(0)*MultiplierCurrent,
                               AISFlowE = pointOfMap.Flow(90) * MultiplierCurrent,
                               AISLogDet = pointOfMap.LogDet,
                               SqErAIS = (obs.DriftN - AISFlowN) * (obs.DriftN - AISFlowN) + (obs.DriftE - AISFlowE) * (obs.DriftE - AISFlowE),
                               AbsErAIS=(flo)Math.Sqrt(SqErAIS),
                               a=pointOfMap.a,
                               b = pointOfMap.b,
                               c = pointOfMap.c,
                               d = pointOfMap.d,
                               e = pointOfMap.e;
                        if (RenewObsValid) obs.valid = (AISLogDet > 4/* && SqErAIS < 25*/ );
                        if (logHead != null) s += string.Format(",{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", AISFlowN, AISFlowE, Math.Sqrt(SqErAIS), AISLogDet, obs.valid,a,b,c,d,e);

                        if (obs.valid)
                        {
                            sumAbsEr += AbsErAIS;
                            SumSqEr += SqErAIS;
                            count++;
                        }
                    }

                    if (logHead != null) CurrentmapStaticClass.log.WriteLine(s);
                }
            }
        }
        return count > 0;
    }
}



