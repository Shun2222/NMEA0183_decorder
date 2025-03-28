using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrentEstim 
{
    public class Combination
    {
        private static List<List<int>> _comb;
        public static List<List<int>> Generate(int n, int r, bool dupulication)
        {
            _comb = new List<List<int>>();
            CalcCombination(new List<int>(), n, r, dupulication);
            return _comb;
        }
        private static void CalcCombination(List<int> list, int n, int r, bool dupulication)
        {
            if (list.Count == r)
            {
                _comb.Add(new List<int>(list));
                return;
            }
            int index = 0;
            if (dupulication)
            {
                index = list.Any() ? list.Last() : 0;
            }
            else
            {
                index = list.Any() ? list.Last() + 1 : 0;
            }
            for (int i = index; i < n; i++)
            {
                list.Add(i);
                CalcCombination(list, n, r, dupulication);
                list.Remove(i);
            }
        }
    }
}
