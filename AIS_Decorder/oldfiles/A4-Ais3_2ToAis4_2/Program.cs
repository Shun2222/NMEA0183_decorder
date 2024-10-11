using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridToVectorComponent
{
    class Program
    {
        const string inExt = "ais3_2", outExt = "ais4_2_2";
        static string logFile;

        static void Main(string[] args)
        {
            List<string> inFiles;
            string outFile;//, errFile;

            //とりあえず、inFileはひとつにしている。複数あると計算時間が少なく便利ではあるが、後処理で同一グリッド・MMSIの複数のレコードがあると予想外のことが起こるかもしれない。
            {
                argparse ap = new argparse(inExt + "ファイルを読み込み、ベクトルコンポーネントの値を計算して" + outExt + "ファイルを出力する");
                ap.ResisterArgs(args);
                inFiles = ap.getArgs((char)0, "InFile", "入力する" + inExt + "ファイル", kind: argparse.Kind.ExistFile/*, canBeMulti: true*/);
                List<string> _outFiles = ap.getArgs('o', "OutFile", "出力する" + outExt + "ファイル（省略時は最初のInFileの拡張子を" + outExt + "にしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                //List<string> _errFiles = ap.getArgs('e', "ErrorFile", "エラー出力ファイル（省略時はOutFileの拡張子を" + outExt + "errにしたもの）", kind: argparse.Kind.NotExistFile, canOmit: true);
                List<string> _logFiles = ap.getArgs('g', "LogFile", "ログ出力ファイル（省略時はOutFileの拡張子を" + outExt + "logにしたもの）", canOmit: true);

                if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; }

                outFile = (_outFiles.Count() > 0) ? _outFiles[0] : UnexistFilePath.ChangeExt(inFiles[0], outExt);
                //errFile = (_errFiles.Count() > 0) ? _errFiles[0] : UnexistFilePath.ChangeExt(outFile, outExt + "err");
                logFile = (_logFiles.Count() > 0) ? _logFiles[0] : UnexistFilePath.ChangeExt(outFile, outExt + "log");

            }

            logout("Start Reading");

            using (StreamWriter sw = new StreamWriter(outFile))
                foreach (string inFile in inFiles) using (StreamReader sr = new StreamReader(inFile))
                    {
                        //1行目はそのまま
                        sw.WriteLine(sr.ReadLine());
                        //2行目はヘッダ
                        sr.ReadLine();
                        //sw.WriteLine("Mmsi,DtIdx,LatIdx,LonIdx,ThetaDeg,F,LineCount,curN,curE");
                        sw.WriteLine("DtIdx,LatIdx,LonIdx,phi1,phi2,lambda1,lambda2,curN,curE,cur1,cur2");
                        while (!sr.EndOfStream)
                        {
                            string ss0 = sr.ReadLine();
                            string[] ss = ss0.Split(',');
                            double
                                A = double.Parse(ss[3]),
                                B = double.Parse(ss[4]),
                                C = double.Parse(ss[5]),
                                D = double.Parse(ss[6]),
                                E = double.Parse(ss[7]),
                                F = double.Parse(ss[8]),
                                curN = double.Parse(ss[10]),
                                curE = double.Parse(ss[11]);
                            int COUNT = int.Parse(ss[9]);
                            if (double.IsNaN(curE)) 
                            {
                                //Console.WriteLine(ss0);
                                continue;
                            }

                            //// 海流ベクトルOPの点Pの座標
                            //double
                            //    Xp = (B * E - C * D) / (A * C - B * B),
                            //    Yp = (B * D - A * E) / (A * C - B * B);

                            // コスト等高線楕円の長軸・短軸の方向（どっちがどっちかは不定）
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
                            double
                                cur1 = curE * Cosθ1 + curN * Sinθ1,
                                cur2 = curE * Cosθ2 + curN * Sinθ2;

                            // 長軸：Lambda2
                            if (Lambda1 > Lambda2)
                            {
                                double a = θ1Rad;
                                θ1Rad = θ2Rad;
                                θ2Rad = a;

                                a = Lambda1;
                                Lambda1 = Lambda2;
                                Lambda2 = a;

                                a = cur1;
                                cur1 = cur2;
                                cur2 = a;

                                Cosθ1 = Math.Cos(θ1Rad);
                                Sinθ1 = Math.Sin(θ1Rad);
                                Cosθ2 = Math.Cos(θ2Rad);
                                Sinθ2 = Math.Sin(θ2Rad);
                            }

                            if (Math.Abs(Lambda1) > 0.5)
                            {
                                double F1 = -(D * Cosθ1 + E * Sinθ1) / Lambda1;// 軸方向の海流ベクトル長さ
                                if(F1<0)
                                {
                                    F1 *= -1;
                                    θ1Rad += Math.PI;
                                }
                                double F2 = -(D * Cosθ2 + E * Sinθ2) / Lambda2;// 軸方向の海流ベクトル長さ
                                if (F2 < 0)
                                {
                                    F2 *= -1;
                                    θ2Rad += Math.PI;
                                }
                                //sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", ss[1], ss[2], ss[3], θ1Rad * 180 / Math.PI, F1, Lambda1, curN, curE);
                                sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", 
                                              ss[1], ss[2], ss[3], 
                                              θ1Rad * 180 / Math.PI, 
                                              θ2Rad * 180 / Math.PI, 
                                              Lambda1, Lambda2,
                                              curN, curE, cur1, cur2);
                                if (Lambda2 > 300)
                                {
                                    Console.WriteLine(ss0);
                                }

                            }
                        }
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
            using (StreamWriter sw = new StreamWriter(logFile,append:true)) sw.WriteLine(s);
        }
    }
}
