#!/usr/bin/env python3
"""Bake one cumulative, chapter-aligned greetz stream with detached glyph cells.

The original episode123-v5 font metrics, outline and colour steps are retained.
Geometry metadata is imported by OWFilmV5RibbonPass through Unity Pipeline.
"""
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

import draw_episode123_v5_identity as identity

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/export/fullfilm-v5/story'
DATA = ROOT / 'data/fullfilm-v5-ribbon-flow.json'
STEP = .08
MIN_SPEED = 240.0
RADIUS = 7.5
BAND_HEIGHT = .52
AW = 4096
BAND = 128
PAD = 9


def smooth_slopes(times, pixels):
    secants = [(b-a)/(tb-ta) for ta,tb,a,b in zip(times,times[1:],pixels,pixels[1:])]
    slopes = [secants[0]]
    for a,b in zip(secants,secants[1:]):
        slopes.append(2*a*b/(a+b) if a*b>0 else 0)
    slopes.append(secants[-1])
    # Monotone cubic Hermite bound; preserve exact cue entrances without a
    # discontinuous velocity each time the cue density changes.
    for i,secant in enumerate(secants):
        if secant==0:
            slopes[i]=slopes[i+1]=0
            continue
        norm=math.hypot(slopes[i]/secant,slopes[i+1]/secant)
        if norm>3:
            factor=3/norm
            slopes[i]*=factor
            slopes[i+1]*=factor
    return slopes


def evaluate(time, times, pixels, slopes):
    i=max(0,min(len(times)-2,int(np.searchsorted(times,time,side='right')-1)))
    dt=times[i+1]-times[i]
    u=max(0,min(1,(time-times[i])/dt))
    return ((2*u**3-3*u**2+1)*pixels[i] + (u**3-2*u**2+u)*dt*slopes[i]
            + (-2*u**3+3*u**2)*pixels[i+1] + (u**3-u**2)*dt*slopes[i+1])


def main():
    story=json.loads((ROOT/'data/fullfilm-v5-story.json').read_text())
    face=identity.Face(80)
    words=[]
    glyphs=[]
    cues=[]
    times=[0.0]
    pixels=[0.0]
    cursor=0.0
    hue=0
    for cue in story['ribbons'][1:]:
        tokens=[{'text':t['text']} for t in cue['tokens']]
        layout,length=identity.layout_tokens(face,tokens,20,trailing_separator=False)
        length=max(64,math.ceil(length+20))
        duration=cue['end']-cue['start']
        travel=max(length+280,MIN_SPEED*duration)
        cues.append(dict(index=cue['index'],id=cue['id'],start=cue['start'],end=cue['end'],
                         stream_start=round(cursor,5),stream_end=round(cursor+length,5),
                         text=cue['text']))
        token_ids={}
        for ti,token in enumerate(tokens):
            token_ids[ti]=len(words)
            words.append(dict(cue=cue['index'],text=token['text'],
                              start=round(cursor+token['start'],5),end=round(cursor+token['end'],5),
                              selected=ti in cue['falling_tokens']))
        for g in layout:
            if g['kind']=='space':
                continue
            wi=token_ids.get(g['token'],-1)
            word=words[wi] if wi>=0 else None
            glyphs.append(dict(character=g['ch'],cue=cue['index'],word=wi,
                               start=round(cursor+g['pen'],5),advance=round(g['adv'],5),
                               centre=round(cursor+g['pen']+g['adv']/2,5),
                               word_centre=round((word['start']+word['end'])/2,5) if word else round(cursor+g['pen']+g['adv']/2,5),
                               selected=bool(word and word['selected']),hue=hue%256))
            hue+=1
        cursor+=travel
        times.append(cue['end'])
        pixels.append(cursor)
    slopes=smooth_slopes(times,pixels)
    curve=[round(evaluate(i*STEP,times,pixels,slopes),5) for i in range(round(times[-1]/STEP)+1)]
    if any(b<a for a,b in zip(curve,curve[1:])):
        raise ValueError('Scroll must never rewind')

    # Cache individual glyph tiles. Padded cells do not sample neighbours, and
    # no mesh triangle joins two glyphs when a selected word falls.
    tiles={}
    x=y=0
    for glyph in glyphs:
        ch=glyph['character']
        if ch in tiles:
            continue
        adv,draw_x=face.place(ch,PAD)
        width=math.ceil(adv)+2*PAD
        if x+width>AW:
            x=0;y+=BAND
        g=dict(ch=ch,kind=face.kind(ch),pen=PAD,adv=adv,x=draw_x,token=0)
        cover,ids,style,_=identity.render_strip(face,[g],width,BAND,92)
        tile=np.stack([cover,identity.outline_plane(cover),ids,style],-1)
        tiles[ch]=dict(x=x,y=y,width=width,advance=adv,image=tile)
        x+=width+2
    ah=2**math.ceil(math.log2(y+BAND))
    atlas=np.zeros((ah,AW,4),dtype=np.uint8)
    for tile in tiles.values():
        atlas[tile['y']:tile['y']+BAND,tile['x']:tile['x']+tile['width']]=tile['image']
    path=OUT/'ribbon-flow-atlas.png'
    Image.fromarray(atlas).save(path)
    for glyph in glyphs:
        tile=tiles[glyph['character']]
        glyph['rect']=[tile['x'],tile['y'],tile['width'],BAND]
        glyph['left']=round(glyph['start']-PAD,5)
        glyph['right']=round(glyph['start']-PAD+tile['width'],5)
    circumference=math.tau*RADIUS*BAND/BAND_HEIGHT
    for word in words:
        if not word['text']:
            continue
        centre=(word['start']+word['end'])/2
        word['phases']={}
        for key,turn in [('grey_start',80/360),('grey_flat',95/360),('drop_start',.46),('drop_end',.8),('one_turn',1.0)]:
            value=centre+turn*circumference
            word['phases'][key]=round(float(np.interp(value,curve,np.arange(len(curve))*STEP)),3) if value<=curve[-1] else None
    result=dict(schema='fullfilm-v5-ribbon-flow.1',basis='episode123-v5 continuous greetz curve and SC font metrics',
                atlas=dict(file=str(path.relative_to(ROOT)),width=AW,height=ah,padding=PAD,
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest()),
                geometry=dict(radius=RADIUS,band_height=BAND_HEIGHT,pixels_per_metre=BAND/BAND_HEIGHT,
                              entry_degrees=45,centre=[0,4.45,-2.8],grey_start_turn=80/360,
                              grey_flat_turn=95/360,drop_start_turn=.46,drop_end_turn=.8,
                              lifetime_turns=1,independent_glyphs=True,connectors=False),
                curve_start=0,curve_step=STEP,curve_px=curve,cues=cues,words=words,glyphs=glyphs)
    DATA.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(success=True,cues=len(cues),glyphs=len(glyphs),selected_words=sum(w['selected'] for w in words),
                          unique_glyphs=len(tiles),atlas=[AW,ah],curve_samples=len(curve),stream_pixels=cursor),ensure_ascii=False))


if __name__=='__main__':
    main()
