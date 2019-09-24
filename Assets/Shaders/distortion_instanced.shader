/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/distortion_instanced" {
    Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Magnitude ("Magnitude", Range(0, 1)) = 1
    }
	SubShader {
        Tags{ "Queue"="Transparent-1" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" "RenderPipeline" = "LightweightPipeline"}

		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		// Blend SrcAlpha One 				// alpha additive
		
        Pass {
            Name "ForwardLit"
            Tags {"LightMode" = "LightweightForward"}

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase                       // This line tells Unity to compile this pass for forward base.
            #pragma multi_compile_instancing
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
 			#pragma target 3.0
 
 			#include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraOpaqueTexture;
			float _Magnitude;
			float     _CurrentTime;

 			struct appdata_custom
            {
                uint vertexID : SV_VertexID;
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

 			struct v2f
 			{
 				float4 pos:SV_POSITION;
				float2 uv:TEXCOORD0;
				float4 screenPos:TEXCOORD1;
 			};
 			
            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 model_matrix = unity_ObjectToWorld;
                float time = model_matrix._m30;
				float size = model_matrix._m31;
                model_matrix._m30 = 0;
                model_matrix._m31 = 0;
                model_matrix._m32 = 0;

                float elapsed = _CurrentTime - time;
                float vx = (float)(v.vertexID&1);
                float vy = (float)(v.vertexID/2);
                float3 up = float3(0, 1, 0);
				float3 tv = float3(0, 0, 0);
				float3 eye = normalize(ObjSpaceViewDir(float4(tv, 1)));
				float3 side = normalize(cross(eye, up));
				float3 vert = normalize(cross(side, eye));
				tv += (v.texcoord.x-0.5f) * side * size;
				tv += (v.texcoord.y-0.5f) * vert * size;

            	v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(tv.xyz + model_matrix._m03_m13_m23, 1));
                o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);
				o.screenPos = ComputeScreenPos(o.pos);
				
            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
				float2 dist = UnpackNormal(tex2D(_MainTex, i.uv)).rg;
				float2 uv = i.screenPos.xy/i.screenPos.w;
				uv += dist * _Magnitude * 0.01;
				fixed4 col = tex2D(_CameraOpaqueTexture, uv);
				col.a = 0.5;
				return col;
            }

            ENDCG
        }
    }
}

/*
 * End of spark.shader
 */
