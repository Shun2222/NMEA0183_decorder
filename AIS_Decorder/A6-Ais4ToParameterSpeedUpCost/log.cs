using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

class Log
{
    static string workfolder = "";
    public static void SetWorkFolder(string folder)
    {
        workfolder=folder.TrimEnd('\\')+@"\";
    }

    StreamWriter sw;
    bool show;
    public Log(string filepath, bool append = false, bool flush = true,bool show=false,bool sjis=false)
    {
        filepath = workfolder + filepath;
        if (!Directory.Exists(Path.GetDirectoryName(filepath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
        sw = new StreamWriter(filepath, append: append, encoding: sjis ?Encoding.GetEncoding(932): Encoding.UTF8);
        sw.AutoFlush= flush;
        this.show = show;
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

    public void WriteLine(string s)
    {
        sw.WriteLine(s);
        if (show) Console.WriteLine(s);
    }
}

