Shader "OldWebMap/FilmV5/Story Ribbon"
{
    Properties
    {
        _MainTex("SC packed glyph strip",2D)="black"{}
        _Timeline("Performance time",Float)=0
        _Morph("Window to spatial ribbon",Range(0,1))=0
        _Opacity("Glyph envelope",Range(0,1))=1
        _Scroll("Text centre in pixels",Float)=0
        _Length("Cyclic text length pixels",Float)=1000
        _Cyclic("Repeat only if explicitly enabled",Float)=0
        _RevealPixels("Revealed text head in pixels",Float)=4096
        _Fall("Selected word nodes decay and fall",Range(0,1))=0
        _Epoch("Stable cue seed",Float)=0
        _Span("Pixels across mesh",Float)=16755.162
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
            float _RevealPixels,_Fall,_Epoch;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float fade:TEXCOORD1;float fall:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            float falling(float packed)
            {
                float code=floor(packed*255+.5);
                float selected=step(127.5,code);
                float token=code-selected*128;
                float threshold=frac(token*.381966+_Epoch*.173)*.24;
                return selected*smoothstep(threshold,threshold+.66,_Fall);
            }
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float a=(v.uv.x-.5)*47.12389;
                float theta=(v.uv.x-.5)*6.2831853;
                float3 dock=float3((v.uv.x-.5)*1.875,(v.uv.y-.5)*.14,0);
                float3 ring=float3(sin(theta)*7.5,(v.uv.y-.5)*.36,cos(theta)*7.5);
                float3 p=lerp(dock,ring,_Morph);
                float obstruction=1-_Cold*.28;
                p.y+=_Amp*sin(a*6.2831853/max(.1,_Wave)+_Timeline*2.454369+_Branch)*obstruction;
                p.z+=_Morph*_Amp*.3*cos(theta*2-_Timeline*.61359+_Branch);
                float s=_Scroll+(v.uv.x-.5)*_Span;
                float word=tex2Dlod(_MainTex,float4(s/4096,.5,0,0)).a;
                float fall=falling(word)*_Morph;
                float token=fmod(floor(word*255+.5),128);
                p.y-=fall*fall*2.5;
                p.x+=sin(token*2.7+_Timeline*.7)*fall*.22;
                p.z+=fall*.7;
                o.vertex=UnityObjectToClipPos(float4(p,1));
                o.uv=float2(s,v.uv.y);
                o.fall=fall;
                o.fade=smoothstep(0,.018,v.uv.x)*smoothstep(0,.018,1-v.uv.x);
                return o;
            }
            float3 hsv(float h,float s,float v){return v*lerp(1,saturate(abs(frac(h+float3(0,.6666667,.3333333))*6-3)-1),s);}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float s=i.uv.x;float inside=step(0,s)*step(s,min(_Length,_RevealPixels));
                float2 dx=float2(ddx(s)/4096,ddx(i.uv.y)),dy=float2(ddy(s)/4096,ddy(i.uv.y));
                s=lerp(s,s-floor(s/max(1,_Length))*_Length,_Cyclic);
                float2 uv=float2(s/4096,i.uv.y);
                float4 glyph=tex2Dgrad(_MainTex,uv,dx,dy);
                float hue=frac(glyph.b*255*.0703125+_Timeline*_HueSpeed);
                float3 colour=lerp(hsv(hue,.78,.95),hsv(hue+.08,.28,1),smoothstep(.15,.85,i.uv.y));
                float decay=falling(glyph.a);
                float3 grey=lerp(float3(.28,.32,.37),float3(.69,.74,.78),i.uv.y);
                colour=lerp(colour,grey,saturate(_Cold*.18+sqrt(decay)));
                float cover=glyph.r,outline=glyph.g;
                float alpha=saturate(cover+(1-cover)*outline)*_Opacity*lerp(inside,1,_Cyclic)*(1-smoothstep(.74,1,decay));
                float3 rgb=lerp(float3(.006,.012,.026),colour,cover);
                return fixed4(rgb,alpha*i.fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
