// Full-film role-token drop and isolated cursor; V5 sine scroller geometry and coverage sampling retained. One shader for the physical ring (performance clock) and the idle window strip (_Time).
// Atlas channels: R glyph cover, G baked outline, B glyph id (hue step 18/256 per glyph), A style (1 = interrupt, grey, flat).
// Text pixel s -> atlas row = floor(s/stride), x = s - row*stride + pad; every row stores `pad` px of its neighbours so seams are exact.
Shader "OldWebMap/FullFilm/Greetz"
{
    Properties
    {
        _Drop ("Role names fall away", Range(0,1)) = 0
        _CursorOnly ("Show only the isolated cursor", Float) = 0
        _CursorSpan ("Cursor source pixel bounds", Vector) = (0,0,0,0)
        _VisibleSpan ("Current stage pixel bounds", Vector) = (-100000,100000,0,0)
        _MainTex ("Glyph atlas (R cover, G outline, B glyph id, A style)", 2D) = "black" {}
        _Scroll ("Text pixel at the arc front (px)", Float) = 0
        _ScrollRate ("Idle scroll (px per second, only when UseTimeline=0)", Float) = 0
        _Timeline ("Performance time", Float) = 0
        _UseTimeline ("1 performance clock, 0 idle _Time", Float) = 1
        _Opacity ("Opacity", Range(0,1)) = 1
        _ArcLength ("Length covered by uv.x (m)", Float) = 34.9
        _PxPerMetre ("Atlas pixels per metre of text", Float) = 190.476
        _Atlas ("Atlas width, height, row height px, rows", Vector) = (4096,1024,112,5)
        _Row ("Row stride px, row pad px, text length px, cyclic", Vector) = (4064,16,18189,0)
        _Grad ("Gradient top px, bottom px, baseline px, unused", Vector) = (13,90,82,0)
        _Amp ("Wobble amplitude (m)", Float) = 0.17
        _Wave ("Wobble wavelength (m)", Float) = 6.4
        _Omega ("Wobble angular speed (rad/s)", Float) = 2.4544
        _HueSpeed ("Rainbow cycles per second", Float) = 0.125
        _EndFade ("Soft fade fraction at both ends", Float) = 0.09
        _Outline ("Outline colour", Color) = (0.015,0.02,0.05,1)
        _Shadow ("Shadow offset px (right, down), strength", Vector) = (3,3,0.55,0)
        _GreyTop ("Interrupt grey top", Color) = (0.74,0.77,0.80,1)
        _GreyBottom ("Interrupt grey bottom", Color) = (0.46,0.49,0.53,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
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
            float _Drop, _CursorOnly;
            float4 _CursorSpan, _VisibleSpan;
            float _Scroll, _ScrollRate, _Timeline, _UseTimeline, _Opacity, _ArcLength, _PxPerMetre;
            float _Amp, _Wave, _Omega, _HueSpeed, _EndFade;
            float4 _Atlas, _Row, _Grad, _Outline, _Shadow, _GreyTop, _GreyBottom;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 info:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float Clock() { return lerp(_Time.y, _Timeline, _UseTimeline); }
            float Wrap(float s) { return _Row.w > .5 ? s - floor(s/_Row.z)*_Row.z : s; }
            float2 AtlasUV(float s, float yTop)
            {
                float row = clamp(floor(s/_Row.x), 0, _Atlas.w-1);
                float x = s - row*_Row.x + _Row.y;
                return float2(x/_Atlas.x, 1 - (row*_Atlas.z + yTop)/_Atlas.y);
            }
            float3 Hsv(float h, float s, float v)
            {
                float3 k = saturate(abs(frac(h + float3(0, .6666667, .3333333))*6 - 3) - 1);
                return v*lerp(float3(1,1,1), k, s);
            }
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float T = Clock();
                float a = (v.uv.x - .5)*_ArcLength;
                float s = _Scroll + _ScrollRate*T*(1-_UseTimeline) + a*_PxPerMetre;
                // style of the token under this vertex: interrupts keep still (no wobble)
                float4 token = tex2Dlod(_MainTex, float4(AtlasUV(Wrap(s), _Atlas.z*.5), 0, 0));
                float style = token.a;
                float role = floor(token.b*255+.5);
                float fallen = smoothstep(frac(role*.6180339)*.72, frac(role*.6180339)*.72+.24, _Drop)*(1-step(.5, style));
                float wobble = _Amp*sin(a*6.2831853/_Wave + _Omega*T)*(1 - saturate(style*1.25));
                float4 p = v.vertex; p.y += wobble - fallen*fallen*1.65;
                o.vertex = UnityObjectToClipPos(p);
                float f0 = saturate(v.uv.x/max(_EndFade,1e-4)), f1 = saturate((1-v.uv.x)/max(_EndFade,1e-4));
                o.info = float4(s, v.uv.y, (f0*f0*(3-2*f0))*(f1*f1*(3-2*f1))*(1-fallen), 0);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float s = i.info.x;
                if (s < _VisibleSpan.x || s > _VisibleSpan.y) return fixed4(0,0,0,0);
                if (_CursorOnly > .5 && (s < _CursorSpan.x || s > _CursorSpan.y)) return fixed4(0,0,0,0);
                float yTop = (1 - i.info.y)*_Atlas.z;
                // derivatives from the continuous coordinates so wrapping / row changes never pick a wrong mip
                float2 dx = float2(ddx(s), ddx(yTop)), dy = float2(ddy(s), ddy(yTop));
                if (_Row.w < .5 && (s < -24 || s > _Row.z + 24)) return fixed4(0,0,0,0);
                float2 uv = AtlasUV(Wrap(s), yTop);
                float2 gx = float2(dx.x/_Atlas.x, -dx.y/_Atlas.y), gy = float2(dy.x/_Atlas.x, -dy.y/_Atlas.y);
                float4 tex = tex2Dgrad(_MainTex, uv, gx, gy);
                float shadow = tex2Dgrad(_MainTex, uv + float2(-_Shadow.x/_Atlas.x, _Shadow.y/_Atlas.y), gx, gy).g*_Shadow.z;
                float T = Clock();
                float hue = frac(tex.b*255*0.0703125 + T*_HueSpeed);
                float g = smoothstep(.30, .70, saturate((yTop - _Grad.x)/(_Grad.y - _Grad.x)));
                float3 rainbow = lerp(Hsv(hue, .30, 1.0), Hsv(hue + .10, .88, .92), g);
                float3 grey = lerp(_GreyTop.rgb, _GreyBottom.rgb, g);
                float3 col = lerp(rainbow, grey, saturate((tex.a - .4)*5));
                float cover = tex.r;
                float alpha = saturate(cover + (1 - cover)*max(tex.g, shadow));
                float3 rgb = lerp(_Outline.rgb, col, cover);
                return fixed4(rgb, alpha*_Opacity*i.info.z);
            }
            ENDCG
        }
    }
    Fallback Off
}
