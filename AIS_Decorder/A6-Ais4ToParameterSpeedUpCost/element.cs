using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using flo = System.Single;

class Element
{
    public flo deg { get; private set; }
    public flo f { get; private set; }
    public flo w { get; private set; }

    public Element(flo deg, flo f, flo w)
    {
        if (f < 0)
        {
            f = -f;
            deg += 180;
        }
        this.deg = deg % 360;
        this.f = f;
        this.w = w;
    }
}

class ABCDE
{
    public flo a { get; private set; }
    public flo b { get; private set; }
    public flo c { get; private set; }
    public flo d { get; private set; }
    public flo e { get; private set; }
    //public UnifiedElements() { a = 0; b = 0; c = 0; d = 0; e = 0; }
    public void Clear() { a = 0; b = 0; c = 0; d = 0; e = 0; }

    public void AddElement(Element ele, flo w = 1) { AddAis4(ele.deg, ele.f, w * ele.w); }
    public void AddAis4(flo deg, flo f, flo w)
    {
        flo cos =(flo) Math.Cos(deg / 180 * Math.PI),
               sin =(flo) Math.Sin(deg / 180 * Math.PI);
        a +=  w * cos * cos;
        b +=  w * sin * cos;
        c +=  w * sin * sin;
        d +=  w * cos * f;
        e +=  w * sin * f;
    }
    public void AddElement(ABCDE ue, flo w = 1)
    {
        a += w * ue.a;
        b += w * ue.b;
        c += w * ue.c;
        d += w * ue.d;
        e += w * ue.e;
    }

    public Element[] GetElements()
    {
        const flo LAMBDA_THRES =(flo) 0.5;
        double phi1 = Math.Atan2(2 * b, a - c) / 2,
               phi2 = phi1 + Math.PI / 2;
        flo    SinPhi1 =(flo) Math.Sin(phi1), CosPhi1 = (flo)Math.Cos(phi1),
               SinPhi2 = (flo)Math.Sin(phi2), CosPhi2 = (flo)Math.Cos(phi2),
               lambda1 = (CosPhi1 * CosPhi1 > SinPhi1 * SinPhi1) ? a + b * SinPhi1 / CosPhi1 : b * CosPhi1 / SinPhi1 + c,
               lambda2 = (CosPhi2 * CosPhi2 > SinPhi2 * SinPhi2) ? a + b * SinPhi2 / CosPhi2 : b * CosPhi2 / SinPhi2 + c,
               f1 = (d * CosPhi1 + e * SinPhi1) / lambda1,
               f2 = (d * CosPhi2 + e * SinPhi2) / lambda2;

        Element r1 = new Element((flo)(phi1 * 180 / Math.PI), f1, lambda1),
                r2 = new Element((flo)(phi2 * 180 / Math.PI), f2, lambda2);

        List<Element> ret = new List<Element>();
        if (lambda1 > LAMBDA_THRES) ret.Add(r1);
        if (lambda2 > LAMBDA_THRES) ret.Add(r2);
        if (lambda1 < lambda2) ret.Reverse();
        return ret.ToArray();
    }

    public flo Flow(flo DirDeg)
    {
        return ((c * d - b * e) * (flo)Math.Cos(DirDeg / 180 * Math.PI) + (a * e - b * d) *(flo) Math.Sin(DirDeg / 180 * Math.PI)) / (a * c - b * b);
    }
    public flo LogDet {  get { return(flo) Math.Log10( Math.Abs( a * c - b * b)); } }
}

class AIS
{
    
    public SortedDictionary<int, Dictionary<LatLonIdx, ABCDE>> ABCDESumByMesh = new SortedDictionary<int, Dictionary<LatLonIdx, ABCDE>>();
    public AIS(List<string> AIS4files, HashSet<int> eliminateMMSIs=null)
    {
        foreach (string ais4file in AIS4files) using (StreamReader sr = new StreamReader(ais4file))
            {
                Console.WriteLine("AIS4Read file={0}", ais4file);
                //1行目はそのまま
                sr.ReadLine();
                //2行目はヘッダで読み飛ばし
                sr.ReadLine();
                //3行目以降をais4recordsにAdd
                int l = 0;
                while (!sr.EndOfStream)
                {
                    if (l % 1000 == 0) Console.Write("{0:0.00}%,{1}lines done\r", 100.0 * sr.BaseStream.Position / sr.BaseStream.Length, l);
                    l++;
                    string[] ss = sr.ReadLine().Split(',');
                    int MMSI = int.Parse(ss[0]),
                        DtIdx = (int) (int.Parse(ss[1])/Time.HoursPerMesh),  
                        LatIdx = int.Parse(ss[2]),
                        LonIdx = int.Parse(ss[3]);

                    //if (DtIdx >= 32880) continue;  // 2014/10/01のデータのみ
                    if (eliminateMMSIs!=null && eliminateMMSIs.Contains(MMSI)) continue;

                    flo ThetaDeg = flo.Parse(ss[4]),
                           F = flo.Parse(ss[5]),
                           LineCount = flo.Parse(ss[6]);

                    LatLonIdx LLidx = new LatLonIdx(LatIdx, LonIdx);
                    
                    if (!ABCDESumByMesh.ContainsKey(DtIdx)) ABCDESumByMesh.Add(DtIdx, new Dictionary<LatLonIdx, ABCDE>());
                    if (!ABCDESumByMesh[DtIdx].ContainsKey(LLidx)) ABCDESumByMesh[DtIdx].Add(LLidx, new ABCDE());
                    ABCDESumByMesh[DtIdx][LLidx].AddAis4(ThetaDeg, F, LineCount);

                }
            }
        Console.WriteLine("AIS4Read Done");      
    }
}