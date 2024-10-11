import os 

for i in range(12):
    os.system(f'Ais2ToAis3.exe ../../../../datas/20150919/150919-{i+1}log/log/Japan_20150919{2*i:02}0000-20150919{2*i+1:02}5959.ais2')
    print(f"comp {i}")
