"""Bake a distance-driven, eight-view cutout walk. Authoring needs Pillow, NumPy and OpenCV."""
from math import cos, pi, sin, sqrt
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

TILE = (196, 284)
ROWS = 14
WALK_FRAMES = 20
STRIDE = 1.38 * (244 / 1.55)  # one complete stride, expressed in painted pixels
PITCH = sin(32 * pi / 180)
NEAR = [0, 0, 0, 0, 1, 1, 1, 1]
ART = Path(__file__).resolve().parents[1] / "Concepts/Observatorio"


def cut(source, column=0, row=0, cols=1, rows=1):
    w, h = source.size
    cell = source.crop((round(column*w/cols), round(row*h/rows),
                        round((column+1)*w/cols), round((row+1)*h/rows)))
    return cell.crop(cell.getchannel("A").point(lambda a: 255 if a > 24 else 0).getbbox())


def premul(image):
    a = np.array(image, dtype=np.float32) / 255
    a[:, :, :3] *= a[:, :, 3:]
    return a


def to_image(a):
    rgb = a[:, :, :3] / np.maximum(.0001, a[:, :, 3:])
    return Image.fromarray((np.clip(np.dstack((rgb, a[:, :, 3])), 0, 1)*255+.5).astype(np.uint8))


def over(back, front):
    return front + back * (1-front[:, :, 3:])


class Limb:
    """A painted strip with shared vertices at each joint, including the boot sole."""
    def __init__(self, art, fractions, length, width_scale):
        self.fractions = np.array(fractions)
        w, h = art.size
        self.tex = premul(art.resize((round(w*length/h*width_scale), length), Image.Resampling.LANCZOS))
        alpha = self.tex[:, :, 3]
        last = alpha.shape[0]-1
        centers = []
        for f in fractions:
            line = alpha[min(last-1,max(1,round(f*last)))]
            xs = np.flatnonzero(line > .1)
            centers.append((xs[0]+xs[-1])/2 if len(xs) else alpha.shape[1]/2)
        # The foot rotates around the ankle, not around the middle of the toe box.
        if len(fractions) == 5:
            centers[-1] = centers[-2]
        self.centers = np.array(centers)

    def draw(self, joints):
        pts = np.array(joints, dtype=np.float32)
        edges = np.diff(pts, axis=0)
        normals = np.stack((edges[:,1], -edges[:,0]), axis=1)
        normals /= np.maximum(.001,np.linalg.norm(normals,axis=1)[:,None])
        cross = np.vstack((normals[0], normals[:-1]+normals[1:], normals[-1]))
        cross /= np.maximum(.001,np.linalg.norm(cross,axis=1)[:,None])
        if len(pts) == 5:
            # Keep the toe box and sole rigid while the cuff follows the shin.
            cross[-2] = cross[-1]
        h,w = self.tex.shape[:2]
        ends = np.array([-1.,float(w)])
        source = np.empty((len(pts),2,2),np.float32)
        source[:,:,0] = ends
        source[:,:,1] = (self.fractions*(h-1))[:,None]
        dest = pts[:,None,:]+cross[:,None,:]*(ends[None,:]-self.centers[:,None])[:,:,None]
        uv = np.full((TILE[1],TILE[0],2),-10,np.float32)
        for i in range(len(pts)-1):
            for keys in (((i,0),(i,1),(i+1,1)),((i,0),(i+1,1),(i+1,0))):
                tri=np.float32([dest[k] for k in keys])
                src=np.float32([source[k] for k in keys])
                x0,y0=np.maximum(0,np.floor(tri.min(axis=0)).astype(int))
                x1,y1=np.minimum(TILE,np.ceil(tri.max(axis=0)).astype(int)+1)
                if x0 >= x1 or y0 >= y1:
                    continue
                mask=np.zeros((y1-y0,x1-x0),np.uint8)
                cv2.fillConvexPoly(mask,np.int32(np.round((tri-[x0,y0])*16)),1,shift=4)
                ys,xs=np.nonzero(mask)
                xs,ys=xs+x0,ys+y0
                matrix=cv2.getAffineTransform(tri,src)
                uv[ys,xs]=xs[:,None]*matrix[:,0]+ys[:,None]*matrix[:,1]+matrix[:,2]
        return cv2.remap(self.tex,uv,None,cv2.INTER_LINEAR)


def foot_path(t):
    stance = .54
    if t < stance:
        return STRIDE*(stance/2-t), 0., 0.
    u=(t-stance)/(1-stance)
    smooth=u*u*(3-2*u)
    # Match the support velocity at toe-off and heel strike.
    travel=STRIDE*(stance*(smooth-.5)-(1-stance)*u*(1-u)*(1-2*u))
    return travel, 11*sin(pi*u)**1.5*(1.2-.4*u), .21*sin(2*pi*u)*sin(pi*u)**.5


def registered_body(art, original, col):
    scale=184/art.height
    art=art.resize((round(art.width*scale),184),Image.Resampling.LANCZOS)
    hair=art.getchannel("A").crop((0,0,art.width,36)).getbbox()
    layer=Image.new("RGBA",TILE)
    layer.paste(art,(round(TILE[0]/2-(hair[0]+hair[2])/2),4))
    body=premul(layer)
    # Keep the established face and hair, including their physical size at game zoom.
    face=Image.new("RGBA",TILE)
    face.paste(original,((TILE[0]-original.width)//2,0))
    y=np.arange(TILE[1],dtype=np.float32)[:,None,None]
    blend=np.clip((65-y)/9,0,1)
    body=body*(1-blend)+premul(face)*blend
    # In front views the opening in the coat reveals the moving trouser legs.
    rgb=body[:,:,:3]/np.maximum(.001,body[:,:,3:])
    indigo=np.clip((rgb[:,:,2]-rgb[:,:,0]+.09)/.10,0,1)
    opening=indigo*np.clip((y[:,:,0]-124)/17,0,1)
    body*=1-opening[:,:,None]
    return body


def render(body, leg, arm, direction, phase, strength):
    a=direction*pi/4
    dx, forward_depth=sin(a),cos(a)
    dy=forward_depth*PITCH
    wave=2*pi*phase
    bob=strength*(.6+2.6*cos(2*wave-.7))
    sway=2.3*sin(wave-.2)*forward_depth*strength
    center=TILE[0]/2
    legs,arms,depths=[],[],[]
    for side in (0,1):
        sign=2*side-1
        t=(phase+side*.5)%1
        travel,lift,roll=np.array(foot_path(t))*strength
        stance_x=center+sign*(13*forward_depth+1.8*dx)
        stance_y=230-sign*dx*4
        hip_x=center+sign*11*forward_depth+sway
        hip_y=120+bob-sign*dx*3
        # Two-link sagittal IK, projected into the hand-painted three-quarter camera.
        vertical=stance_y-hip_y-lift
        reach=max(1,sqrt(travel*travel+vertical*vertical))
        bone=(stance_y-(120-sign*dx*3))/2+4.6*strength
        bend=sqrt(max(0,bone*bone-(reach/2)**2))
        kf=travel*.5+bend*vertical/reach
        kd=vertical*.5-bend*travel/reach
        ankle=np.array([stance_x+dx*travel,stance_y+dy*travel-lift])
        knee=np.array([.5*(hip_x+stance_x)+dx*kf,hip_y+kd+dy*kf])
        sole=ankle+[dx*16*sin(roll),16*cos(roll)]
        legs.append(leg.draw([(hip_x,hip_y),knee,knee+.73*(ankle-knee),ankle,sole]))
        depths.append(forward_depth*travel-sign*dx*17)
        sx=center+sign*26*forward_depth+dx*.8+sway*.4
        sy=80-sign*dx*2+bob*.65
        swing=-cos(wave+side*pi-.12)*strength
        elbow=(sx+sign*2*forward_depth+dx*(8*swing-2*strength),sy+32+dy*6*swing)
        hand=(sx+sign*4*forward_depth+dx*23*swing,sy+68+dy*19*swing-4*swing*swing)
        arms.append(arm.draw([(sx,sy),elbow,hand]))
    back,near=np.argsort(depths)
    canvas=over(legs[back],legs[near])
    yy,xx=np.mgrid[:TILE[1],:TILE[0]].astype(np.float32)
    hem=np.clip((yy-115)/72,0,1)**2
    head=np.clip((118-yy)/86,0,1)
    bx=xx-sway*(1-.58*head)-dx*strength*1.9*head+dx*2.8*strength*sin(wave-.5)*hem
    by=yy-bob*(1-.48*head)
    costume=cv2.remap(body,bx.astype(np.float32),by.astype(np.float32),cv2.INTER_LINEAR)
    far,front=1-NEAR[direction],NEAR[direction]
    if direction in (0,4):
        canvas=over(canvas,costume)
        canvas=over(canvas,arms[far])
    else:
        canvas=over(over(canvas,arms[far]),costume)
    canvas=over(canvas,arms[front])
    # Put the arm roots beneath the shoulder cape.
    cape=costume*np.clip((98-by)/5,0,1)[:,:,None]
    return to_image(over(canvas,cape))


def bake(rest_views):
    with Image.open(ART/"guardian-rig-costume.png") as body_sheet, Image.open(ART/"guardian-rig-limbs.png") as parts, Image.open(ART/"guardian-rig-front-left.png") as correction:
        atlas=Image.new("RGBA",(16*TILE[0],ROWS*TILE[1]))
        for d,original in enumerate(rest_views):
            art=cut(correction) if d==7 else cut(body_sheet,d%4,d//4,4,2)
            body=registered_body(art,original,d)
            angle=min(d,8-d)
            paintings=[cut(parts,angle,row,5,2) for row in (0,1)]
            if d>4:
                paintings=[p.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for p in paintings]
            leg=Limb(paintings[0],(0,.49,.74,.83,1.),125,.83)
            arm=Limb(paintings[1],(0,.44,1.),68,.83)
            poses=[(.25,0.)]+[(i/WALK_FRAMES,1.) for i in range(WALK_FRAMES)]
            poses += [(p,s) for p in (.25,.75) for s in (.25,.5,.75)]
            for i,(phase,strength) in enumerate(poses):
                tile=render(body,leg,arm,d,phase,strength)
                atlas.paste(tile,((d+8*(i//ROWS))*TILE[0],(i%ROWS)*TILE[1]))
    return atlas
