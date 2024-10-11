using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;

class Program
{
    static Log log;
    //UInt64 Hash(string s)
    //{
    //    UInt64 hash;
    //    if (!UInt64.TryParse(s.Substring(s.LastIndexOf(',') + 1),
    //        System.Globalization.NumberStyles.HexNumber,
    //        System.Globalization.CultureInfo.InvariantCulture,
    //        out hash))
    //        hash = 0;
    //    return hash;
    //}
    //static Dictionary<string, string> dicAis1ToLinear = new Dictionary<string, string>();
    //static string ais1ToLinear(string s)
    //{
    //    if (dicAis1ToLinear.ContainsKey(s)) return dicAis1ToLinear[s];
    //    string r;
    //    try
    //    {
    //        string[] ss = s.Split(',');
    //        string mmsi = "         " + ss[2];mmsi = mmsi.Substring(mmsi.Length - 9);
    //        string dt =DateTime.Parse( ss[0]).ToString("yyyyMMddHHmmss");
    //        string hash = ss[ss.Length - 1];

    //        if (hash.Length == 16) r= mmsi + dt + hash;
    //        else r= null;
    //    }
    //    catch { r= null; }
    //    if (dicAis1ToLinear.Count > 10000) dicAis1ToLinear.Clear();
    //    dicAis1ToLinear[s] = r;
    //    if (r == null)
    //        log.WriteLine($"エラー ais1ToLinear s={s}");
    //    return r;
    //}
    //static int comparer(string x,string y)
    //{
    //    string sx = ais1ToLinear(x);
    //    string sy = ais1ToLinear(y);
    //    if (sx == null) return 1;
    //    else if (sy == null) return -1;
    //    else return sx.CompareTo(sy);
    //}

    class record : IComparable<record>
    {
        public readonly string s;
        readonly DateTime dt;
        public readonly int MMSI;
        public readonly ulong Hash;
        //readonly string linear;
        public readonly Exception e;

        //MMSI昇順、日時昇順、Hash昇順に並べるための比較器
        public int CompareTo(record obj)
        {
            if (obj == null) throw new ArgumentNullException();
            int r=0;
            if (r == 0) r = MMSI.CompareTo(obj.MMSI);
            if (r == 0) r = dt.CompareTo(obj.dt);
            if (r == 0) r = Hash.CompareTo(obj.Hash);
            return r;
        }

        public record(string s)
        {
            try
            {
                this.s = s;
                string[] ss = s.Split(',');
                dt = DateTime.Parse(ss[0]);
                MMSI = int.Parse(ss[2]);
                Hash = Convert.ToUInt64(s.Substring(s.LastIndexOf(',') + 1), 16);
                e = null;
            }
            catch (Exception e) { this.e = e;}
        }
        public static record GetRecord(string s) {
            record r=new record(s);
            if (r.e != null) r = null;
            return r;
        }
    }

    static void Main(string[] args)
    {
        argparse ap = new argparse("ais1ファイル形式を読み込み、hashでユニーク判別をしてユニークとし、MMSI昇順、時刻昇順でソートしてais2ファイル形式で出力する。ais2ファイル形式を読み込ませてもよい（マージして改めてais2を作る）。");
        ap.ResisterArgs(args);
        List<string> inFiles = ap.getArgs((char)0, "InFile", "入力するais1またはais2ファイル", kind: argparse.Kind.FilesWithWildcard_or_FilesInFolder, canBeMulti: true);
        List<string> outFiles = ap.getArgs('o', "OutFile", "出力するais2ファイル（省略時は最初のInFileの拡張子をais2にしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
        List<string> errFiles = ap.getArgs('e', "ErrorFile", "エラー出力ファイル（省略時はOutFileの拡張子をais2errにしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
        List<string> Lines = ap.getArgs('l', "Lines", "読み込む最大行数（省略時は最後まで）", kind: argparse.Kind.IsInt, canOmit: true);

        if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; }

        string outFile = (outFiles.Count() > 0) ? outFiles[0] : UnexistFilePath.ChangeExt(inFiles[0], "ais2");
        string errFile = (errFiles.Count() > 0) ? errFiles[0] : UnexistFilePath.ChangeExt(outFile, "ais2err");
        int maxLine = (Lines.Count() > 0) ? int.Parse(Lines[0]) : int.MaxValue;
        log = new Log(UnexistFilePath.ChangeExt(outFile, "log"), show: true,datetime:true);

        //まずはinFilesにあるファイル名をinFilesAis1とinFilesAis2に分ける
        List<string> inFilesAis2 = new List<string>();
        List<string> inFilesAis1 = new List<string>();
        List<string> tempFiles = new List<string>();
        foreach (string inFile in inFiles)
            switch (Path.GetExtension(inFile).ToLower())
            {
                case ".ais1":
                    inFilesAis1.Add(inFile);
                    break;
                case ".ais2":
                    inFilesAis2.Add(inFile);
                    break;
                default:
                    log.WriteLine($"異常な拡張子:{inFile}");
                    break;
            }
        //inFilesAis1を読んでソートしtemp.ais2ファイルを作る。tempFilesとinFilesAis2にいれる
        using (MultiFileReader<int> mr = new MultiFileReader<int>(inFilesAis1))
        {
            int line = 0;
            List<record> ais1recordList = new List<record>();
            foreach (string s in mr)
            {
                record r = new record(s);
                if (r.e != null) log.WriteLine($"エラー {mr.OpeningFile}:{mr.Num_LineOfTheFile} {r.e.ToString()}");
                else ais1recordList.Add(r);

                if (++line % 100000 == 0) log.WriteLine($"Reading Line:{line}");

                if (ais1recordList.Count >= 5000000 || mr.EndOfFiles)
                {
                    ais1recordList.Sort();
                    string tempais2filePath = UnexistFilePath.ChangeExt(outFile, ".temp.ais2");
                    using (StreamWriter sw = new StreamWriter(tempais2filePath))
                    {
                        log.WriteLine($"Write:{tempais2filePath}");
                        foreach (record ais1r in ais1recordList) sw.WriteLine(ais1r.s);
                    }
                    inFilesAis2.Add(tempais2filePath);
                    tempFiles.Add(tempais2filePath);
                    ais1recordList.Clear();
                }
            }
        }

        //inFilesAis2にあるais2ファイル群をすべてオープンし、値の小さいものから拾って新たにais2ファイルにストアしていく
        //ただしHashlistにハッシュを登録していって重複するのは出力しない
        using (StreamWriter swOut = new StreamWriter(outFile))
        using (StreamWriter swErr = new StreamWriter(errFile))
        using (MultiFileReader<record> mr = new MultiFileReader<record>(inFilesAis2, record.GetRecord))
        {
            log.WriteLine($"OutFile:{outFile}");
            log.WriteLine($"ErrFile:{errFile}");
            HashSet<UInt64> Hashlist = new HashSet<UInt64>();
            int mmsi = 0;
            int line = 0;
            foreach (string s in mr)
            {
                record r = new record(s);
                if (r.e != null) log.WriteLine($"エラー {mr.OpeningFile}:{mr.Num_LineOfTheFile} {r.e.ToString()}");
                else
                {
                    //もしmmsiが変化したらハッシュクリア
                    if (mmsi != r.MMSI) { mmsi = r.MMSI; Hashlist.Clear(); }
                    if (!Hashlist.Contains(r.Hash))
                    {
                        Hashlist.Add(r.Hash);
                        swOut.WriteLine(r.s);
                    }
                    else swErr.WriteLine("ハッシュ衝突," + r.s);
                }
                if (++line % 100000==0) log.WriteLine($"Writing Line:{line}");
            }
        }
        
        foreach (string s in tempFiles) { File.Delete(s); log.WriteLine($"Delete:{s}"); }
        
        log.WriteLine("Main Finished");
    }
}

