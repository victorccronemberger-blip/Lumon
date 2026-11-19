Shader "Lumon/Splat Pixel Pass"
{
    Properties
    {
        _MainTex ("World", 2D) = "white" {}
        _Dither ("Dither", Range(0,1)) = 0.24
        _EdgeStrength ("Selective outlines", Range(0,1)) = 0.13
        _DawnValley ("Painted mountain valley", 2D) = "white" {}
        _PaintSky ("Observatory sky", Float) = 0
        _ClearColor ("Camera background", Color) = (0.09, 0.14, 0.22, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Dither;
            float _EdgeStrength;
            float _PaintSky;
            sampler2D _DawnValley;
            float4 _ValleyView;
            float4 _ClearColor;

            float Luma(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float3 ObservatorySky(float2 uv)
            {
                // A mirrored two-turn panorama closes without a seam, also on a full orbit.
                float x = _ValleyView.x + (uv.x - 0.5) * _ValleyView.y;
                float2 sampleUV = float2(1.0 - abs(frac(x * 0.5) * 2.0 - 1.0),
                    saturate(0.48 + (uv.y - 0.5) * 0.83 + _ValleyView.z));
                float3 panorama = tex2D(_DawnValley, sampleUV).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    panorama = LinearToGammaSpace(panorama);
                #endif
                return panorama;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float2 offset = abs(_MainTex_TexelSize.xy) * 1.5;
                float center = Luma(source.rgb);
                float contrast =
                    abs(center - Luma(tex2D(_MainTex, i.uv + float2(offset.x, 0)).rgb)) +
                    abs(center - Luma(tex2D(_MainTex, i.uv - float2(offset.x, 0)).rgb)) +
                    abs(center - Luma(tex2D(_MainTex, i.uv + float2(0, offset.y)).rgb)) +
                    abs(center - Luma(tex2D(_MainTex, i.uv - float2(0, offset.y)).rgb));
                float edge = smoothstep(0.2, 0.75, contrast);
                float3 color = source.rgb * (1.0 - edge * _EdgeStrength);

                // Quantize in display space so dark materials retain their intermediate tones.
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = LinearToGammaSpace(color);
                #endif

                if (_PaintSky > 0.5)
                {
                    // Keep ink on the shadow side of a material boundary. One sample stride
                    // corresponds to the two physical pixels of the world camera's logical grid.
                    float2 stride = abs(_MainTex_TexelSize.xy) * 2.0;
                    float3 brightest = max(max(tex2D(_MainTex, i.uv + float2(stride.x,0)).rgb,
                        tex2D(_MainTex, i.uv - float2(stride.x,0)).rgb), max(
                        tex2D(_MainTex, i.uv + float2(0,stride.y)).rgb, tex2D(_MainTex, i.uv - float2(0,stride.y)).rgb));
                    float3 current = source.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                        brightest = LinearToGammaSpace(brightest);
                        current = LinearToGammaSpace(current);
                    #endif
                    float ink = smoothstep(0.055, 0.22, Luma(brightest) - Luma(current));
                    color = lerp(color, color * float3(0.61, 0.65, 0.73), ink * 0.48);
                }

                static const float bayer[16] = { 0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5 };
                uint2 pixel = uint2(i.pos.xy) % 4u;
                float threshold = ((bayer[pixel.y * 4 + pixel.x] + 0.5) / 16.0 - 0.5) * _Dither;
                float levels = _PaintSky > 0.5 ? 127.0 : 31.0;
                color = floor(saturate(color) * levels + 0.5 + threshold) / levels;
                if (_PaintSky > 0.5)
                {
                    float skyMask = 1.0 - smoothstep(0.004, 0.018, length(source.rgb - _ClearColor.rgb));
                    color = lerp(color, ObservatorySky(i.uv), skyMask);
                }

                #ifndef UNITY_COLORSPACE_GAMMA
                    color = GammaToLinearSpace(color);
                #endif
                return float4(color, source.a);
            }
            ENDCG
        }
    }
}
