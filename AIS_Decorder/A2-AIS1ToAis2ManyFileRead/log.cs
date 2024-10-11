using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

/// <summary>
/// 動作ログを記録する
/// </summary>
class Log
{
    StreamWriter sw;
    bool show,datetime;
    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="filepath">記録するファイルパス（省略時log.txt）</param>
    /// <param name="show">コンソールウィンドウに表示する</param>
    /// <param name="append">追記する</param>
    /// <param name="autoflush">AutoFlush</param>
    /// <param name="sjis">S-JISかUTCか</param>
    /// <param name="datetime">行頭に日時記載</param>
    public Log(string filepath="log.txt", bool show = false, bool append = false, bool autoflush = true, bool sjis = false,bool datetime=false)
    {
        if (!Directory.Exists(Path.GetDirectoryName(filepath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
        sw = new StreamWriter(filepath, append: append, encoding: sjis ? Encoding.GetEncoding(932) : Encoding.UTF8);
        sw.AutoFlush = autoflush;
        this.show = show;
        this.datetime = datetime;
    }
    ~Log()
    {
        try
        {
            sw.Flush();
            sw.Close();
            sw.Dispose();
        }
        catch (Exception e) { }
    }
    /// <summary>
    /// 動作ログに記載
    /// </summary>
    /// <param name="s">記載するセンテンス</param>
    public void WriteLine(string s)
    {
        if (datetime) s = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss, ") + s;
        sw.WriteLine(s);
        if (show) Console.WriteLine(s);
    }
}

/// <summary>
/// 動作ログを記録する静的クラス（コンストラクタ不要）
/// </summary>
static class DefaultLog
{

    //staticをFinalizeしたいときはここから 
    sealed class Finalizer
    {
        static Finalizer instance = new Finalizer();
        public static Finalizer GetInstance() { return instance; }
        Finalizer() { }
        ~Finalizer() { Close(); }
    }
    static readonly Finalizer finalizer = Finalizer.GetInstance();
    //ここまでを決まり文句にする。またstatic void Close()を定義する


    static DefaultLog() { }
    static StreamWriter defaultSW = null;

    /// <summary>
    /// 動作ログを記録する。defailtlog.txtに記録される
    /// </summary>
    /// <param name="s">記録するセンテンス</param>
    /// <param name="show">コンソールウィンドウに表示する</param>
    /// <param name="append">追記する（初回呼び出し時のみ有効）</param>
    /// <param name="autoflush">AutoFlush（初回呼び出し時のみ有効）</param>
    /// <param name="sjis">S-JISかUTCか（初回呼び出し時のみ有効）</param>
    /// <param name="datetime">行頭に日時記載</param>
    public static void WriteLine(string s, bool show = false, bool append = false, bool autoflush = true, bool sjis = false, bool datetime = false)
    {
        if (defaultSW == null)
        {
            defaultSW = new StreamWriter("defaultlog.txt", append: append, encoding: sjis ? Encoding.GetEncoding(932) : Encoding.UTF8);
            defaultSW.AutoFlush = autoflush;
        }
        if (datetime) s = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss, ") + s;
        defaultSW.WriteLine(s);
        if (show) Console.WriteLine(s);
    }
    static void Close()
    {
        if (defaultSW != null)
        {
            try
            {
                defaultSW.Flush();
                defaultSW.Close();
                defaultSW.Dispose();
            }
            catch (Exception e) { }
        }
    }
}
