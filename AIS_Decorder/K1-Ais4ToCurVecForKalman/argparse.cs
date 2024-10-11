using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;


/// <summary>
/// exeにつけられた引数を取得するクラス。
/// </summary>
class argparse
{
    Dictionary<char, List<string>> argValues = new Dictionary<char, List<string>>();
    Dictionary<char, argDesc> argDescs = new Dictionary<char, argDesc>();
    class argDesc
    {
        public string itemName;
        public string description;
    }

    string description;


    List<string> errMes = new List<string>();

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="description">プログラムの説明文</param>
    public argparse(string description)
    {
        this.description = description;
    }
    /// <summary>
    /// exe引数の登録
    /// </summary>
    /// <param name="args">exeの引数args</param>
    public void ResisterArgs(string[] args)
    {
        //Dictionary argValuesに、ハイフンの後の一文字をキーに、それに続く引数を値にして登録。
        //例えば-o a.txt b.txtなら、ArgValues.Add('o', new List<string>({"a.txt", "b.txt"}))

        char nowLabel = (char)0;
        argValues.Add(nowLabel, new List<string>());

        foreach (string ss in args)
        {
            string s = ss;
            if (s[0] == '-' || s[0] == '/')
            {
                if (s.Length >= 2) nowLabel = s[1]; else nowLabel = (char)0;
                if (!argValues.ContainsKey(nowLabel)) argValues.Add(nowLabel, new List<string>());
                if (s.Length >= 3) s = s.Substring(2); else continue;
            }
            argValues[nowLabel].Add(s);
        }
    }
    /// <summary>
    /// 子引数の許可された種類
    /// </summary>
    public enum Kind
    {
        NoSpecific = 0,
        ExistFile,
        NotExistFile,
        ExistFolder,
        NotExistFolder,
        ExistFileOrFolder,
        NotExistFileOrFolder,
        FilesWithWildcard_or_FilesInFolder,
        IsInt,
        IsNumeric
    }
    /// <summary>
    /// 子引数の許可された数
    /// </summary>
    public enum Quantity
    {
        Null_Or_0,
        Null_Or_1,
        Null_Or_LE1,
        Null_Or_GE0,
        Null_Or_GE1,
        One,
        GE1
    }
    /// <summary>
    /// 引数に対する説明と制約をつけるとともに、引数を取得
    /// </summary>
    /// <param name="label">ハイフンの後の一文字。ハイフンなしでexeの後に直接書く引数を取得する際は(char)0をいれる</param>
    /// <param name="itemName">引数名</param>
    /// <param name="description">親引数の説明</param>
    /// <param name="kind">子引数の許可された種類</param>
    /// <param name="quantity">子引数の許可された個数（FilesWithWildcard_or_FilesInFolderについては結果に対して）</param>
    /// <param name="regex">正規表現</param>
    /// <returns>子引数のリスト</returns>
    public List<string> getArgs(
        char label,
        string itemName,
        string description,
        Kind kind = Kind.NoSpecific,
        Quantity quantity = Quantity.Null_Or_1,
        string regex = null)
    {
        if (regex == "") regex = null;
        argDesc ad = new argDesc();
        ad.itemName = itemName;
        string desc = "";
        switch (kind)
        {
            case Kind.NoSpecific:
                break;
            case Kind.ExistFile:
                desc += "存在するファイルパス,";
                break;
            case Kind.NotExistFile:
                desc += "存在しないファイルパス,";
                break;
            case Kind.ExistFolder:
                desc += "存在するフォルダ,";
                break;
            case Kind.NotExistFolder:
                desc += "存在しないフォルダ,";
                break;
            case Kind.ExistFileOrFolder:
                desc += "存在するファイルまたはフォルダ,";
                break;
            case Kind.NotExistFileOrFolder:
                desc += "存在しないファイルまたはフォルダ,";
                break;
            case Kind.FilesWithWildcard_or_FilesInFolder:
                desc += "ファイル（*,?可）またはフォルダ（中のファイル対象）,";
                break;
            case Kind.IsInt:
                desc += "整数,";
                break;
            case Kind.IsNumeric:
                desc += "数値,";
                break;
        }
        switch (quantity)
        {
            case Quantity.Null_Or_0:
                break;
            case Quantity.Null_Or_1:
                desc += "1つのみ記載,省略可,";
                break;
            case Quantity.Null_Or_LE1:
                desc += "0個か1個記載,省略可,";
                break;
            case Quantity.Null_Or_GE0:
                desc += "0個以上記載,省略可,";
                break;
            case Quantity.Null_Or_GE1:
                desc += "1個以上記載,省略可,";
                break;
            case Quantity.One:
                desc += "1つのみ記載,省略不可,";
                break;
            case Quantity.GE1:
                desc += "1個以上記載,省略不可,";
                break;
        }


        if (regex != null) desc += "正規表現(" + regex + "),";
        ad.description = description + (desc == "" ? "" : "(" + desc.Trim(',') + ")");

        argDescs.Add(label, ad);

        List<string> r = argValues.ContainsKey(label) ? new List<string>() : null;
        if (r != null)
            foreach (string s in argValues[label])
            {
                int i; double d; bool b;
                switch (kind)
                {
                    case Kind.NoSpecific:
                        r.Add(s);
                        break;
                    case Kind.ExistFile:
                        if (!File.Exists(s)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : ファイルが存在しません:" + s);
                        r.Add(s);
                        break;
                    case Kind.NotExistFile:
                        if (File.Exists(s)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : ファイルが存在します:" + s);
                        r.Add(s);
                        break;
                    case Kind.ExistFolder:
                        if (!Directory.Exists(s)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : フォルダが存在しません:" + s);
                        r.Add(s);
                        break;
                    case Kind.NotExistFolder:
                        if (Directory.Exists(s)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : フォルダが存在します:" + s);
                        r.Add(s);
                        break;
                    case Kind.ExistFileOrFolder:
                        if (!(File.Exists(s) || Directory.Exists(s))) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : ファイルまたはフォルダが存在しません:" + s);
                        r.Add(s);
                        break;
                    case Kind.NotExistFileOrFolder:
                        if (File.Exists(s) || Directory.Exists(s)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : ファイルまたはフォルダが存在します:" + s);
                        r.Add(s);
                        break;
                    case Kind.FilesWithWildcard_or_FilesInFolder:
                        foreach (string f in getFiles(s)) r.Add(f);
                        break;
                    case Kind.IsInt:
                        b = int.TryParse(s, out i);
                        b &= double.TryParse(s, out d);
                        if (!b || i != d) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : 整数に変換できません:" + s);
                        r.Add(s);
                        break;
                    case Kind.IsNumeric:
                        if (!double.TryParse(s, out d)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : 数値に変換できません:" + s);
                        r.Add(s);
                        break;
                    default:
                        r.Add(s);
                        break;
                }
                if (regex != null) if (!Regex.IsMatch(s, regex)) errMes.Add((label == (char)0 ? itemName : "-" + label) + " : 正規表現に合致しません:" + s);
            }

        string tmp = (label == (char)0 ? itemName : "-" + label) + " : 引数の数が不正です";
        switch (quantity)
        {
            case Quantity.Null_Or_0:
                if (!(r == null || r.Count == 0)) errMes.Add(tmp);
                break;
            case Quantity.Null_Or_1:
                if (!(r == null || r.Count == 1)) errMes.Add(tmp);
                break;
            case Quantity.Null_Or_LE1:
                if (!(r == null || r.Count <= 1)) errMes.Add(tmp);
                break;
            case Quantity.Null_Or_GE0:
                break;
            case Quantity.Null_Or_GE1:
                if (!(r == null || r.Count >= 1)) errMes.Add(tmp);
                break;
            case Quantity.One:
                if (!(r != null && r.Count == 1)) errMes.Add(tmp);
                break;
            case Quantity.GE1:
                if (!(r != null && r.Count >= 1)) errMes.Add(tmp);
                break;
            default:
                break;
        }

        return r;
    }

    public void AddError(string ErrorDescription)
    {
        errMes.Add(ErrorDescription);
    }

    public bool HasError { get { return argValues.ContainsKey('?') || errMes.Count > 0; } }
    public string ErrorOut()
    {
        string s = Path.GetFileName(Assembly.GetExecutingAssembly().Location) + "\r\n\r\nDescription:\r\n" + description + "\r\n\r\nUsage:\r\n";
        s += Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        foreach (char label in argDescs.Keys) if (label == (char)0) s += " " + argDescs[label].itemName;
        foreach (char label in argDescs.Keys) if (label != (char)0) s += " -" + label + " " + argDescs[label].itemName;
        s += "\r\n\r\nArguments:\r\n";
        foreach (char label in argDescs.Keys) if (label == (char)0) s += argDescs[label].itemName + " : " + argDescs[label].description + "\r\n";
        foreach (char label in argDescs.Keys) if (label != (char)0) s += "-" + label + " " + argDescs[label].itemName + " : " + argDescs[label].description + "\r\n";

        if (!argValues.ContainsKey('?'))
        {
            s += "\r\nError:\r\n";
            foreach (string err in errMes) s += err + "\r\n";
        }
        return s;
    }

    /// <summary>
    /// ワイルドカード付ファイル名やフォルダ名から、ファイル名を展開する
    /// </summary>
    static string[] getFiles(string pathWithWildcardsOrFolder)
    {

        string path, file;
        if (Directory.Exists(pathWithWildcardsOrFolder))
        {
            path = pathWithWildcardsOrFolder;
            file = "*";
        }
        else
        {
            path = Path.GetDirectoryName(pathWithWildcardsOrFolder);
            file = Path.GetFileName(pathWithWildcardsOrFolder);
        }
        if (path == "") path = ".";
        string[] files = Directory.GetFiles(path, file);

        return files;
    }
}

class UnexistFilePath
{
    public static string ChangeExt(string path, string ext)
    {
        if (ext != "" && ext[0] != '.') ext = "." + ext;
        string kouho;
        for (int i = 0; ; i++)
        {
            kouho = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + string.Format("{0:;(#)}", -i) + ext);
            if (!File.Exists(kouho)) break;
        }
        return kouho;
    }
    public static string UnchangeExt(string path)
    {
        return ChangeExt(path, Path.GetExtension(path));
    }
}

