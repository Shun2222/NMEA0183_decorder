#!/bin/bash

for file in E:\\shunsukeE\\data\\ais/*log/log/*.log; do
    if [ -f "$file" ]; then
        echo "start $file" 
        start A1-AIS_ToyoJAXAFileoutToAis1/program.exe $file
        sleep 60
        echo "finished $file" 
    fi
done

for file in E:\\shunsukeE\\data\\ais/*log/log/*.ais1; do
    if [ -f "$file" ]; then
        echo "start $file" 
        start A2-AIS1ToAis2ManyFileRead/bin/Debug/Ais1ToAis2.exe $file
        sleep 20
        echo "finished $file" 
    fi
done

#for file in C:\\Users\\nmri\\Documents\\shunsuke\\nmri\\data\\ais\\2015/*log/log/*.ais1; do
#    if [ -f "$file" ]; then
#        echo "rm $file" 
#        rm $file
#    fi
#done

for file in E:\\shunsukeE\\data\\ais/*log/log/*.ais2; do
    if [ -f "$file" ]; then
        echo "start $file" 
        start A3-Ais2ToAis3/Program.exe $file
        sleep 10
        echo "finished $file" 
    fi
done

#for file in C:\\Users\\nmri\\Documents\\shunsuke\\nmri\\data\\ais\\2015/*log/log/*.ais2; do
#    if [ -f "$file" ]; then
#        echo "rm $file" 
#        rm $file
#    fi
#done

for file in E:\\shunsukeE\\data\\ais/*log/log/*.ais3; do
    if [ -f "$file" ]; then
        echo "start $file" 
        start A4-Ais3ToAis4/Program.exe $file
        sleep 10
        echo "finished $file" 
    fi
done

#for file in C:\\Users\\nmri\\Documents\\shunsuke\\nmri\\data\\ais\\2015/*log/log/*.ais3; do
#    if [ -f "$file" ]; then
#        echo "rm $file" 
#        rm $file
#    fi
#done

for file in E:\\shunsukeE\\data\\ais/*log/log/*.ais4; do
    if [ -f "$file" ]; then
        echo "start $file" 
        start A5-Ais4ToAisCurrLowMemConsumption/Program.exe $file
        sleep 10
        echo "finished $file" 
    fi
done

#for file in C:\\Users\\nmri\\Documents\\shunsuke\\nmri\\data\\ais\\2015/*log/log/*.ais4; do
#    if [ -f "$file" ]; then
#        echo "rm $file" 
#        rm $file
#    fi
#done

#for file in C:\\Users\\nmri\\Documents\\shunsuke\\nmri\\data\\ais\\2015/*log/log/*")".log; do
#    if [ -f "$file" ]; then
#        echo "rm $file" 
#        rm $file
#    fi
#done
