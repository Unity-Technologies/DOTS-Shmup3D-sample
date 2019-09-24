/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/explosion_instanced" {
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

            sampler2D _MainTex;
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
 			};
 			
            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 model_matrix = unity_ObjectToWorld;
                float time = model_matrix._m30;
                float theta = model_matrix._m31;
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
                float width = 4;
				tv += (v.texcoord.x-0.5f) * side * width;
				tv += (v.texcoord.y-0.5f) * vert * width;

                /* rotate matrix for an arbitrary axis
                 * Vx*Vx*(1-cos) + cos      Vx*Vy*(1-cos) - Vz*sin    Vz*Vx*(1-cos) + Vy*sin;
                 * Vx*Vy*(1-cos) + Vz*sin    Vy*Vy*(1-cos) + cos     Vy*Vz*(1-cos) - Vx*sin;
                 * Vz*Vx*(1-cos) - Vy*sin    Vy*Vz*(1-cos) + Vx*sin    Vz*Vz*(1-cos) + cos;
                 */
                float s, c;
                sincos(theta, s, c);
                float3 n = eye;
                float3 n1c = n * (1-c);
                float3 ns = n * s;
                float3x3 mat = {
                    (n.x*n1c.x + c),   (n.x*n1c.y - ns.z), (n.z*n1c.x + ns.y),
                    (n.x*n1c.y + ns.z), (n.y*n1c.y + c),   (n.y*n1c.z - ns.x),
                    (n.z*n1c.x - ns.y), (n.y*n1c.z + ns.x),   (n.z*n1c.z + c),
                };
                tv = mul(mat, tv);

                float rW = 1.0/8.0;
                float rH = 1.0/8.0;
                float fps = 45;
                float loop0 = 1.0/(fps*rW*rH);
                elapsed = clamp(elapsed, 0, loop0);
                float texu = floor(elapsed*fps) * rW - floor(elapsed*fps*rW);
                float texv = 1 - floor(elapsed*fps*rW) * rH;
                texu += vx * rW;
                texv += -vy * rH;

            	v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(tv.xyz + model_matrix._m03_m13_m23, 1));
                o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, float2(texu, texv));
				
            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }

            ENDCG
        }
    }
}

/*
 * End of spark.shader
 */
