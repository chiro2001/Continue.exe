Shader "OldWebMap/FilmV4/Story Ribbon"
{
    Properties
    {
        _MainTex("SC packed glyph strip",2D)="black"{}
        _Timeline("Performance time",Float)=0
        _Morph("Window to spatial ribbon",Range(0,1))=0
        _Opacity("Glyph envelope",Range(0,1))=1
        _Scroll("Text centre in pixels",Float)=0
        _Length("Cyclic text length pixels",Float)=1000
        _Cyclic("Repeat only during performance",Float)=1
        _Span("Pixels across mesh",Float)=8950
        _Amp("Wave amplitude metres",Float)=.12
        _Wave("Wave length metres",Float)=6.4
        _HueSpeed("Climax colour cycles",Float)=0
        _Cold("Access conditions desaturation",Range(0,1))=0
        _Line("Quiet empty thread",Range(0,1))=0
        _Branch("Phase of converging ribbon",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent+12" "RenderType"="Transparent"}
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
            float _Timeline,_Morph,_Opacity,_Scroll,_Length,_Cyclic,_Span,_Amp,_Wave,_HueSpeed,_Cold,_Line,_Branch;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float fade:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float a=(v.uv.x-.5)*47.12389;
                float theta=(v.uv.x-.5)*6.2831853;
                float3 dock=float3((v.uv.x-.5)*1.875,(v.uv.y-.5)*.15,0);
                float3 ring=float3(sin(theta)*7.5,(v.uv.y-.5)*.36,cos(theta)*7.5);
                float3 p=lerp(dock,ring,_Morph);
                float obstruction=(1-_Cold*.78)*lerp(1,smoothstep(.1,.28,abs(frac(v.uv.x*5)-.5)),_Cold);
                p.y+=_Amp*sin(a*6.2831853/max(.1,_Wave)+_Timeline*2.454369+_Branch)*obstruction;
                p.z+=_Morph*_Amp*.3*cos(theta*2-_Timeline*.61359+_Branch);
                o.vertex=UnityObjectToClipPos(float4(p,1));
                o.uv=float2(_Scroll+(v.uv.x-.5)*_Span,v.uv.y);
                o.fade=smoothstep(0,.018,v.uv.x)*smoothstep(0,.018,1-v.uv.x);
                o.fade*=lerp(1,smoothstep(.07,.12,abs(frac(v.uv.x*5)-.5)),_Cold*.7);
                return o;
            }
            float3 hsv(float h,float s,float v){return v*lerp(1,saturate(abs(frac(h+float3(0,.6666667,.3333333))*6-3)-1),s);}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float s=i.uv.x;float inside=step(0,s)*step(s,_Length);
                float2 dx=float2(ddx(s)/4096,ddx(i.uv.y)),dy=float2(ddy(s)/4096,ddy(i.uv.y));
                s=lerp(s,s-floor(s/max(1,_Length))*_Length,_Cyclic);
                float2 uv=float2(s/4096,i.uv.y);
                float4 glyph=tex2Dgrad(_MainTex,uv,dx,dy);
                float hue=frac(glyph.b*255*.0703125+_Timeline*_HueSpeed);
                float3 colour=lerp(hsv(hue,.78,.95),hsv(hue+.08,.28,1),smoothstep(.15,.85,i.uv.y));
                colour=lerp(colour,lerp(float3(.38,.47,.55),float3(.86,.91,.94),i.uv.y),max(_Cold,glyph.a));
                float cover=glyph.r,outline=glyph.g;
                float alpha=saturate(cover+(1-cover)*outline)*_Opacity*lerp(inside,1,_Cyclic);
                float threadAlpha=exp(-abs(i.uv.y-.5)*150)*_Line;
                float3 rgb=lerp(float3(.006,.012,.026),colour,cover);
                rgb=lerp(rgb,float3(.15,.27,.33),threadAlpha);
                return fixed4(rgb,max(alpha,threadAlpha*.19)*i.fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
