using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using flo = System.Single; //System.SingleかSystem.Double

class Program
{
    class Conf
    {
        public IEnumerable<int>
            DtToLoop=null,
            DtToOutput = null;
        public flo
            halfMeshDt=0,
            halfMeshNorth = 0,
            halfMeshEast = 0,
            Multiplier = 0,
            alpha = 0;  //0のときは学習しない
        public bool
            ChangeMultiplier=false,
            RenewObservatoryValid = false;
        
    }
    static void Main(string[] args)
    {
        //出力フォルダ
        string outFolder;
        for (int i = 0; ; i++)
        {
            outFolder = $"out{i}";
            if (!Directory.Exists(outFolder)) break;
        }
        Log.SetWorkFolder(outFolder);

        // Shipをロードするか？
        bool loadShip = true;
        List<string> shipFiles = new List<string>();

        // Shipをロード
        if (loadShip) shipFiles.Add(@"connect.csv");
        Ship ship = new Ship(shipFiles);


        // 不使用のMMSIをロード
        HashSet<int> MMSIsNotForEstimate;
        {
            string path = @"invalidMMSI.csv";
            int dmy;
            MMSIsNotForEstimate = new HashSet<int>(
                File.ReadLines(path)
                .Select(s => s.Split(',')[0])
                .Where(s => int.TryParse(s, out dmy))
                .Select(s => int.Parse(s)).ToArray());
        }

        //AISをロード
        List<string> ais4Files = new List<string>();
        ais4Files.AddRange(Directory.GetFiles(@"U:\ais4", "201512.ais4", SearchOption.TopDirectoryOnly));
        AIS ais = new AIS(ais4Files, MMSIsNotForEstimate);
        int LoopStart = Time.DtIdx(new DateTime(2015, 12, 1)),
            LoopEnd = Time.DtIdx(new DateTime(2016, 1, 1));

        Conf conf=new global::Program.Conf();

        // ループを回したり海流をcsv出力する時刻の集合
        conf.DtToLoop = Enumerable.Range(LoopStart, LoopEnd - LoopStart + 1);

        
        //出力先
        Log log = new Log("out.csv", flush: true, show: true);
        log.WriteLine("Now,L,DtIdx,DT,halfHour,halfMeshNorth,halfMeshEast,Multiplier,ShipCount,aveEr,RMSE");
        for (int L = 0; L<1; L++)
        {
            IEnumerable<int> DtToCompareShip = ship.Dt_Observatories.Keys;
            switch (L)
            {
                case 0:
                    conf.DtToOutput = conf.DtToLoop.Where(dt => Time.DT(dt).Hour == 0);
                    conf.halfMeshDt = (flo)(8.545 / Time.HoursPerMesh);
                    conf.halfMeshNorth = (flo)1.117;
                    conf.halfMeshEast = (flo)1.918;
                    conf.Multiplier = (flo)1;
                    conf.alpha = 0;
                    conf.ChangeMultiplier = false;
                    conf.RenewObservatoryValid = true;
                    break;
                default:
                    if (L % 10 == 0)
                    {
                        //10回ごとに学習を停止して出力
                        conf.DtToOutput = conf.DtToLoop.Where(dt => Time.DT(dt).Hour == 0);
                        conf.alpha = 0;
                    }
                    else
                    {
                        conf.DtToOutput = null;
                        conf.alpha = (flo)(0.01 * Math.Pow(1 / 1.1, L - 1));
                    }
                    conf.RenewObservatoryValid = false;
                    break;
            }

            foreach (int dt in conf.DtToLoop)
            {
                Console.WriteLine($"Dt={dt}:{Time.DT(dt):yyyy/MM/dd-HH}");
                CurrentMap map;
                
                if (conf.DtToOutput != null && conf.DtToOutput.Contains(dt))
                {
                    //全体マップ作製、出力
                    map = new CurrentMap(ais, dt, conf.halfMeshDt, conf.halfMeshNorth, conf.halfMeshEast, true, null);
                    map.CsvOut($"{dt}({Time.DT(dt):yyyyMMddHH})-{L}",conf.Multiplier);
                }
                else
                {
                    //部分マップ作製
                    map = new CurrentMap(ais, dt, conf.halfMeshDt, conf.halfMeshNorth, conf.halfMeshEast, true, ship);
                }

                if (DtToCompareShip.Contains(dt))
                {
                    flo sumSqEr, sumSqErH, sumSqErN, sumSqErE, sumSqErM;
                    flo sumAbsEr, sumAbsErH, sumAbsErN, sumAbsErE, sumAbsErM;

                    int count;
                    //観測値からの誤差
                    bool success = map.DistanceFromShip(ship, conf.RenewObservatoryValid, conf.Multiplier, $"map-{L}", out sumAbsEr, out sumSqEr, out count);
                    //学習状態（と、当該Dtでの観測値との誤差）を出力
                    string s = $"{DateTime.Now},{L},{dt},{Time.DT(dt):yyyy/MM/dd-HH},{conf.halfMeshDt:0.000},{conf.halfMeshNorth:0.000},{conf.halfMeshEast:0.000},{conf.Multiplier:0.000}";
                    if (success) s += $",{count},{sumAbsEr / count:0.000},{Math.Sqrt(sumSqEr / count):0.000}";
                    log.WriteLine(s);

                    if (conf.alpha>0 && success)
                    {
                        //最急降下法ここから

                        flo deltaH = conf.halfMeshDt * (flo)0.01;
                        CurrentMap mapH = new CurrentMap(ais, dt, conf.halfMeshDt + deltaH, conf.halfMeshNorth, conf.halfMeshEast, false, ship);
                        mapH.DistanceFromShip(ship, false, conf.Multiplier, null /*$"mapH-{L}"*/, out sumAbsErH, out sumSqErH, out count);

                        flo deltaN = conf.halfMeshNorth * (flo)0.01;
                        CurrentMap mapN = new CurrentMap(ais, dt, conf.halfMeshDt, conf.halfMeshNorth + deltaN, conf.halfMeshEast, false, ship);
                        mapN.DistanceFromShip(ship, false, conf.Multiplier, null /*$"mapN-{L}"*/, out sumAbsErN, out sumSqErN, out count);

                        flo deltaE = conf.halfMeshEast * (flo)0.01;
                        CurrentMap mapE = new CurrentMap(ais, dt, conf.halfMeshDt, conf.halfMeshNorth, conf.halfMeshEast + deltaE, false, ship);
                        mapE.DistanceFromShip(ship, false, conf.Multiplier, null /*$"mapE-{L}"*/, out sumAbsErE, out sumSqErE, out count);

                        flo deltaM = conf.Multiplier * (flo)0.01;
                        if (conf.ChangeMultiplier)
                        {
                            map.DistanceFromShip(ship, false, conf.Multiplier + deltaM, null /*$"mapM-{L}"*/, out sumAbsErM, out sumSqErM, out count);
                        }
                        else { sumAbsErM = 0; sumSqErM = 0; }

                        conf.halfMeshDt -= conf.alpha * (sumSqErH - sumSqEr) / deltaH;
                        conf.halfMeshNorth -= (flo)(conf.alpha * (sumSqErN - sumSqEr) / deltaN * 0.1);
                        conf.halfMeshEast -= (flo)(conf.alpha * (sumSqErE - sumSqEr) / deltaE * 0.1);
                        if (conf.ChangeMultiplier) conf.Multiplier -= (flo)(conf.alpha * (sumSqErM - sumSqEr) / deltaM * 0.01);
                    }
                }
            }
            if (conf.RenewObservatoryValid) ship.RemoveInvalid();
        }
    }
}

