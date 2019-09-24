Shader "Custom/ground_with_fog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry+1" }
		Cull Off
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode" = "LightweightForward"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog // make fog work
			// #pragma fragmentoption ARB_precision_hint_nicest  // seems not working.
 			#pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                fixed4 vertex : POSITION;
                fixed2 uv : TEXCOORD0;
            };

            struct v2f
            {
                fixed2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
                fixed4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            fixed4 _MainTex_ST;

			//
            //    https://briansharpe.wordpress.com/2011/11/15/a-fast-and-simple-32bit-floating-point-hash-function/
			//    FAST_32_hash
			//    A very fast 2D hashing function.  Requires 32bit support.
			//
			//    The hash formula takes the form....
			//    hash = mod( coord.x * coord.x * coord.y * coord.y, SOMELARGEFIXED ) / SOMELARGEFIXED
			//    We truncate and offset the domain to the most interesting part of the noise.
            //
			//    arranged by Yuji
            //
			float4 hash4fast(float2 gridcell)
			{
				const float2 OFFSET = float2(26.0, 161.0);
				const float DOMAIN = 71.0;
				const float SOMELARGEFIXED = 951.135664;
				float4 P = float4(gridcell.xy, gridcell.xy + 1);
				P = frac(P*(1/DOMAIN)) * DOMAIN;
				P += OFFSET.xyxy;                                //    offset to interesting part of the noise
				P *= P;                                          //    calculate and return the hash
				return frac(P.xzxz * P.yyww * (1/SOMELARGEFIXED));
			}

            v2f vert(appdata v)
            {
		        float3 forward = -UNITY_MATRIX_V._m20_m21_m22;
				float3 campos = _WorldSpaceCameraPos;
				float center_distance = abs(_ProjectionParams.z - _ProjectionParams.y) * 0.5;
                float3 center = campos + forward * (center_distance - abs(_ProjectionParams.y));
				float3 pos = float3(v.vertex.x*center_distance + center.x,
									0, // ground level
									v.vertex.z*center_distance + center.z);
				//pos = (v.vertex.xyz*100) + campos + forward * 100;
                v2f o;
                o.vertex = UnityWorldToClipPos(pos);
                o.uv = TRANSFORM_TEX(pos.xz*float2(1.0/16.0, 1.0/16.0), _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
				// generate per-tile transform
                // int2 iuv = int2(floor(i.uv));
				// fixed4 ofa = hash4(iuv + int2(0,0));
		        float4 ofa = hash4fast(floor(i.uv));
    
				// transform per-tile uvs
				// sign like this is NO good: ofa.zw = sign(ofa.zw-0.5);
				// because the sign may return zero.
				ofa.zw = ((step(0.5, ofa.zw)) - 0.5) * 2;

				// uv
				float2 fuv = frac(i.uv);
				float2 uva = fuv * ofa.zw + ofa.xy;

				// derivatives for correct mipmapping
				float2 ddxa = ddx(i.uv) * ofa.zw;
				float2 ddya = ddy(i.uv) * ofa.zw;
                
				// fetch
				fixed4 col = tex2Dgrad(_MainTex, uva, ddxa, ddya);

                // col = tex2D(_MainTex, i.uv);

                UNITY_APPLY_FOG(i.fogCoord, col);
// #if UNITY_REVERSED_Z
// 				col *= float4(1, 0.5, 0.5, 1);
// #else
// 				col *= float4(0.5, 1, 0.5, 1);
// #endif
				return col;
			}

            fixed4 frag_normal_fetch(v2f i) : SV_Target
            {
				fixed4 col = tex2D(_MainTex, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}

            ENDCG
        }
    }
}
