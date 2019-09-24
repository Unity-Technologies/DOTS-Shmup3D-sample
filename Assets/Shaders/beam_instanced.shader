/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/beam_instanced" {
    Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
    }
	SubShader {
        Tags{ "Queue"="Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" "RenderPipeline" = "LightweightPipeline"}

		ZWrite Off
		Cull Off
		// Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		Blend SrcAlpha One 				// alpha additive
		
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

            uniform sampler2D _MainTex;

 			struct appdata_custom
            {
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

 			struct v2f
 			{
 				float4 pos:SV_POSITION;
 				fixed4 color:COLOR;
				float2 uv:TEXCOORD0;
 			};
 			
            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 model_matrix = unity_ObjectToWorld;
                uint col32 = asuint(model_matrix._m30);
                float4 col = float4(
                    ((col32>>0)&0xff)/255.0,
                    ((col32>>8)&0xff)/255.0,
                    ((col32>>16)&0xff)/255.0,
                    1);
                float width = model_matrix._m31;
                float length = model_matrix._m32;
                model_matrix._m30 = 0;
                model_matrix._m31 = 0;
                model_matrix._m32 = 0;

                float3 normal = float3(0, 0, 1);
				float3 tv = float3(0, 0, 0);
				float3 eye = ObjSpaceViewDir(float4(tv, 1));
                float inner_product = dot(normal, eye);
				float3 side = normalize(cross(eye, normal));
				float3 vert = normalize(cross(normal, side));
				tv += (v.texcoord.x-0.5f)*side * width;
				tv += (v.texcoord.y-0.5f)*vert * width * (inner_product > 0 ? 1 : -1);
                tv += v.vertex.xyz*length;
            	v2f o;
                o.pos = mul(UNITY_MATRIX_VP, mul(model_matrix, float4(tv.xyz, 1)));
				o.color = col;
				o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);
				
            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
				fixed alpha = tex2D(_MainTex, i.uv).a;
				fixed3 res = lerp(i.color.rgb, fixed3(1,1,1), alpha);
				return fixed4(res, i.color.a * alpha);
            }

            ENDCG
        }
    }
}

/*
 * End of spark.shader
 */
