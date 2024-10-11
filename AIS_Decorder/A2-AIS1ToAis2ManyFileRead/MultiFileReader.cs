using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Reader : IDisposable
{

    public readonly string file;
    StreamReader sr = null;
    //プロパティ
    /// <summary>
    /// 読み込んだ行の内容。読み込む前や、EOFになってから読んだ場合はnull
    /// </summary>
    public string OneLine { get; private set; } = null;
    /// <summary>
    /// OneLineが記載されている行番号
    /// </summary>
    public int num_line { get; private set; } = 0;
    /// <summary>
    /// ファイルが終端に達した
    /// </summary>
    public bool EOF = false;
    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="File">開くファイル。ここでは確認のために開くだけ</param>
    public Reader(string File, bool testOpen = false)
    {
        file = File;
        if (testOpen)
        {
            try { using (StreamReader srtmp = new StreamReader(file)) if (srtmp.EndOfStream) EOF = true; }
            catch (Exception e) { Console.Error.WriteLine($"エラー File={file} メッセージ={e.ToString()}"); }
        }
    }
    //メソッド
    /// <summary>
    /// 一行読み、OneLineプロパティにセットする。
    /// 読み込みが成功したら読んだ行,失敗したらnullを返す。
    /// ファイルが終端に達したらファイルを閉じ、EOFをtrueにする。
    /// </summary>
    /// <returns>読み込んだ行</returns>
    public string MoveNext()
    {
        if (EOF) OneLine = null;
        else
        {
            try
            {
                if (sr == null) sr = new StreamReader(file);
                if (sr.EndOfStream) OneLine = null;
                else
                {
                    OneLine = sr.ReadLine();
                    num_line++;
                }
                if (sr.EndOfStream)
                {
                    EOF = true;
                    Dispose();
                }
            }
            catch (Exception e)
            {
                OneLine = null;
                EOF = true;
                Console.Error.WriteLine($"エラー File={file} メッセージ={e.ToString()}");
            }
        }
        return OneLine;
    }
    public void Dispose()
    {
        try { if (sr != null) sr.Dispose(); }
        catch { }
        finally { sr = null; }
    }
    ~Reader() { Dispose(); }
}

/// <summary>
/// SからTのインスタンスを作成し、その比較器をもとに順序を並べ替える配列
/// </summary>
/// <typeparam name="S">IComparableクラスを作成するもとになるクラス</typeparam>
/// <typeparam name="T">文字列をもとに作成するIComparableクラス</typeparam>
/// <typeparam name="U">一緒に出力する任意のクラス</typeparam>
public class My_SortedSet<S, T, U> where T : IComparable<T>
{
    public delegate T S2T(S s);
    public S2T func;
    public class STU : IComparable<STU>
    {
        public readonly S s;
        public readonly T t;
        public readonly U u;
        ulong renban;
        static ulong RenbanMaker = 0;
        public STU(S s, T t, U u)
        {
            this.s = s;
            this.t = t;
            this.u = u;
            renban = RenbanMaker++;
        }
        /// <summary>
        /// 比較器。tをもとに比較した結果を返す。
        /// 片方のtがnullのときはnullの方を小さいとする
        /// 両方がnullか同値のときは後から登録された方（連番が大きい方）を小さいとする
        /// </summary>
        /// <param name="rival">比較対象</param>
        /// <returns>比較結果</returns>
        public int CompareTo(STU rival)
        {
            int r = 0;
            if (t == null && rival.t != null) return -1;
            if (t != null && rival.t == null) return 1;
            if (t != null && rival.t != null) r = t.CompareTo(rival.t);

            if (r == 0) r = -1 * renban.CompareTo(rival.renban);
            return r;
        }
    }
    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="func">文字列からIComparableインスタンスを作成する関数エントリ</param>
    public My_SortedSet(S2T func) { this.func = func; }
    private My_SortedSet() { }

    SortedSet<STU> my_stu = new SortedSet<STU>();
    /// <summary>
    /// 配列要素を追加する。自動的にソートされる
    /// </summary>
    /// <param name="s">文字列</param>
    /// <param name="u">一緒に出力するオブジェクト</param>
    public void Add(S s, U u)
    {
        T t = func == null ? default(T) : func(s);
        STU stu = new STU(s, t, u);
        my_stu.Add(stu);
    }
    /// <summary>
    /// 配列の先頭を取り出す
    /// </summary>
    /// <param name="stu">配列の先頭に来ている要素</param>
    /// <returns>配列が空ならfalse</returns>
    public bool GetTop(out STU stu)
    {
        if (my_stu.Count() == 0) { stu = default(STU); return false; }
        else
        {
            stu = my_stu.ElementAt(0);
            return true;
        }
    }
    /// <summary>
    /// 配列の先頭を取り出すとともに配列から削除
    /// </summary>
    /// <param name="stu">配列の先頭に来ている要素</param>
    /// <returns>配列が空ならfalse</returns>
    public bool GetTopAndRemove(out STU stu)
    {
        if (GetTop(out stu))
        {
            my_stu.Remove(stu);
            return true;
        }
        return false;
    }
}

/// <summary>
/// 複数のファイルをOpenして行を読む。foreach (string s in mr)で回せる。
/// comparerを定義するとその順番で読む。定義しないとファイルを順番に読む。
/// </summary>
class MultiFileReader<T> : IEnumerable<string>, IDisposable where T : IComparable<T>
{
    //フィールド
    My_SortedSet<string, T, Reader> ms;

    //プロパティ
    /// <summary>
    /// さっき読んだ行の由来のファイル名
    /// </summary>
    public string OpeningFile { get; private set; }
    /// <summary>
    /// さっき読んだ行の当該ファイル中の行番号(1始まり)
    /// </summary>
    public int Num_LineOfTheFile { get; private set; }
    /// <summary>
    /// さっき読んだ行で終わりか
    /// </summary>
    public bool EndOfFiles { get; private set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="Files">ファイルパスの配列</param>
    /// <param name="func">stringからTインスタンスを作成する関数 省略時はファイル配列を先頭から順番に読む</param>
    public MultiFileReader(IEnumerable<string> Files, My_SortedSet<string, T, Reader>.S2T func = null)
    {
        //file配列の後のほうから登録していく。STU.CompareToが後から登録されたものを優先するとしているから。
        ms = new My_SortedSet<string, T, Reader>(func);
        foreach (string file in Files.Reverse())
        {
            Reader r = new Reader(file);
            string s = r.MoveNext();
            if (s != null) ms.Add(s, r);
        }
    }

    public IEnumerator<string> GetEnumerator()
    {
        My_SortedSet<string, T, Reader>.STU stu, stuDummy;
        while (ms.GetTopAndRemove(out stu))
        {
            string s = stu.s; Reader r = stu.u;
            OpeningFile = r.file;
            Num_LineOfTheFile = r.num_line;
            string newLine = r.MoveNext();
            if (newLine != null) ms.Add(newLine, r); else r.Dispose();
            EndOfFiles = !ms.GetTop(out stuDummy);

            yield return stu.s;
        }
    }
    //意味は分からないがつけろとhttps://msdn.microsoft.com/ja-jp/library/s793z9y2.aspx に書いてある
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

    void IDisposable.Dispose()
    {
        My_SortedSet<string, T, Reader>.STU stu;
        while (ms.GetTopAndRemove(out stu)) stu.u.Dispose();
    }
}