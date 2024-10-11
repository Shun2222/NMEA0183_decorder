using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using flo = System.Single;

/// <summary>
/// 観測ごとの観測レコードのうち日時以外の情報が入る
/// </summary>
class Observatory
{
    public readonly string shipName;
    public readonly LatLonIdx llidx;
    public readonly flo DriftN, DriftE,FOPN,FOPE;
    public bool valid;
    public Observatory(LatLonIdx llidx, flo DriftN, flo DriftE, flo FOPN, flo FOPE,string shipName)
    {
        this.llidx = llidx; this.DriftN = DriftN; this.DriftE = DriftE; this.FOPN = FOPN; this.FOPE=FOPE;this.shipName = shipName;
        valid = false;
    }
}
/// <summary>
/// 船舶による観測データ全体が入るシングルトン
/// </summary>
class Ship
{
    /// <summary>
    /// 日時順にならんだ、日時別観測レコードリスト
    /// </summary>
    public SortedDictionary<int, List<Observatory>> Dt_Observatories = new SortedDictionary<int, List<Observatory>>();
    public Ship(List<string> ShipFiles)
    {
        foreach (string shipFile in ShipFiles) using (StreamReader sr = new StreamReader(shipFile))
            {
                Console.WriteLine("ShipRead file={0}", shipFile);

                //1行目はヘッダで読み飛ばし
                sr.ReadLine();
                //2行目以降をobservatoriesにadd
                int l = 1;
                while (!sr.EndOfStream)
                {

                    if (l % 1000 == 0) Console.Write("{0:0.00}%,{1}lines done\r", 100.0 * sr.BaseStream.Position / sr.BaseStream.Length, l);
                    l++;
                    string[] ss = sr.ReadLine().Split(',');
                    if (ss[1].Trim() != "9409869")//旭星丸を除く
                    {
                        try
                        {
                            DateTime dt = DateTime.Parse(ss[0]); //時刻は全てUTC扱い
                            flo Lat = flo.Parse(ss[24]),
                                   Lon = flo.Parse(ss[25]),
                                   driftN = flo.Parse(ss[26]),
                                   driftE = flo.Parse(ss[27]),
                                   FOPN = flo.Parse(ss[28]),
                                   FOPE = flo.Parse(ss[29]);
                            string shipName = l.ToString(); //ここでは行番号が入る ss[1];//ここではshipNameにはIMO番号が入る

                            int DtIdx = Time.DtIdx(dt);

                            LatLonIdx LLidx = LatLonIdx.GetLatLonIdxFromDeg(Lat, Lon);
                            if (LLidx.AreaStatus == 7 && driftN * driftN + driftE * driftE < 9)  // ROIでありドリフトが3kt以下のものを登録
                            {
                                if (!Dt_Observatories.ContainsKey(DtIdx)) Dt_Observatories.Add(DtIdx, new List<Observatory>());
                                Dt_Observatories[DtIdx].Add(new Observatory(LLidx, driftN, driftE, FOPN, FOPE, shipName));
                            }
                        }
                        catch (Exception e)
                        {
                            //値がnullのものがあるためここで読み飛ばす
                        }
                    }
                }
            }
    }
    public void RemoveInvalid()
    {
        List<int> RemoveDts = new List<int>();
        foreach(int dt in Dt_Observatories.Keys)
        {
            List<Observatory> observatories = Dt_Observatories[dt];
            int c = observatories.Count;
            for (int i = c - 1; i >= 0; i--) if (!observatories[i].valid) observatories.RemoveAt(i);
            if (observatories.Count == 0) RemoveDts.Add(dt);
        }
        foreach (int dt in RemoveDts) Dt_Observatories.Remove(dt);
    }
}
