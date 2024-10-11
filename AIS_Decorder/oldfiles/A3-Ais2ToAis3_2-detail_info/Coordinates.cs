using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//ここで扱う座標系は、
//経度-90～+90
//緯度 20～380  ←ここ注意！！
//です。

namespace CurrentEstim
{
    class LatLonDT
    {
        public readonly Single Lat, Lon;
        public readonly DateTime DT;
        static Single LonNormalize(Single lon) { return ((lon - 20 + 720) % 360) + 20; }

        public LatLonDT(Single lat, Single lon, DateTime dt)
        {
            Lat = lat;
            Lon = LonNormalize(lon);
            DT = dt;
        }
    }
    class Coord
    {
        public Single North, East;
        public Coord(Single north, Single east)
        {
            North = north;
            East = east;
        }
    }
}