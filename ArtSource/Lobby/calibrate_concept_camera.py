# Calibration de la caméra du concept art du Lobby (Lobby_Vue.jpg) à partir des sommets de piliers
# (hauteur 57,2 m, un pilier tous les 50 m, murs à ±200 m). Écrit concept_camera.json à côté de ce script.
# Lancer :  python ArtSource/Lobby/calibrate_concept_camera.py
import numpy as np, json, os, sys
H = 57.2
pts = []  # (X,Y,Z, u,v, poids)
east = [(805,124),(878.5,129),(958.5,136),(1045,142.5),(1137.5,151),(1242.5,160),(1355,169),(1478.5,181),(1615,191)]
for i,(u,v) in enumerate(east): pts.append((200,H,200-50*i,u,v,1.0))
north = {150:(745,138),100:(677,153),50:(593,168),0:(492,194),-50:(367,216),-100:(215,252),-150:(27,290)}
for x,(u,v) in north.items(): pts.append((x,H,200,u,v,1.0))
west = {0:(220,600),-50:(475,680),-100:(790,810),-150:(1310,1000)}
for z,(u,v) in west.items(): pts.append((-200,H,z,u,v,0.3))
P = np.array(pts,float)

def rot(yaw,pitch,roll):
    cy,sy,cp,sp,cr,sr = np.cos(yaw),np.sin(yaw),np.cos(pitch),np.sin(pitch),np.cos(roll),np.sin(roll)
    # Unity : main gauche, Y haut. forward = (sin yaw cos pitch, -sin pitch, cos yaw cos pitch)
    f = np.array([sy*cp,-sp,cy*cp]); r = np.array([cy,0,-sy]); u = np.cross(f,r)
    R0 = np.stack([r,u,f])            # lignes : droite, haut, avant
    Rr = np.array([[cr,-sr,0],[sr,cr,0],[0,0,1]])
    return Rr@R0
def project(p,X):
    cx,cy,cz,yaw,pitch,roll,f,u0,v0 = p
    R = rot(yaw,pitch,roll)
    d = (X-np.array([cx,cy,cz]))@R.T
    return np.stack([u0+f*d[:,0]/d[:,2], v0-f*d[:,1]/d[:,2]],1), d[:,2]
def resid(p):
    uv,_ = project(p,P[:,:3]); return ((uv-P[:,3:5])*np.sqrt(P[:,5:6])).ravel()
def lm(p,free,it=400):
    p = p.copy(); lam=1e-2
    for _ in range(it):
        r = resid(p); J = np.zeros((len(r),len(free)))
        for j,k in enumerate(free):
            dp = np.zeros_like(p); dp[k] = 1e-4*max(1,abs(p[k])); J[:,j] = (resid(p+dp)-r)/dp[k]
        A = J.T@J; g = J.T@r
        step = np.linalg.solve(A+lam*np.diag(np.diag(A)+1e-9),-g)
        q = p.copy(); q[free]+=step
        if (resid(q)**2).sum() < (r**2).sum(): p=q; lam*=0.5
        else: lam*=4
    return p
best=None
for yaw0 in np.radians([60,75,90,105,120]):
  for pitch0 in np.radians([25,35,45]):
    for dist in [350,500,700]:
      for f0 in [800,1400,2200]:
        fw = np.array([np.sin(yaw0)*np.cos(pitch0),-np.sin(pitch0),np.cos(yaw0)*np.cos(pitch0)])
        c = np.array([0,0,0])-fw*dist
        p0 = np.array([c[0],c[1],c[2],yaw0,pitch0,0,f0,1000,558],float)
        try:
            p = lm(p0,[0,1,2,3,4,6])
            p = lm(p,[0,1,2,3,4,5,6],200)
        except Exception as e: continue
        e = np.sqrt((resid(p)**2).mean())
        _,dz = project(p,P[:,:3])
        if (dz>0).all() and p[6]>0 and (best is None or e<best[0]): best=(e,p)
e,p = best
print('rms px',round(e,2)); print('cam pos',p[:3].round(1),'yaw',np.degrees(p[3]).round(1),'pitch',np.degrees(p[4]).round(1),'roll',np.degrees(p[5]).round(2),'f',p[6].round(1))
uv,_ = project(p,P[:,:3])
for a,b in zip(P,uv): print(a[:3],a[3:5],b.round(1),(b-a[3:5]).round(1))
chk = np.array([[200,0,-25],[200,34,-25],[25,0,200],[25,34,200],[0,0,0],[200,0,200],[200,0,-200],[-200,0,200],[-200,0,-200]],float)
print(project(p,chk)[0].round(0))
json.dump({'p':p.tolist()},open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'concept_camera.json'),'w'))
