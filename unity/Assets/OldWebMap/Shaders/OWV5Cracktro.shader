// Waiting / finished window of the V5 startup object. Idle animation uses _Time (the performance clock is not running here).
// _Mode 0 field: copper bars + 3-layer scrolling stars + scanlines, kept dark behind the canvas rectangle
// _Mode 1 metal: blue-white metal ramp from baked bevel shading (vertex colour r) with a slow moving sheen
// _Mode 2 logo : point filtered pixel sprite with a shimmer sweep
Shader "OldWebMap/V5/Cracktro Field"
{
    Properties
    {
        [Enum(Field,0,Metal,1,Logo,2)] _Mode ("Mode", Float) = 0
        _MainTex ("Logo sprite (point filtered)", 2D) = "white" {}
        _Aspect ("Field aspect (width / height)", Float) = 1.2857
        _Safe ("Canvas safe rectangle in uv (x0 y0 x1 y1)", Vector) = (.115,.145,.885,.76)
        _Bars ("Top bar centre, bottom bar centre, top half height, bottom half height", Vector) = (.875,.072,.05,.04)
        _Gain ("Master", Range(0,2)) = 1
        _Lines ("Scanline count", Float) = 168
        _Sweep ("Sheen / shimmer speed", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst blend", Float) = 0
        [Enum(Off,0,On,1)] _ZW ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off ZWrite [_ZW] ZTest LEqual Blend [_Src] [_Dst]
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
            sampler2D _MainTex;
            float _Mode, _Aspect, _Gain, _Lines, _Sweep;
            float4 _Safe, _Bars;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; o.local = v.vertex.xyz;
                return o;
            }
            float Hash(float2 p) { p = frac(p*float2(123.34,456.21)); p += dot(p, p + 45.32); return frac(p.x*p.y); }
            // crisp square stars drifting left, three parallax layers
            float Stars(float2 uv, float cells, float speed, float size, float seed, float t)
            {
                float2 p = float2(uv.x*_Aspect + t*speed, uv.y)*cells;
                float2 id = floor(p), f = frac(p) - .5;
                float h = Hash(id + seed);
                float2 o = (float2(Hash(id + seed*1.7 + 3.1), Hash(id + seed*2.3 + 7.7)) - .5)*.5;
                float2 d = abs(f - o);
                float w = fwidth(p.x);
                float shape = 1 - smoothstep(size - w, size + w, max(d.x, d.y));
                float twinkle = .65 + .35*sin(t*(1.5 + h*4) + h*50);
                return shape*step(.62, h)*twinkle*(.4 + .6*h);
            }
            float3 Bars(float y, float t, float centre, float amp, float hw)
            {
                float3 col = 0;
                // blue, cyan, magenta, amber copper bars
                float3 pal0 = float3(.15,.35,1), pal1 = float3(.10,.80,.95), pal2 = float3(.90,.30,.80), pal3 = float3(1,.65,.20);
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float c = centre + amp*sin(t*(1.1 + k*.37) + k*1.7);
                    float d = abs(y - c)/hw;
                    float body = pow(saturate(1 - d), 1.7);
                    float core = pow(saturate(1 - d*3.2), 3);
                    float3 p = k == 0 ? pal0 : k == 1 ? pal1 : k == 2 ? pal2 : pal3;
                    col += p*body*.6 + float3(.8,.9,1)*core*.5;
                }
                return col;
            }
            float3 MetalRamp(float t)
            {
                t = saturate(t);
                float3 c0 = float3(.015,.04,.12), c1 = float3(.09,.28,.62), c2 = float3(.42,.70,.95), c3 = float3(.94,.99,1);
                return t < .35 ? lerp(c0, c1, t/.35) : t < .7 ? lerp(c1, c2, (t - .35)/.35) : lerp(c2, c3, (t - .7)/.3);
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                if (_Mode < .5)
                {
                    float2 uv = i.uv;
                    float2 c = (_Safe.xy + _Safe.zw)*.5, h = (_Safe.zw - _Safe.xy)*.5;
                    float2 q = abs((uv - c)*float2(_Aspect,1)) - h*float2(_Aspect,1);
                    float d = length(max(q, 0)) + min(max(q.x, q.y), 0);
                    float safe = 1 - smoothstep(0, .06, d);
                    float3 col = lerp(float3(.004,.009,.03), float3(.015,.035,.09), uv.y*.6 + .2);
                    float stars = Stars(uv, 22, .018, .10, 1, t)*.45 + Stars(uv, 14, .045, .09, 5, t)*.75 + Stars(uv, 9, .10, .07, 9, t);
                    col += stars*lerp(float3(.55,.75,1), float3(1,1,1), uv.y)*lerp(1, .35, safe);
                    // copper bars live above and below the canvas; the field behind the canvas stays near black
                    float topMask = smoothstep(.70, .79, uv.y), bottomMask = 1 - smoothstep(.14, .22, uv.y);
                    float3 bars = Bars(uv.y, t, _Bars.x, .055, _Bars.z)*topMask + Bars(uv.y, t + 3.1, _Bars.y, .03, _Bars.w)*bottomMask;
                    bars += Bars(uv.y, t*.4 + 7, .5, .3, .03)*.14*(1 - topMask)*(1 - bottomMask);
                    col += bars*lerp(.65, .10, safe)*(.5 + .5*(1 - safe));
                    col *= .93 + .07*sin(uv.y*_Lines*3.14159265);
                    float2 v = uv - .5; col *= 1 - dot(v, v)*.9;
                    col += (Hash(uv*531 + t) - .5)*.008;
                    return fixed4(col*_Gain, 1);
                }
                else if (_Mode < 1.5)
                {
                    float shade = i.color.r;
                    float sheen = pow(saturate(sin((i.local.x*1.1 + i.local.y*.7)*2.4 - t*_Sweep*1.3)), 14)*.45*i.color.g;
                    return fixed4(MetalRamp(shade*.95 + sheen*.6)*_Gain, 1);
                }
                else
                {
                    float4 c = tex2D(_MainTex, i.uv);
                    float sweepX = frac(t*.22*_Sweep)*1.8 - .4;
                    float sweep = smoothstep(.12, 0, abs(i.uv.x + i.uv.y*.35 - sweepX));
                    c.rgb += c.rgb*sweep*.9*step(.5, c.a);
                    clip(c.a - .01);
                    return fixed4(c.rgb*_Gain, c.a);
                }
            }
            ENDCG
        }
    }
    Fallback Off
}
