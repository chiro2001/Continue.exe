Shader "OldWebMap/FullFilm/Overload Field"
{
    Properties
    {
        _MainTex ("Original recall and interface tiles", 2D) = "white" {}
        _Opacity ("Section envelope", Range(0,1)) = 1
        _Timeline ("Absolute performance seconds", Float) = 133.12
        _MotionTime ("Queue clock with explicit freeze", Float) = 133.12
        _Start ("Section start", Float) = 133.12
        _Mode ("Copies / pressure / queue / remnants", Float) = 0
        _Tint ("Section colour", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
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
            sampler2D _MainTex;float4 _MainTex_TexelSize,_Tint;
            float _Opacity,_Timeline,_MotionTime,_Start,_Mode;
            // vertex = small local card corner; meta = angular x/y, depth phase, seed.
            // rect selects a baked tile; color.r = card/credit/cursor/click role.
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 meta:TEXCOORD1; float4 rect:TEXCOORD2; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 rect:TEXCOORD1; float4 tone:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float age=max(0,_Timeline-_Start), motionAge=max(0,_MotionTime-_Start);
                float speed=_Mode<.5?.035:_Mode<1.5?.047:_Mode<2.5?.023:.011;
                float depth=4.5+frac(v.meta.z-motionAge*speed)*34;
                float size=1+depth*.032;
                if(_Mode>.5&&_Mode<1.5)size*=1+.18*saturate(age/10.24);
                float3 center=float3(v.meta.x*(depth+2.8),1.35+v.meta.y*(depth+2.8),depth);
                float3 local=v.vertex.xyz*size;
                float alpha=smoothstep(4.5,5.8,depth)*(1-smoothstep(36,38.5,depth));
                float role=v.color.r;
                if(role>.16&&role<.5)
                {
                    float drop=saturate((age-.64-v.meta.w*6)/1.28);
                    drop=drop*drop*(3-2*drop);
                    center.y-=drop*drop*4.2;
                    center.x+=sin(v.meta.w*13)*drop*.6;
                    center.z-=drop*.45;
                    float s=sin(drop*.5),c=cos(drop*.5);
                    local.xy=float2(c*local.x-s*local.y,s*local.x+c*local.y);
                    alpha*=1-drop;
                }
                if(role>.5)
                {
                    float cycle=frac(age/1.28+v.meta.w);
                    center.x+=sin(age*1.7+v.meta.w*18)*.13*size;
                    center.y+=cos(age*1.3+v.meta.w*14)*.10*size;
                    if(role>.84)
                    {
                        local*=.5+cycle*1.4;
                        alpha*=pow(1-cycle,3)*.7;
                    }
                    else local*=1-.08*exp(-pow((cycle-.5)*12,2));
                }
                if(_Mode>2.5)alpha*=1-smoothstep(.2+v.meta.w*.60,.5+v.meta.w*.5,age/17.92);
                o.vertex=UnityObjectToClipPos(float4(center+local,1));
                o.uv=v.uv;o.rect=v.rect;
                float shade=.75+.25*saturate(1-depth/40);
                o.tone=float4(_Tint.rgb*shade,alpha*_Opacity);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 inset=_MainTex_TexelSize.xy*.7;
                float2 uv=i.rect.xy+inset+i.uv*max(i.rect.zw-2*inset,.00001);
                fixed4 c=tex2D(_MainTex,uv);
                c.rgb*=i.tone.rgb;
                c.a*=i.tone.a;
                clip(c.a-.02);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
