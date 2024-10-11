import os 

for i in range(12):
    os.system(f'Ais3ToAis4.exe ../../../../datas/20150919/150919-{i+1}log/log/Japan_20150919{2*i:02}0000-20150919{2*i+1:02}5959.ais3')
    print(f"comp {i}")
