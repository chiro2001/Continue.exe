// PC world screen effect. Per-eye GrabPass sampling supports multipass, double-wide
// and single-pass instanced. Head-following mesh never moves the player's camera.
Shader "OldWebMap/V3/Local Camera Signal"
{
    Properties
    {
        _Intensity ("Intensity", Range(0,1)) = 0
        _Timeline ("Performance time", Float) = 0
        _CRT ("Scanline amount", Range(0,1)) = 0
        _Chroma ("Per-eye RGB split", Range(0,.006)) = .0005
        _Warp ("Per-eye radial refraction", Range(0,.01)) = 0
        _Vignette ("Peripheral shade", Range(0,.4)) = .1
        _Desaturate ("Desaturation", Range(0,1)) = 0
        _Dropout ("Short signal loss", Range(0,1)) = 0
        _Warmth ("Creation warmth", Range(0,1)) = 0
        _LocalHeadPosition ("Local viewer world position", Vector) = (0,1.35,-2.8,1)
        _LocalHeadForward ("Local viewer forward", Vector) = (0,0,1,0)
        _ViewRadius ("Primary view maximum distance", Float) = .24
    }
    SubShader
    {
        Tags { "Queue"="Overlay-20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Off ZWrite Off ZTest Always
        GrabPass { "_OWV3SceneGrab" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_OWV3SceneGrab);
            float4 _LocalHeadPosition, _LocalHeadForward;
            float _ViewRadius, _Intensity, _Timeline, _CRT, _Chroma, _Warp, _Vignette, _Desaturate, _Dropout, _Warmth;
            // VRChat camera globals: 0 is the main view; mirrors/handheld cameras
            // are explicitly rejected in addition to the spatial/view test.
            float _VRChatMirrorMode, _VRChatCameraMode;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 grab:TEXCOORD0; float allowed:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v); v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Vertices are a persisted [-1,+1] screen quad. Clip-space output
                // covers each eye independently, including asymmetric VR frusta.
                o.vertex=float4(v.vertex.xy,UNITY_NEAR_CLIP_VALUE,1);
                o.grab=ComputeGrabScreenPos(o.vertex);
                float3 forward=-UNITY_MATRIX_V[2].xyz;
                float distanceToViewer=distance(_WorldSpaceCameraPos,_LocalHeadPosition.xyz);
                o.allowed=step(distanceToViewer,_ViewRadius)*step(.78,dot(normalize(forward),normalize(_LocalHeadForward.xyz)));
                return o;
            }
            float hash21(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(i.allowed-.5); clip(.5-_VRChatMirrorMode); clip(.5-_VRChatCameraMode);
                float2 grabUV=i.grab.xy/i.grab.w;
                float2 eyeUV=grabUV;
                float2 eyeScale=float2(1,1);
                #if defined(UNITY_SINGLE_PASS_STEREO)
                    float4 stereoScaleOffset=unity_StereoScaleOffset[unity_StereoEyeIndex];
                    eyeScale=stereoScaleOffset.xy;
                    eyeUV=(grabUV-stereoScaleOffset.zw)/max(stereoScaleOffset.xy,float2(.001,.001));
                #endif
                // All artistic math is in individual-eye coordinates. Convert
                // displacement back to packed texture space only at the end.
                float2 center=eyeUV-.5;
                float radius=dot(center,center);
                float2 displacement=center*radius*_Warp;
                float row=floor(eyeUV.y*96);
                float gate=step(.975,hash21(float2(row,floor(_Timeline*8))));
                displacement.x += gate*_Dropout*.012*sin(row+_Timeline*3);
                float2 distorted=grabUV+displacement*eyeScale;
                float2 separation=float2(_Chroma*(.25+radius*3),0)*eyeScale;
                // Clamp within the eye rectangle; never bleed the other eye.
                float2 eyeMin=grabUV-eyeUV*eyeScale+eyeScale*.001;
                float2 eyeMax=eyeMin+eyeScale*.998;
                float3 scene;
                scene.r=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_OWV3SceneGrab,clamp(distorted+separation,eyeMin,eyeMax)).r;
                scene.g=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_OWV3SceneGrab,clamp(distorted,eyeMin,eyeMax)).g;
                scene.b=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_OWV3SceneGrab,clamp(distorted-separation,eyeMin,eyeMax)).b;
                float luminance=dot(scene,float3(.2126,.7152,.0722));
                scene=lerp(scene,luminance.xxx,_Desaturate);
                float scan=.5+.5*sin(eyeUV.y*620*6.2831853);
                scene*=1-_CRT*.075*scan;
                scene*=1-_Vignette*smoothstep(.06,.45,radius);
                scene*=float3(1+_Warmth*.12,1+_Warmth*.025,1-_Warmth*.08);
                float grain=(hash21(floor(eyeUV*float2(960,640))+floor(_Timeline*12))-.5)*.012*_CRT;
                scene+=grain;
                scene*=1-gate*_Dropout*.2;
                return fixed4(max(scene,0),saturate(_Intensity));
            }
            ENDCG
        }
    }
    Fallback Off
}
