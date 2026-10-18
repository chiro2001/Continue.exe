// Continuous, detached glyphs. Colour/outline/shadow follow episode123-v5.
// UV1: stream pixel at vertex / glyph band y. UV2: glyph centre / word centre.
// UV3: selected word / colour id. No shared triangles, texture scrolling or thread.
Shader "OldWebMap/FilmV5/Cumulative Greetz"
{
    Properties
    {
        _MainTex("SC glyph atlas (R cover, G outline)",2D)="black"{}
        _AtlasSize("Width, height, inverse width, inverse height",Vector)=(4096,1024,.00024414,.00097656)
        _Scroll("Cumulative stream pixels",Float)=0
        _Timeline("Performance time",Float)=0
        _Morph("Window to overhead ring",Range(0,1))=1
        _Opacity("Opacity",Range(0,1))=1
        _Radius("Ring radius metres",Float)=7.5
        _BandHeight("Band height metres",Float)=.52
        _PixelsPerMetre("Natural glyph proportions",Float)=246.15385
        _EntryAngle("Upper right entry radians",Float)=.78539816
        _Stages("Grey start, flat grey, drop start, drop end (turns)",Vector)=(.2222222,.2638889,.46,.8)
        _Amp("Sine amplitude metres",Float)=.17
        _Wave("Sine wavelength metres",Float)=6.4
        _HueSpeed("Rainbow cycles per second",Float)=0
        _Cold("Ambient desaturation",Range(0,1))=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True"}
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
            float4 _AtlasSize,_Stages;
            float _Scroll,_Timeline,_Morph,_Opacity,_Radius,_BandHeight,_PixelsPerMetre,_EntryAngle,_Amp,_Wave,_HueSpeed,_Cold;
            struct appdata
            {
                float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 stream:TEXCOORD1;
                float2 centres:TEXCOORD2;float2 style:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;
                float4 info:TEXCOORD1;float2 phase:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO
            };
            float hash(float x){return frac(sin(x*127.1)*43758.5453);}
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float circumference=6.2831853*_Radius*_PixelsPerMetre;
                float glyphTravel=_Scroll-v.centres.x;
                float wordTurn=(_Scroll-v.centres.y)/circumference;
                float grey=smoothstep(_Stages.x,_Stages.y,wordTurn)*v.style.x;
                float fall=smoothstep(_Stages.z,_Stages.w,wordTurn)*v.style.x;
                // Grey words first hold at their original height. Wobble is
                // completely gone before any downward displacement begins.
                float transition=smoothstep(_Stages.x,_Stages.x+.006,wordTurn)*(1-smoothstep(_Stages.y-.006,_Stages.y,wordTurn))*v.style.x;
                float frame=floor(_Timeline*25);
                float glitch=(hash(floor(v.centres.y)+frame)-.5)*transition;
                float metres=(_Scroll-v.stream.x)/_PixelsPerMetre;
                float theta=_EntryAngle-metres/_Radius;
                float3 ring=float3(sin(theta)*_Radius,(v.stream.y-.5)*_BandHeight,cos(theta)*_Radius);
                ring.y+=_Amp*sin(metres*6.2831853/max(.1,_Wave)+_Timeline*2.454369)*(1-grey);
                ring.y-=fall*fall*3.3;
                ring.x+=glitch*.065+sin(v.centres.y*.13)*fall*.12;
                ring.z+=fall*.3;
                float3 dock=float3(.9375-(_Scroll-v.stream.x)*.14/128,(v.stream.y-.5)*.14,0);
                float3 p=lerp(dock,ring,_Morph);
                o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;
                float fade=smoothstep(0,22,glyphTravel)*(1-smoothstep(.96,1,glyphTravel/circumference));
                fade*=1-smoothstep(.78,.97,wordTurn)*v.style.x;
                float dockClip=1-smoothstep(.91,.96,abs(dock.x));
                fade*=lerp(dockClip,1,smoothstep(.1,.7,_Morph));
                o.info=float4(v.stream.y,grey,v.style.y,fade);o.phase=float2(transition,glitch);
                return o;
            }
            float3 hsv(float h,float s,float v){return v*lerp(float3(1,1,1),saturate(abs(frac(h+float3(0,.6666667,.3333333))*6-3)-1),s);}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 tex=tex2D(_MainTex,i.uv);
                float shadow=tex2D(_MainTex,i.uv+float2(-3*_AtlasSize.z,3*_AtlasSize.w)).g*.55;
                float hue=frac(i.info.z*.0703125+_Timeline*_HueSpeed);
                float grad=smoothstep(.30,.70,saturate(((1-i.info.x)*128-13)/77));
                float3 rainbow=lerp(hsv(hue,.30,1),hsv(hue+.10,.88,.92),grad);
                float3 grey=lerp(float3(.74,.77,.80),float3(.46,.49,.53),grad);
                float3 colour=lerp(rainbow,grey,saturate(i.info.y+_Cold*.08));
                float cover=tex.r;
                float alpha=saturate(cover+(1-cover)*max(tex.g,shadow))*_Opacity*i.info.w;
                float slice=step(.85,hash(floor(i.info.x*24)+floor(_Timeline*25)));
                alpha*=1-i.phase.x*slice*.55;
                clip(alpha-.002);
                return fixed4(lerp(float3(.015,.02,.05),colour,cover),alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
