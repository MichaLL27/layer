// Ground material with rectangular "cut-outs": pixels inside up to three world-space rectangles
// (x0,x1,y0,y1), nearer than _CutZ, are discarded. Lets the cutaway hole, trench and riser open
// in the front face and the top surface without any real geometry boolean.
Shader "WMI/CutawayGround"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color;
        half _Glossiness;
        float4 _Cut0;
        float4 _Cut1;
        float4 _Cut2;
        float _CutZ;
        bool InRect(float4 r, float3 p)
        {
            return p.x > r.x && p.x < r.y && p.y > r.z && p.y < r.w && p.z < _CutZ;
        }
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            if (InRect(_Cut0, IN.worldPos) || InRect(_Cut1, IN.worldPos) || InRect(_Cut2, IN.worldPos))
                clip(-1);
            o.Albedo = _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
