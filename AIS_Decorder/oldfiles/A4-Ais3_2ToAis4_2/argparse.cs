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
/// △○.exe -? や △○.exe /? でプログラムの説明を表示したり、
/// △○.exe a.txt -o b.txt -i 5 -d 3 7 6.5 -t 2001/05/03 のような引数を取得する。
///usage:
///argparse ap = new argparse("これはファイルを読み込んでつなげてファイル出力するプログラムです"); 
///ap.ResisterArgs(args);
/// (例)△○.exe *FileOut2016*.txt -o b.txt -i 5 -d 3 7 6.5 -t 2001/05/03
///List<string> inFiles = ap.getArgs((char)0, "inFile", "入力ファイル", kind: argparse.Kind.FilesWithWildcard_or_FilesInFolder, canBeMulti: true);  // *FileOut2016*.txt ファイル存在しなかったらエラー
///List<string> outFile = ap.getArgs('o', "OutFile", "出力ファイル", kind: argparse.Kind.NotExistFile, canOmit: true);  // b.txt  ファイル存在したらエラー
///List<string> Lines   = ap.getArgs('i', "Lines", "読み込む最大行数", kind: argparse.Kind.IsInt, canOmit: true); // 5  整数でなかったらエラー
///List<string> dbl     = ap.getArgs('d', "数値", "インターバルの秒数", kind: argparse.Kind.IsNumeric: true, canBeMulti: true); // 3 7 6.5  数値でなかったらエラー 複数指定可
///List<string> dateTime= ap.getArgs('t', "日付", "スタート日", kind: argparse.Kind.NoSpecific, regex: @"\d+/\d+/\d+");  // 2001/05/03 正規表現にあわなければエラー
///if (Lines.Count()>0 && int.Parse(Lines[0]) < 1) ap.addError("行数は1以上にしてください");
///エラーがあったら説明を表示し処理を終了する そうでなければメモリ解放
///if (ap.HasError) { Console.Error.Write(ap.ErrorOut()); return; } else ap=null;
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
    bool help = false;

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
        //'-?'か'/?'があればhelp=trueにする
        char nowLabel = (char)0;
        foreach (string ss in args)
        {
            string s = ss.ToLower();
            if (s[0] == '-' || s[0] == '/')
            {
                if (s[1] == '?')
                {
                    help = true;
                    break;
                }
                else
                {
                    nowLabel = s[1];
                    if (s.Length == 2) continue;
                    else s = s.Substring(2);
                }

            }
            if (!argValues.ContainsKey(nowLabel)) argValues.Add(nowLabel, new List<string>());
            argValues[nowLabel].Add(s);

        }
    }

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
    /// 引数に対する説明と制約をつけるとともに、引数を取得
    /// </summary>
    /// <param name="label">ハイフンの後の一文字。ハイフンなしでexeの後に直接書く引数を取得する際は(char)0をいれる</param>
    /// <param name="itemName">引数名</param>
    /// <param name="description">引数の説明</param>
    /// <param name="kind">文字列の種類</param>
    /// <param name="canOmit">省略可否（FilesWithWildcard_or_FilesInFolderについては結果に対して）</param>
    /// <param name="canBeMulti">複数指定可否（FilesWithWildcard_or_FilesInFolderについては結果に対して）</param>
    /// <param name="regex">正規表現</param>
    /// <returns>引数のリスト</returns>
    public List<string> getArgs(
        char label,
        string itemName,
        string description,
        Kind kind = Kind.NoSpecific,
        bool canOmit = false,
        bool canBeMulti = false,
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
            default:
                break;
        }
        if (canOmit) desc += "省略可,";
        if (canBeMulti) desc += "複数可,";
        if (regex != null) desc += "正規表現(" + regex + "),";
        ad.description = description + (desc == "" ? "" : "(" + desc.Trim(',') + ")");

        argDescs.Add(label, ad);
        if (argValues.ContainsKey(label))
        {
            List<string> r = new List<string>();

            foreach (string s in argValues[label])
            {
                int i; double d; bool b;
                switch (kind)
                {
                    case Kind.NoSpecific:
                        r.Add(s);
                        break;
                    case Kind.ExistFile:
                        if (!File.Exists(s)) errMes.Add(itemName + ":ファイルが存在しません:" + s); else r.Add(s);
                        break;
                    case Kind.NotExistFile:
                        if (File.Exists(s)) errMes.Add(itemName + ":ファイルが存在します:" + s); else r.Add(s);
                        break;
                    case Kind.ExistFolder:
                        if (!Directory.Exists(s)) errMes.Add(itemName + ":フォルダが存在しません:" + s); else r.Add(s);
                        break;
                    case Kind.NotExistFolder:
                        if (Directory.Exists(s)) errMes.Add(itemName + ":フォルダが存在します:" + s); else r.Add(s);
                        break;
                    case Kind.ExistFileOrFolder:
                        if (!(File.Exists(s) || Directory.Exists(s))) errMes.Add(itemName + ":ファイルまたはフォルダが存在しません:" + s); else r.Add(s);
                        break;
                    case Kind.NotExistFileOrFolder:
                        if (File.Exists(s) || Directory.Exists(s)) errMes.Add(itemName + ":ファイルまたはフォルダが存在します:" + s); else r.Add(s);
                        break;
                    case Kind.FilesWithWildcard_or_FilesInFolder:
                        foreach (string f in getFiles(s)) r.Add(f);
                        break;
                    case Kind.IsInt:
                        b = int.TryParse(s, out i);
                        b &= double.TryParse(s, out d);
                        if (!b || i != d) errMes.Add(itemName + ":整数に変換できません:" + s); else r.Add(s);
                        break;
                    case Kind.IsNumeric:
                        if (!double.TryParse(s, out d)) errMes.Add(itemName + ":数値に変換できません:" + s); else r.Add(s);
                        break;
                    default:
                        r.Add(s);
                        break;
                }
                if (regex != null) if (!Regex.IsMatch(s, regex)) errMes.Add(itemName + ":正規表現に合致しません:" + s);
            }
            if (!canBeMulti && r.Count() > 1) errMes.Add(itemName + ":複数あってはなりません");
            if (!canOmit && r.Count() == 0) errMes.Add(itemName + ":値がありません");
            return r;
        }
        else
        {
            if (!canOmit) errMes.Add(itemName + ":値がありません");
            return new List<string>();
        }

    }

    public void AddError(string ErrorDescription)
    {
        errMes.Add(ErrorDescription);
    }

    public bool HasError { get { return help || errMes.Count > 0; } }
    public string ErrorOut()
    {
        string s = description + "\r\n\r\nUsage:\r\n";
        s += Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        foreach (char label in argDescs.Keys) if (label == (char)0) s += " " + argDescs[label].itemName;
        foreach (char label in argDescs.Keys) if (label != (char)0) s += " -" + label + " " + argDescs[label].itemName;
        s += "\r\n\r\nArguments:\r\n";
        foreach (char label in argDescs.Keys) if (label == (char)0) s += argDescs[label].itemName + ":" + argDescs[label].description + "\r\n";
        foreach (char label in argDescs.Keys) if (label != (char)0) s += argDescs[label].itemName + ":" + argDescs[label].description + "\r\n";

        if (!help)
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

        //Debug.WriteLine("path=" + path);
        //Debug.WriteLine("file=" + file);


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
            kouho =Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + string.Format("{0:;(#)}", -i) + ext);
            if (!File.Exists(kouho)) break;
        }
        return kouho;
    }
    public static string UnchangeExt(string path)
    {
        return ChangeExt(path, Path.GetExtension(path));
    }
}

