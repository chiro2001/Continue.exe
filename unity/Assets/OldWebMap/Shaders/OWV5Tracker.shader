Shader "OldWebMap/V5/Tracker Pattern"
{
    // One physical panel of the FC14 tracker sidebars: 21 visible rows, smooth sub-row scroll,
    // highlighted playing row, per-voice note-on flash and level bars. All glyphs come from the
    // baked atlas (tools/prepare_episode123_v5_tracker.py); the panel is a pure function of
    // _RowPosition (= performance time / 0.08), _Flash, _Levels, _Opacity and _Timeline.
    Properties
    {
        _Atlas ("Tracker atlas (linear RGBA, point, no mips)", 2D) = "black" {}
        _Panel ("Panel (0 = CH1|CH2, 1 = CH3|CH4)", Float) = 0
        _PanelPx ("Panel size in font pixels", Vector) = (144,254,0,0)
        _RowPosition ("Row position (time / 0.08)", Float) = 0
        _Flash ("Note-on flash CH1..CH4", Vector) = (0,0,0,0)
        _Levels ("Voice level CH1..CH4", Vector) = (0,0,0,0)
        _Opacity ("Panel opacity", Range(0,1)) = 1
        _Timeline ("Performance time", Float) = 0
        _Ch0 ("CH1 phosphor", Color) = (.35,1,.45,1)
        _Ch1 ("CH2 phosphor", Color) = (.3,.85,1,1)
        _Ch2 ("CH3 phosphor", Color) = (1,.7,.25,1)
        _Ch3 ("CH4 phosphor", Color) = (1,.4,.85,1)
        _TextColor ("Note text", Color) = (.92,.97,1,1)
        _InstColor ("Instrument and row text", Color) = (.35,.85,1,1)
        _BarColor ("Playing-row bar", Color) = (.07,.27,.95,1)
        _Background ("Panel background", Color) = (.012,.02,.04,1)
        _BackAlpha ("Background alpha", Range(0,1)) = .55
        _Brightness ("Text gain", Range(0,3)) = 1.15
        _TrailSeconds ("Played-note afterglow (s)", Float) = .38
        _Smooth ("Vertical scroll smoothing", Range(0,1)) = .6
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            sampler2D _Atlas;
            float4 _Atlas_TexelSize;
            float _Panel, _RowPosition, _Opacity, _Timeline, _BackAlpha, _Brightness, _TrailSeconds, _Smooth;
            float4 _PanelPx, _Flash, _Levels, _Ch0, _Ch1, _Ch2, _Ch3;
            float4 _TextColor, _InstColor, _BarColor, _Background;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            // Atlas layout (see data/episode123-v5-tracker.json): 16 strips of 128x8 px per atlas row,
            // strip = row>>1; even row R(panel0) G(panel1), odd row B(panel0) A(panel1).
            #define MARGIN 8.0
            #define ROW_H 10.0
            #define ROWS_VISIBLE 21.0
            #define ROWS_TOTAL 3200.0
            #define HEADER_SLOT 1600.0
            #define FONT_SLOT 1601.0
            #define ROW_SECONDS 0.08

            float4 Texel(float2 px)
            {
                return tex2Dlod(_Atlas, float4((px.x+.5)*_Atlas_TexelSize.x, 1-(px.y+.5)*_Atlas_TexelSize.y, 0, 0));
            }
            float2 SlotOrigin(float slot)
            {
                float sy = floor(slot/16.0);
                return float2((slot-sy*16.0)*128.0, sy*8.0);
            }
            float Cov(float2 origin, float gx, float gy, float4 mask)
            {
                float inside = step(0,gx)*step(gx,127.5)*step(0,gy)*step(gy,7.5);
                return inside*dot(Texel(origin + float2(clamp(gx,0,127), clamp(gy,0,7))), mask);
            }
            // hx,hy: continuous strip coordinates in font pixels (pixel k spans k..k+1).
            // x is point sampled with an analytic 1-screen-pixel edge (sharp bilinear), y is blended
            // toward linear by smoothY. Returns (coverage, horizontal glow, max tap level).
            float3 StripCov(float2 origin, float4 mask, float hx, float hy, float2 fw, float smoothY)
            {
                float cx = hx-.5, cy = hy-.5;
                float gx0 = floor(cx), wx = cx-gx0;
                float gy0 = floor(cy), wy = cy-gy0;
                wx = saturate((wx-.5)/max(fw.x*.8,.0001)+.5);
                float wys = saturate((wy-.5)/max(fw.y*.8,.0001)+.5);
                wy = lerp(wys, wy, smoothY);
                float v00 = Cov(origin,gx0,gy0,mask), v10 = Cov(origin,gx0+1,gy0,mask);
                float v01 = Cov(origin,gx0,gy0+1,mask), v11 = Cov(origin,gx0+1,gy0+1,mask);
                float cov = lerp(lerp(v00,v10,wx), lerp(v01,v11,wx), wy);
                float l = lerp(Cov(origin,gx0-1,gy0,mask), Cov(origin,gx0-1,gy0+1,mask), wy);
                float r = lerp(Cov(origin,gx0+2,gy0,mask), Cov(origin,gx0+2,gy0+1,mask), wy);
                return float3(cov, (l+r)*.5, max(max(v00,v10),max(v01,v11)));
            }
            float3 Pick3(float idx)
            {
                return idx < .5 ? _Ch0.rgb : idx < 1.5 ? _Ch1.rgb : idx < 2.5 ? _Ch2.rgb : _Ch3.rgb;
            }
            float Pick(float4 v, float idx)
            {
                return idx < .5 ? v.x : idx < 1.5 ? v.y : idx < 2.5 ? v.z : v.w;
            }
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float W = _PanelPx.x, H = _PanelPx.y;
                float2 P = float2(i.uv.x*W, (1-i.uv.y)*H);
                float2 fw = max(fwidth(P), float2(.0001,.0001));
                float op = _Opacity;
                float headH = H - ROWS_VISIBLE*ROW_H;
                float yc = headH + ROWS_VISIBLE*.5*ROW_H;
                float curRow = floor(_RowPosition + .5);
                float a = _BackAlpha*op;
                float3 light = 0;
                float hx = P.x - MARGIN;
                float chr = floor(hx/8.0);
                float inStrip = step(0,hx)*step(hx,127.999);
                chr = clamp(chr,0,15);
                float cellIdx = chr >= 9.5 ? 1 : 0;
                float chIndex = _Panel*2 + cellIdx;
                float3 chCol = Pick3(chIndex);
                float fl = Pick(_Flash, chIndex);
                float isGutter = step(chr,1.5);
                float isSep = step(abs(chr-2),.5) + step(abs(chr-9),.5);
                float isInst = step(abs(chr-7.5),1) + step(abs(chr-14.5),1);
                float3 noteCol = lerp(_TextColor.rgb, chCol, .22);
                float3 textCol = isGutter > .5 ? _InstColor.rgb*.85 : isSep > .5 ? chCol*.8 : isInst > .5 ? _InstColor.rgb : noteCol;

                if (P.y < headH)
                {
                    // ---- header: baked CHn labels, hex sequence number, level bars
                    float4 hmask = _Panel < .5 ? float4(1,0,0,0) : float4(0,1,0,0);
                    float3 hc = StripCov(SlotOrigin(HEADER_SLOT), hmask, hx, P.y-6.0, fw, 0);
                    float labelBoost = .72 + fl*.9;
                    float3 hcol = isSep > .5 ? chCol*.5 : isInst > .5 ? _InstColor.rgb*.55 : chCol*labelBoost;
                    light += inStrip*hcol*(hc.x*_Brightness + hc.y*.4);
                    // sequence (order) number of the playing row, two hex digits from the font strip
                    float order = clamp(floor(curRow/32.0), 0, 99);
                    float hi = floor(order/16.0), lo = order - hi*16.0;
                    float digit = chr < .5 ? hi : lo;
                    float fx = digit*8.0 + (hx - chr*8.0);
                    float3 dc = StripCov(SlotOrigin(FONT_SLOT), float4(1,0,0,0), fx, P.y-6.0, fw, 0);
                    light += (inStrip*step(chr,1.5))*_InstColor.rgb*.9*(dc.x*_Brightness + dc.y*.4);
                    // level bars: 12 segments of 4 px per voice (3 lit + 1 gap), y 19..25
                    float by = P.y - 19.0;
                    float inY = step(0,by)*step(by,5.999);
                    float bIdx = P.x < 84.0 ? 0 : 1;
                    float bx = P.x - (bIdx < .5 ? 32.0 : 88.0);
                    float inX = step(0,bx)*step(bx,47.999);
                    float seg = floor(bx/4.0);
                    float segOn = step(frac(bx/4.0), .749);
                    float barCh = _Panel*2 + bIdx;
                    float segValue = saturate(Pick(_Levels,barCh)*12.0 - seg);
                    float3 barCol = lerp(Pick3(barCh), float3(1,1,1), .7*smoothstep(8,12,seg+.5));
                    float barFl = Pick(_Flash,barCh);
                    light += inY*inX*segOn*barCol*(.07 + (.95+barFl*.5)*segValue);
                    // channel rules, accent strip and frame
                    float rule = step(abs(P.y-31.5),.5)*step(MARGIN+24.0,P.x)*step(P.x,MARGIN+128.0)*step(.5,abs(P.x-84.0)-.5);
                    light += rule*(P.x < 84.0 ? Pick3(_Panel*2) : Pick3(_Panel*2+1))*.55;
                    float accent = step(P.y,2.0);
                    light += accent*lerp(Pick3(_Panel*2), Pick3(_Panel*2+1), smoothstep(60,84,P.x))*.8;
                }
                else
                {
                    // ---- rows: scrolling pattern with the playing row centred on yc
                    float q = _RowPosition + (P.y-yc)/ROW_H;
                    float r = floor(q + .5);
                    float f = (q + .5 - r)*ROW_H;
                    float valid = step(0,r)*step(r,ROWS_TOTAL-1);
                    float rc = clamp(r,0,ROWS_TOTAL-1);
                    float par = rc - 2.0*floor(rc*.5);
                    float slot = floor(rc*.5);
                    float idx = par*2 + _Panel;
                    float4 mask = float4(step(abs(idx),.5), step(abs(idx-1),.5), step(abs(idx-2),.5), step(abs(idx-3),.5));
                    float3 c = StripCov(SlotOrigin(slot), mask, hx, f-1.0, fw, _Smooth);
                    float cov = c.x*valid*inStrip;
                    float glow = c.y*valid*inStrip;
                    float dist = abs(P.y-yc);
                    float fall = lerp(1, .5, saturate(dist/(ROWS_VISIBLE*.5*ROW_H)));
                    float edge = smoothstep(0,16,P.y-headH)*smoothstep(0,16,H-P.y);
                    float played = r < curRow - .5 ? .62 : 1;
                    float isNote = step(.7, c.z)*(1-isGutter)*(1-isSep);
                    float ageSec = max(_RowPosition - r, 0)*ROW_SECONDS;
                    float trail = (r <= _RowPosition + .5 ? 1 : 0)*exp(-ageSec/max(_TrailSeconds,.01));
                    float bar = 1 - smoothstep(4.5,5.5,dist);
                    float isCur = step(abs(r-curRow),.5);
                    float3 base = textCol*(cov*_Brightness*(1+.5*bar)*played*fall + glow*.45*fall*played);
                    float3 afterglow = chCol*cov*isNote*trail*1.5*fall;
                    light += (base + afterglow)*edge;
                    // playing-row bar, flash blocks under the voice columns
                    float barEdge = 1 - smoothstep(0,1.1,abs(dist-4.6));
                    light += _BarColor.rgb*(.55*bar + .9*barEdge)*step(4.0,P.x)*step(P.x,W-4.0);
                    float colA = step(MARGIN+24.0,P.x)*step(P.x,MARGIN+72.0);
                    float colB = step(MARGIN+80.0,P.x)*step(P.x,MARGIN+128.0);
                    float f0 = Pick(_Flash,_Panel*2), f1 = Pick(_Flash,_Panel*2+1);
                    light += bar*(Pick3(_Panel*2)*f0*colA + Pick3(_Panel*2+1)*f1*colB)*.85;
                    // sequence boundary every 32 rows
                    float inOrder = rc - 32.0*floor(rc/32.0);
                    float divider = valid*step(abs(inOrder),.5)*(1-smoothstep(.6,1.5,f))*step(4.0,P.x)*step(P.x,W-4.0);
                    light += _InstColor.rgb*divider*.9*edge;
                    a = lerp(a, max(a,.78*op), bar*.85);
                }

                // frame, scanlines and a slow bright band, driven by the performance clock only
                float frameLine = max(max(step(P.x,1),step(W-1,P.x)), max(step(P.y,1),step(H-1,P.y)));
                light += frameLine*_InstColor.rgb*.32;
                float scan = 1 - .10*(.5+.5*sin(P.y*3.14159265));
                float bandPos = frac(_Timeline*.17);
                float bandD = (bandPos-P.y/H)*9;
                float band = exp(-bandD*bandD);
                light = light*scan + _InstColor.rgb*.035*band;
                light = 1 - exp(-light);
                float3 rgb = _Background.rgb*a + light*op;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
