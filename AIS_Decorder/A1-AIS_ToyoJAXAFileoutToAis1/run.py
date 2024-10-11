import os 

path = r'C:\Users\nmri\Documents\shunsuke\nmri\data\ais\2015123031/'
for i in range(1, 6):
    j = (i-1) * 2
    os.system( f'program.exe ' + path + f'151230-{i}log/log/japan_20151230{j:02}0000-20151230{j+1:02}5959.log')
    #print( f'program.exe ' + path + f'151231-{i}log/log/japan_20151231{j:02}0000-20151231{j+1:02}5959.log')
    print(f"comp {i}")
