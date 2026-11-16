// Horizontal UV displacement for banners and trees in the Hub. Built-in render pipeline.
// With _SwayParams.xy = 0 the output equals Sprites/Default (premultiplied alpha, unlit).
// Every time-dependent value is supplied by HubLayerMotion, never by _Time, so a sample is repeatable.
Shader "Lumon/HubWorld/Sway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // x: main offset in UV, y: ripple offset in UV, z: held fraction of the height, w: 1 = top held, 0 = bottom held
        _SwayParams ("Sway (set by HubLayerMotion)", Vector) = (0,0,0,0)
        _SwayRipple ("Ripple turns along the free part", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _SwayParams;
            float _SwayRipple;

            v2f vert(appdata IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                // Distance from the held edge, 0 at the edge and 1 at the far end.
                float along = lerp(uv.y, 1.0 - uv.y, step(0.5, _SwayParams.w));
                float held = min(_SwayParams.z, 0.95);
                float weight = saturate((along - held) / (1.0 - held));
                weight *= weight;

                float shift = _SwayParams.x * weight
                    + _SwayParams.y * weight * sin(weight * _SwayRipple * 6.2831853);
                uv.x -= shift;

                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                // Nothing is borrowed from beyond the cut-out.
                c.a *= step(0.0, uv.x) * step(uv.x, 1.0);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
