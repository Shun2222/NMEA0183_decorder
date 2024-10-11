#!/bin/bash

echo "start program" 

rm blackList.txt
rm ../../shunsukeE/data/ais/*log/log/*.ais3_2
rm ../../shunsukeE/data/ais/*log/log/*.ais4_2
rm ../../shunsukeE/data/ais/*log/log/*")".ais*
rm ../../shunsukeE/data/ais/*log/log/*")".log
echo "rm blackList.txt, *.ais3_2, *.ais4_2 ,,,"

echo "A3-Ais2ToAis3-remove/Program.exe"

# creating black list
A3-Ais2ToAis3-remove/Program.exe $(ls ../../shunsukeE/data/ais/15090[1-7]-*/log/*.ais2 | sort -V)

# use black list
A3-Ais2ToAis3-remove/Program.exe $(ls ../../shunsukeE/data/ais/15090[8-9]-*/log/*.ais2 | sort -V)
A3-Ais2ToAis3-remove/Program.exe $(ls ../../shunsukeE/data/ais/1509[1-3]*log/log/*.ais2 | sort -V)

#echo "A4-Ais3-removeToMap/Program.exe \$(ls ../../shunsukeE/data/ais/*log/log/*.ais3_2 | sort -V)"

$outFile="201509_combined.ais3_2"
files=$(ls ../../shunsukeE/data/ais/*log/log/*.ais3_2 | sort -V )
first_file=true

for file in $files; do
    if [ "$first_file" = true ]; then
        cat "$file" > $outFIle 
        first_file=false
    else
        tail -n +2 "$file" >> $outFile 
    fi
done

echo "Created $outFile"
echo "Please exec A4-Ais3-removeToMap/Program.exe $outFile"
