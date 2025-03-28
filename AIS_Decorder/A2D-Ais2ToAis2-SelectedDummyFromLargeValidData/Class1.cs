using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CurrentEstim
{
    class AIS
    {
        public readonly LatLonDT LatLonDT;
        public readonly UInt32 Mmsi;

        public readonly UInt16 COG10;      // northup,deg
        public readonly byte SOG10;      // knot
        public readonly Coord Vog;    // knot
        public readonly UInt16 Hdg;      // northup,deg
        public bool Valid;

        public AIS(string aisx,StreamWriter swErr=null)
        {
            string[] sa = aisx.Split(',');
            sa = Array.ConvertAll(sa, ss => ss.Trim());
            try
            {
                // 動的データ && under way using engine なら有効
                if (sa[1] == "Dyn" && int.Parse(sa[3]) == 0)
                {
                    Mmsi = UInt32.Parse(sa[2]);

                    DateTime dt = DateTime.SpecifyKind(DateTime.Parse(sa[0]), DateTimeKind.Utc);    // 入力ファイルの日時はUTCとする
                    LatLonDT = new LatLonDT(Single.Parse(sa[4]), Single.Parse(sa[5]), dt);
                    SOG10 = (byte)(double.Parse(sa[7]) * 10);
                    COG10 = (UInt16)(double.Parse(sa[6]) * 10);
                    Hdg = UInt16.Parse(sa[8]);
                    Vog = new Coord((Single)(0.1 * SOG10 * Math.Cos(0.1 * COG10 * Math.PI / 180)), (Single)(0.1 * SOG10 * Math.Sin(0.1 * COG10 * Math.PI / 180)));
                    Valid = true;

                }
                else
                {
                    Valid = false;
                    if (swErr != null) swErr.WriteLine("静的か力行以外," + aisx);
                }
            }
            catch (Exception e)
            {
                Program.logout(e.Message + "\r\n" + aisx);
                Valid = false;
                if (swErr != null) swErr.WriteLine("不明なエラー," + aisx);
            }
        }
    }
}