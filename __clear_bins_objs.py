import os, shutil

for root, dirs, files in os.walk('.', topdown=False):
    for d in dirs:
        if d.lower() in ('bin', 'obj'):
            p = os.path.join(root, d)
            try:
                if os.path.islink(p):
                    os.unlink(p)
                else:
                    shutil.rmtree(p)
                print("-:", p)
            except Exception as e:
                print("!", p, ":", e)