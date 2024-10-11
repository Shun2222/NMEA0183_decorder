using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


public enum enAISResult
{
    IsSentenceErr = -3,
    IsSequentialMessageInconsistentErr = -2,
    IsDecapsulatingErr = -1,
    ToBeContinue = 0,
    IsDynamic = 1,
    IsStatic = 2,
    IsOtherSentence = -4,
    IsCheckSumErr = -5
}
public struct staticData
{
    public int IMO_No;
    public string Name;
    public byte TypeOfShipAndCargo;
    public int LOA;     // meter
    public int Breadth; // meter
    public int AntennaFromFore; // meter
    public int AntennaFromLeft; // meter
    public string ETA;  //UTC "MM/DD hh:mm"
    public double draught; // meter
    public string Destination;

}
public struct dynamicData
{
    public byte NavigationStatus;
    public string strNavigationStatus;
    public double? TurnRate; // deg/min
    public double? SOG; // knots
    public bool PositionIsAccurate;
    public double? Latitude;
    public double? Longitude;
    public double? COG;
    public int? Heading;


}
public struct AISCode
{
    public enAISResult Result;
    public string encapsulatedString;
    public int MMSI;
    public staticData StaticData;
    public dynamicData DynamicData;
    public UInt64 hash { get { return HashMD5.calcHash(encapsulatedString); } }
}




class AISDecoding
{
    byte q_num1 = 0, q_num2 = 0, q_seq_id = 0;
    string q_encap = "";

    Regex re = new Regex(@"!(?<forCheck>AIVD[OM],(?<num1>\d),(?<num2>\d),(?<seq_id>\d?),[AB]?,(?<encap>[^,]+),(?<fillbits>[0-5]))\*(?<checksum>[0-9a-fA-F]{2})");

    public AISCode setSentence(string sentence)
    {
        AISCode ret = new AISCode();
        byte num1, num2, seq_id, fillbits;
        string encap, checksum, forCheck;

        // センテンスの解読
        Match m = re.Match(sentence);
        if (!m.Success)
        {
            ret.Result = enAISResult.IsSentenceErr;
            return ret;
        }
        num1 = byte.Parse(m.Groups["num1"].Value);
        num2 = byte.Parse(m.Groups["num2"].Value);
        seq_id = m.Groups["seq_id"].Value == "" ? (byte)0 : byte.Parse(m.Groups["seq_id"].Value);
        encap = m.Groups["encap"].Value;
        fillbits = byte.Parse(m.Groups["fillbits"].Value);
        checksum = m.Groups["checksum"].Value;
        forCheck = m.Groups["forCheck"].Value;

        //チェックサムのチェック
        if (CheckSum(forCheck) != checksum)
        {
            ret.Result = enAISResult.IsCheckSumErr;
            return ret;
        }

        //複数行にわたるメッセージの受信処理
        if (num1 != 1)
        {
            if (num2 == 1)  // !AIVDM,3,1,2,…
            {
                q_num1 = num1;
                q_num2 = num2;
                q_seq_id = seq_id;
                q_encap = encap;
                ret.Result = enAISResult.ToBeContinue;
                return ret;
            }
            if (num2 < num1)  // !AIVDM,3,2,2,…
            {
                if (q_seq_id == seq_id && q_num1 == num1 && q_num2 + 1 == num2)
                {
                    q_num2 = num2;
                    q_encap += encap;
                    ret.Result = enAISResult.ToBeContinue;
                    return ret;
                }
                ret.Result = enAISResult.IsSequentialMessageInconsistentErr;
                return ret;
            }
            if (num2 != num1)
            {
                ret.Result = enAISResult.IsSequentialMessageInconsistentErr;
                return ret;
            }
            //num2==num1  // !AIVDM,3,3,2,…
            encap = q_encap + encap;
        }


        ret.encapsulatedString = encap;

        // encapsulatedStringの数値化 → bits[]
        byte[] bits = new byte[encap.Length * 6 - fillbits];
        for (int i = 0; i < encap.Length; i++)
        {
            int code = @"0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVW`abcdefghijklmnopqrstuvw".IndexOf(encap.Substring(i, 1));
            if (code == -1)
            {
                ret.Result = enAISResult.IsDecapsulatingErr;
                return ret;
            }
            for (int j = 0; j < 6; j++)
                if (i * 6 + j < bits.Length) bits[i * 6 + j] = (code & 1 << (5 - j)) != 0 ? (byte)1 : (byte)0;
        }

        // メッセージIDの解読
        ulong u;
        Exception e = null;
        switch (bitsToUlong(bits, 0, 6, ref e))
        {
            case 1:
            case 2:
            case 3:
                //動的メッセージ
                if (bits.Length != 168)
                {
                    ret.Result = enAISResult.IsDecapsulatingErr;
                    break;
                }
                //MMSI
                ret.MMSI = (int)bitsToUlong(bits, 8, 30, ref e);
                //NavigationStatus
                ret.DynamicData.NavigationStatus = (byte)bitsToUlong(bits, 38, 4, ref e);
                ret.DynamicData.strNavigationStatus = new string[] {
                        "underway using engine",
                        "at anchor",
                        "not under command",
                        "restricted manoeuvrability",
                        "Constrained by her draught",
                        "moored",
                        "Aground",
                        "Fishing",
                        "Under way sailing",
                        "reserved for HSC",
                        "reserved for WIG",
                        "reserved",
                        "reserved",
                        "reserved",
                        "reserved",
                        "not defined"
                    }[ret.DynamicData.NavigationStatus];
                //TurnRate
                u = bitsToUlong(bits, 42, 8, ref e);
                if (u == 128) ret.DynamicData.TurnRate = null;
                else if (u == 127) ret.DynamicData.TurnRate = null;
                else if (u == 129) ret.DynamicData.TurnRate = null;
                else if (u < 127) ret.DynamicData.TurnRate = Math.Pow((double)u / 4.733, 2.0);
                else ret.DynamicData.TurnRate = -Math.Pow((double)(256 - u) / 4.733, 2.0);
                //SOG
                u = bitsToUlong(bits, 50, 10, ref e);
                ret.DynamicData.SOG = u == 1023 ? null : (double?)u / 10;
                //PositionIsAccurate
                ret.DynamicData.PositionIsAccurate = (bits[60] != 0);
                //Longitude
                u = bitsToUlong(bits, 61, 28, ref e);
                if (u == 0x6791ac0) ret.DynamicData.Longitude = null;
                else if (u < 0x8000000) ret.DynamicData.Longitude = (double)u / 10000 / 60;
                else ret.DynamicData.Longitude = -(double)(0x10000000 - u) / 10000 / 60;
                //Latitude
                u = bitsToUlong(bits, 89, 27, ref e);
                if (u == 0x3412140) ret.DynamicData.Latitude = null;
                else if (u < 0x4000000) ret.DynamicData.Latitude = (double)u / 10000 / 60;
                else ret.DynamicData.Latitude = -(double)(0x8000000 - u) / 10000 / 60;
                //COG
                u = bitsToUlong(bits, 116, 12, ref e);
                ret.DynamicData.COG = u >= 3600 ? null : (double?)u / 10;
                //Heading
                u = bitsToUlong(bits, 128, 9, ref e);
                ret.DynamicData.Heading = u >= 360 ? null : (int?)u;
                ret.Result = enAISResult.IsDynamic;
                break;

            case 5:
                // 静的メッセージ
                if (bits.Length != 424)
                {
                    ret.Result = enAISResult.IsDecapsulatingErr;
                    break;
                }
                //MMSI
                ret.MMSI = (int)bitsToUlong(bits, 8, 30, ref e);
                //IMO No
                ret.StaticData.IMO_No = (int)bitsToUlong(bits, 40, 30, ref e);
                // Ship Name
                ret.StaticData.Name = bitsToString(bits, 112, 120, ref e);
                // Type of Ship
                ret.StaticData.TypeOfShipAndCargo = (byte)bitsToUlong(bits, 232, 8, ref e);
                //Dimension
                int A = (int)bitsToUlong(bits, 240, 9, ref e),
                    B = (int)bitsToUlong(bits, 249, 9, ref e),
                    C = (int)bitsToUlong(bits, 258, 6, ref e),
                    D = (int)bitsToUlong(bits, 264, 6, ref e);
                //LOA
                ret.StaticData.LOA = A + B;
                //Breadth
                ret.StaticData.Breadth = C + D;
                //AntennaFromFore
                ret.StaticData.AntennaFromFore = A;
                //AntennaFromLeft
                ret.StaticData.AntennaFromLeft = C;
                //ETA
                int month = (int)bitsToUlong(bits, 274, 4, ref e);
                int day = (int)bitsToUlong(bits, 278, 5, ref e);
                int hr = (int)bitsToUlong(bits, 283, 5, ref e);
                int min = (int)bitsToUlong(bits, 288, 6, ref e);
                if (1 <= month && month <= 12 && 1 <= day && day <= 31 && 0 <= hr && hr <= 23 && 0 <= min && min <= 59)
                    ret.StaticData.ETA += month.ToString("00") + "/" + day.ToString("00") + " " + hr.ToString("00") + ":" + min.ToString("00");
                else ret.StaticData.ETA = null;
                //Draught
                ret.StaticData.draught = 0.1 * (double)bitsToUlong(bits, 294, 8, ref e);
                //Destination
                ret.StaticData.Destination = bitsToString(bits, 302, 120, ref e);
                ret.Result = enAISResult.IsStatic;
                break;

            case 18:
                //動的メッセージtypeB
                if (bits.Length != 168)
                {
                    ret.Result = enAISResult.IsDecapsulatingErr;
                    break;
                }
                //MMSI
                ret.MMSI = (int)bitsToUlong(bits, 8, 30, ref e);
                //NavigationStatus
                ret.DynamicData.NavigationStatus = 17;
                ret.DynamicData.strNavigationStatus = "Type B";
                //TrunRate
                ret.DynamicData.TurnRate = null;
                //SOG
                u = bitsToUlong(bits, 46, 10, ref e);
                ret.DynamicData.SOG = u == 1023 ? null : (double?)u / 10;
                //PositionIsAccurate
                ret.DynamicData.PositionIsAccurate = (bits[56] != 0);
                //Longitude
                u = bitsToUlong(bits, 57, 28, ref e);
                if (u == 0x6791ac0) ret.DynamicData.Longitude = null;
                else if (u < 0x8000000) ret.DynamicData.Longitude = (double)u / 10000 / 60;
                else ret.DynamicData.Longitude = -(double)(0x10000000 - u) / 10000 / 60;
                //Latitude
                u = bitsToUlong(bits, 85, 27, ref e);
                if (u == 0x3412140) ret.DynamicData.Latitude = null;
                else if (u < 0x4000000) ret.DynamicData.Latitude = (double)u / 10000 / 60;
                else ret.DynamicData.Latitude = -(double)(0x8000000 - u) / 10000 / 60;
                //COG
                u = bitsToUlong(bits, 112, 12, ref e);
                ret.DynamicData.COG = u >= 3600 ? null : (double?)u / 10;
                //Heading
                u = bitsToUlong(bits, 124, 9, ref e);
                ret.DynamicData.Heading = u >= 360 ? null : (int?)u;
                ret.Result = enAISResult.IsDynamic;
                break;

            default:
                ret.Result = enAISResult.IsOtherSentence;
                break;
        }
        if (e != null) ret.Result = enAISResult.IsDecapsulatingErr;
        return ret;
    }


    private ulong bitsToUlong(byte[] bits, int startIndex, int length, ref Exception e)
    {
        if (startIndex + length > bits.Length && e == null)
        {
            e = new IndexOutOfRangeException();
            return 0;
        }
        ulong ret = 0;
        for (int i = startIndex; i < startIndex + length; i++)
        {
            ret <<= 1;
            ret += bits[i] != 0 ? (ulong)1 : (ulong)0;
        }
        return ret;
    }
    private string bitsToString(byte[] bits, int startIndex, int length, ref Exception e)
    {
        if (startIndex + length > bits.Length && e == null)
        {
            e = new IndexOutOfRangeException();
            return "IndexOutOfRangeException";
        }
        if (length % 6 != 0) return "Error";
        string ret = "";
        for (int i = startIndex; i < startIndex + length; i += 6)
        {
            byte chr = 0;
            for (int j = 0; j < 6; j++)
            {
                chr <<= 1;
                chr += bits[i + j] != 0 ? (byte)1 : (byte)0;
            }
            ret += @"@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_ |""#$%&'()*+,-./0123456789:;<=>?".Substring(chr, 1);
        }
        return ret;
    }
    private static string CheckSum(string s)
    {
        byte b = 0;
        foreach (char c in s) b ^= Convert.ToByte(c);  // ^はべき乗ではなくxor
        return b.ToString("X2");
    }
}


class HashMD5
{
    static System.Security.Cryptography.MD5 md5;

    //--------------------------------------------------------------------
    /// <summary>  指定された文字列をMD5でハッシュ化し、文字列として返す
    /// </summary>
    /// <param name="srcStr">入力文字列</param>
    /// <returns>入力文字列のMD5ハッシュ値</returns>
    //--------------------------------------------------------------------
    public static string calcMd5(string srcStr)
    {

        if (md5 == null) md5 = System.Security.Cryptography.MD5.Create();

        // md5ハッシュ値を求める
        byte[] srcBytes = System.Text.Encoding.UTF8.GetBytes(srcStr);
        byte[] destBytes = md5.ComputeHash(srcBytes);

        // 求めたmd5値を文字列に変換する
        System.Text.StringBuilder destStrBuilder;
        destStrBuilder = new System.Text.StringBuilder();
        foreach (byte curByte in destBytes)
        {
            destStrBuilder.Append(curByte.ToString("x2"));
        }

        // 変換後の文字列を返す
        return destStrBuilder.ToString();
    }

    //--------------------------------------------------------------------
    /// <summary>  指定された文字列をMD5でハッシュ化し、下位64bitをUInt64で返す
    /// </summary>
    /// <param name="srcStr">入力文字列</param>
    /// <returns>入力文字列のMD5ハッシュ値下位64bit</returns>
    //--------------------------------------------------------------------
    public static UInt64 calcHash(string srcStr)
    {
        if (md5 == null) md5 = System.Security.Cryptography.MD5.Create();

        // md5ハッシュ値を求める
        byte[] srcBytes = System.Text.Encoding.UTF8.GetBytes(srcStr);
        byte[] destBytes = md5.ComputeHash(srcBytes);

        // 求めたmd5値の下位8byteをuint64に変換する
        UInt64 u = 0;
        for (int i = 0; i < destBytes.Count(); i++)
        {
            u <<= 8;
            u += destBytes[i];
        }
        return u;
    }
}


