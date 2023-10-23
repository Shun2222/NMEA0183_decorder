using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// 対水船速　VBWとVHWがあるが，VHWは基本的に方位情報がないため，VBWのみを使う

// $--VBW,x.x,x.x,A, x.x, x.x, A*hh
// 実データ：(2015 / 01 / 01 05:00:04.656,iLOG,$VDVBW,14.1,, A,,, V*5C)
// 1) Longitudinal water speed, "-" means astern 縦水速
// 2) Transverse water speed, "-" means port 横水速
// 3) Status, A = data validv Aの場合、データが有効
// 4) Longitudinal ground speed, "-" means astern 縦地上速度
// 5) Transverse ground speed, "-" means port 横地上速度
// 6) Status, A = data valid
// 7) Checksum

// $--VHW,x.x,T, x.x, M, x.x, N, x.x, K*hh
// 1) Degress True　真北から時計または反時計周りにどれくらい旋回しているか
// 2) T = True 時計回り
// 3) Degrees Magnetic 磁北を基準とした角度
// 4) M = Magnetic　
// 5) Knots(speed of vessel relative to the water)船速
// 6) N = Knots　単位
// 7) Kilometers(speed of vessel relative to the water)
// 8) K = Kilometres　単位
// 9) Checksum データの各バイトの合計

// Checksumについて
// $-*内に対して0x00を初期値とし，左から見ぢへ各文字コードのXORをとったもの

/*
$--GGA,hhmmss.ss,llll.ll,a, yyyyy.yy, a, x, xx, x.x, x.x, M, x.x, M, x.x, xxxx*hh
Time(UTC): UTC（協定世界時）での現在の時刻を示します。例：121301.500（12時13分1.5秒UTC）。
Latitude: 緯度を示します。一般的に度分秒形式や10進数形式で表されます。
N or S (North or South): 緯度が北半球（N）または南半球（S）を示します。
Longitude: 経度を示します。一般的に度分秒形式や10進数形式で表されます。
E or W (East or West): 経度が東経（E）または西経（W）を示します。
GPS Quality Indicator: GPSの品質を示します。0は「fix not available」、1は「GPS fix」、2は「Differential GPS fix」を表します。
Number of satellites in view: 視野内の衛星の数を示します。00から12までの数値で表されます。
Horizontal Dilution of Precision (HDOP): 水平精度の拡散度合いを示します。値が小さいほど、精度が高いことを示します。
Antenna Altitude: アンテナの高度を平均海面からの距離で示します。
Units of antenna altitude: アンテナ高度の単位を示します（一般的にはメートル）。
Geoidal separation: WGS - 84楕円体と平均海面（ジオイド）の間の高度差を示します。"-"は平均海面が楕円体よりも下にあることを示します。
Units of geoidal separation: ジオイド分離の単位を示します（一般的にはメートル）。
Age of differential GPS data: 差分GPSデータの年齢を示します。最後のSC104タイプ1または9の更新からの経過時間（DGPSを使用しない場合はnull）。
Differential reference station ID: 差分GPSの基準局IDを示します。
Checksum: データの整合性を確認するためのチェックサム。
*/

/*
$--HDT,x.x,T* hh
1) Heading Degrees, true
2) T = True
3) Checksum
*/

/*VTG 対地ベクトル
1) Track Degrees
2) T = True
3) Track Degrees
4) M = Magnetic
5) Speed Knots
6) N = Knots
7) Speed Kilometers Per Hour
8) K = Kilometres Per Hour
9) Checksum
*/

/*RMC 対地ベクトル
1) Track Degrees
2) T = True
3) Track Degrees
4) M = Magnetic
5) Speed Knots
6) N = Knots
7) Speed Kilometers Per Hour
8) K = Kilometres Per Hour
9) Checksum
*/


public enum enNMEAResult
{
    IsSentenceErr = -3,
    IsVBW = 1,
    IsGGA = 2,
    IsHDT = 3,
    IsVTG = 4,
    IsCheckSumErr = -5
}

public struct VBWData 
{
    public double LonWaterSpeed;
    public double TraWaterSpeed;
}
public struct GGAData 
{
    public double Lat;
    public double Lon;
}

public struct HDTData 
{
    public double HeadDeg;
}

public struct VTGData 
{
    public double HeadDeg;
    public double GroundSpeed;
}

public struct NMEACode
{
    public enNMEAResult Result;
    public string encapsulatedString;
    public VBWData vbwData; 
    public GGAData ggaData; 
    public HDTData hdtData; 
    public VTGData vtgData; 
}

class NMEADecoding
{
    public NMEACode setSentence(string sentence)
    {
        NMEACode ret = new NMEACode();
        ret = VBWCheck(sentence);
        if (ret.Result == enNMEAResult.IsVBW) return ret;
        ret = GGACheck(sentence);
        if (ret.Result == enNMEAResult.IsGGA) return ret;
        ret = HDTCheck(sentence);
        if (ret.Result == enNMEAResult.IsHDT) return ret;
        ret = VTGCheck(sentence);
        if (ret.Result == enNMEAResult.IsVTG) return ret;

        return ret;
    }
    public NMEACode VBWCheck(string sentence)
    {
        //Regex re = new Regex(@"\$(?<forCheck>..VBW,(?<num1>\d + (\.\d+)?),(?<num2>\d),A,(?<num3>\d),(?<num4>\d),.(?<fillbits>[0-5]))\*(?<checksum>[0-9a-fA-F]{2})");
        Regex re = new Regex(@"\$(?<forCheck>..VBW,(?<num1>[\d.]*),(?<num2>[\d.]*),A,(?<num3>[\d.]*),(?<num4>[\d.]*),.)\*(?<checksum>[0-9a-fA-F]{2})");
        NMEACode ret = new NMEACode();
        double num1, num2;
        string checksum, forCheck;

        // センテンスの解読
        Match m = re.Match(sentence);
        if (!m.Success)
        {
            ret.Result = enNMEAResult.IsSentenceErr;
            return ret;
        }
        if (!double.TryParse(m.Groups["num1"].Value, out num1))
        {
            num1 = -999;
        }
        if (!double.TryParse(m.Groups["num2"].Value, out num2))
        {
            num2 = -999;
        }

        checksum = m.Groups["checksum"].Value;
        forCheck = m.Groups["forCheck"].Value;

        //チェックサムのチェック
        if (CheckSum(forCheck) != checksum)
        {
            ret.Result = enNMEAResult.IsCheckSumErr;
            return ret;
        }

        ret.Result = enNMEAResult.IsVBW;
        VBWData vbwData = new VBWData();
        vbwData.LonWaterSpeed = num1;
        vbwData.TraWaterSpeed = num2;
        ret.vbwData = vbwData;
        return ret;
    }
    private static string CheckSum(string s)
    {
        byte b = 0;
        foreach (char c in s) b ^= Convert.ToByte(c);  // ^はべき乗ではなくxor
        return b.ToString("X2");
    }
    public NMEACode GGACheck(string sentence)
    {
        string pattern = @"^\$(?<forCheck>..GGA,*,(?<num1>[\d.]*),(?<str1>[N|S]{1}),(?<num2>[\d.]*),(?<str2>[E|W]{1}),*,*,*,*,*,*,*,*,*,)\*(?<checksum>[0-9a-fA-F]{2})";
        Regex re = new Regex(pattern);

        NMEACode ret = new NMEACode();
        double num1, num2;
        string str1, str2, checksum, forCheck;

        // センテンスの解読
        Match m = re.Match(sentence);
        if (!m.Success)
        {
            ret.Result = enNMEAResult.IsSentenceErr;
            return ret;
        }
        if (!double.TryParse(m.Groups["num1"].Value, out num1))
        {
            num1 = -999;
        }
        if (!double.TryParse(m.Groups["num2"].Value, out num2))
        {
            num2 = -999;
        }

        str1 = m.Groups["str1"].Value;
        str2 = m.Groups["str2"].Value;
        if (str1 == "S") num1 *= -1;
        if (str2 == "W") num2 *= -1;

        checksum = m.Groups["checksum"].Value;
        forCheck = m.Groups["forCheck"].Value;

        //チェックサムのチェック
        if (CheckSum(forCheck) != checksum)
        {
            ret.Result = enNMEAResult.IsCheckSumErr;
            return ret;
        }

        ret.Result = enNMEAResult.IsGGA;
        GGAData ggaData = new GGAData();
        ggaData.Lat = num1;
        ggaData.Lon = num2;
        ret.ggaData = ggaData;
        return ret;
    }
    public NMEACode HDTCheck(string sentence)
    {
        string pattern = @"^\$(?<forCheck>..HDT,*,(?<num1>[\d.]*),T)\*(?<checksum>[0-9a-fA-F]{2})";
        Regex re = new Regex(pattern);

        NMEACode ret = new NMEACode();
        double num1;
        string checksum, forCheck;

        // センテンスの解読
        Match m = re.Match(sentence);
        if (!m.Success)
        {
            ret.Result = enNMEAResult.IsSentenceErr;
            return ret;
        }
        if (!double.TryParse(m.Groups["num1"].Value, out num1))
        {
            num1 = -999;
        }

        checksum = m.Groups["checksum"].Value;
        forCheck = m.Groups["forCheck"].Value;

        //チェックサムのチェック
        if (CheckSum(forCheck) != checksum)
        {
            ret.Result = enNMEAResult.IsCheckSumErr;
            return ret;
        }

        ret.Result = enNMEAResult.IsHDT;
        HDTData hdtData = new HDTData();
        hdtData.HeadDeg = num1;
        ret.hdtData = hdtData;
        return ret;
    }

    public NMEACode VTGCheck(string sentence) 
    {
    

        Regex re = new Regex(@"\$(?<forCheck>..VTG,(?<num1>[\d.]*),(?<str1>[T]*),(?<num2>[\d.]*),(?<str2>[T]*),(?<num3>[\d.]*),(?<str3>[N]*),(?<num4>[\d.]*),(?<str4>[K]*))\*(?<checksum>[0-9a-fA-F]{2})");
        //Regex re = new Regex(@"\$(?<forCheck>..VTG,(?<num1>[0-9.]*),(?<str1>[T]*),(?<num2>[0-9.]*),(?<str2>[N]*),(?<num3>[0-9.]*),(?<str3>[K]*))\*(?<checksum>[0-9a-fA-F]{2})");

        NMEACode ret = new NMEACode();
        double headDeg, groundSpeed;
        string str1, str2, str3, str4, checksum, forCheck;

        // センテンスの解読
        Match m = re.Match(sentence);
        str1 = m.Groups["str1"].Value;
        str2 = m.Groups["str2"].Value;
        str3 = m.Groups["str3"].Value;
        str4 = m.Groups["str4"].Value;

        checksum = m.Groups["checksum"].Value;
        forCheck = m.Groups["forCheck"].Value;
        if (!m.Success)
        {
            ret.Result = enNMEAResult.IsSentenceErr;
            return ret;
        }

        if (!double.TryParse(m.Groups["num1"].Value, out headDeg))
        {
            if (!double.TryParse(m.Groups["num2"].Value, out headDeg))
            {
                headDeg = -999;
            }
        }

        bool isNot = true;
        if (!double.TryParse(m.Groups["num3"].Value, out groundSpeed))
        {
            isNot = false;
            if (!double.TryParse(m.Groups["num4"].Value, out groundSpeed))
            {
                groundSpeed = -999;
            }
        }
        if (!isNot && groundSpeed!=-999) 
        {

            groundSpeed *= 0.539956;
        }


        //チェックサムのチェック
        if (CheckSum(forCheck) != checksum)
        {
            ret.Result = enNMEAResult.IsCheckSumErr;
            return ret;
        }

        ret.Result = enNMEAResult.IsVTG;
        VTGData vtgData = new VTGData();
        vtgData.HeadDeg = headDeg;
        vtgData.GroundSpeed = groundSpeed;
        ret.vtgData = vtgData;
        return ret;
    }
}

