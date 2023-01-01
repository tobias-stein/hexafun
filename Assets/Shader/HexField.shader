Shader "Unlit/HexShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _WireframeColor("Wireframe color", color) = (1.0, 1.0, 1.0, 1.0)
        _WireframeAliasing("Wireframe aliasing", float) = 1.5
        _HighlightBorderSize("Highlight Border Size", Range (0, 1)) = 0.1
        _HighlightBorderBlur("Highlight Border Blur", Range (1, 100)) = 1.0

        _HexFieldLayout("X = CoordinateSystem: 0 = offset, 1 = axial, Y = Orientation: 0 = pointy-top, 1 = flat-top", Vector) = (0.0, 0.0, 0.0, 0.0)
        _HexFieldSizeOrigin("Hex Field Size (XY) and Origin (ZW)", Vector) = (0.0, 0.0, 0.0, 0.0)
        _HexFieldBorderColorThreshold("Hex Field border color threshold", Range (0, 1)) = 0.01

        _HexFieldCursor("Hex Field Cursor", Vector) = (0.0, 0.0, 0.0, 0.0)
        _HexFieldCursorHighlightStrenth("Hex Field Cursor Highlight strength", Range (0, 1)) = 0.3
    }

    SubShader
    {
        Tags    
        { 
            "RenderType"    = "Opaque" 
            "Queue"         = "Transparent"
        }

        LOD     100
        Blend   SrcAlpha OneMinusSrcAlpha

        // Hexagon Grid
        Pass
        {
            CGPROGRAM

            #pragma vertex      vert
            #pragma fragment    frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex       : POSITION;
                float3 color        : COLOR0;
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                float3 color        : COLOR0;
            };

            fixed4      _WireframeColor;
            float       _WireframeAliasing;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex        = UnityObjectToClipPos(v.vertex);
                o.color         = v.color;
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate the unit width based on triangle size.
                float3 d = fwidth(i.color);
                //return fixed4(i.color.rgb, 1.0);

                // Alias the line a bit.
                // float3 aliased = step(d, i.color);
                float3 aliased = smoothstep(float3(0.0, 0.0, 0.0), d * _WireframeAliasing, i.color);

                return fixed4(_WireframeColor.r, _WireframeColor.g, _WireframeColor.b, _WireframeColor.a * (1.0 - aliased.r));
            }

            ENDCG
        }

        // Highlight
        Pass
        {
            CGPROGRAM

            #pragma vertex      vert
            #pragma fragment    frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex       : POSITION;
                float2 tileUV       : TEXCOORD0;
                float2 fieldUV      : TEXCOORD1;
                float3 color        : COLOR0;
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                float2 tileUV       : TEXCOORD0;
                float2 fieldUV      : TEXCOORD1;
                float3 barycentric  : COLOR0;
            };

            sampler2D   _MainTex;
            float2      _MainTex_TexelSize;

            float       _HighlightBorderSize;
            float       _HighlightBorderBlur;
          
            int4        _HexFieldLayout;
            float4      _HexFieldSizeOrigin;
            float       _HexFieldBorderColorThreshold;
            float4      _HexFieldCursor;
            float       _HexFieldCursorHighlightStrenth;

            
            v2f vert (appdata v)
            {
                v2f o;

                o.vertex        = UnityObjectToClipPos(v.vertex);
                o.tileUV        = v.tileUV;
                o.fieldUV       = v.fieldUV;
                o.barycentric   = v.color;

                return o;
            }

            float2 getHexTileUV(int2 hexTileCoord) { return float2(hexTileCoord.x / _HexFieldSizeOrigin.x, hexTileCoord.y / _HexFieldSizeOrigin.y); }

            void getHexTileNeighborCoords(int2 hexTileCoord, out int3 neighbors[6]) 
            { 
                const int4 axialOffsets[6] = 
                {
                    // XY = PT-A, ZW = FT-A   
                    int4( 0,  1,  1, -1),
                    int4( 1,  0,  0, -1),
                    int4( 1, -1, -1,  0),
                    int4( 0, -1, -1,  1),
                    int4(-1,  0,  0,  1),
                    int4(-1,  1,  1,  0),
                };

                const int4 offsetOffsets[2][2][6] = 
                {
                    // PT-O
                    {
                        // even-row
                        {
                            int4( 1,  1,  0,  1),
                            int4( 1,  0,  1,  0),
                            int4( 1, -1,  0, -1),
                            int4( 0, -1, -1, -1),
                            int4(-1,  0, -1,  0),
                            int4( 0,  1, -1,  1),
                        },
                        // odd-row
                        {
                            int4( 0,  1,  1,  1),
                            int4( 1,  0,  1,  0),
                            int4( 0, -1,  1, -1),
                            int4(-1, -1,  0, -1),
                            int4(-1,  0, -1,  0),
                            int4(-1,  1,  0,  1),
                        }
                    },

                    // FT-O
                    {
                        // even-col
                        {
                            int4( 1,  0,  1, -1),
                            int4( 0, -1,  0, -1),
                            int4(-1,  0, -1, -1),
                            int4(-1,  1, -1,  0),
                            int4( 0,  1,  0,  1),
                            int4( 1,  1,  1,  0),
                        },
                        // odd-col
                        {
                            int4( 1, -1,  1,  0),
                            int4( 0, -1,  0, -1),
                            int4(-1, -1, -1,  0),
                            int4(-1,  0, -1,  1),
                            int4( 0,  1,  0,  1),
                            int4( 1,  0,  1,  1),
                        }
                    }
                };
                
                int even            = _HexFieldLayout.y 
                    ? (int)(_HexFieldSizeOrigin.z) & 1  // FT-O 
                    : (int)(_HexFieldSizeOrigin.w) & 1; // PT-O

                int parity          = _HexFieldLayout.y
                    ? hexTileCoord.x & 1                // FT-O
                    : hexTileCoord.y & 1;               // PT-O

                for(int i = 0; i < 6; i++)
                {
                    int2 offset     = _HexFieldLayout.x
                        // axial coordinates (1)
                        ? _HexFieldLayout.y 
                            ? axialOffsets[i].zw 
                            : axialOffsets[i].xy
                        // offset coordinates (0)
                        : parity 
                            ? offsetOffsets[_HexFieldLayout.y][even][i].xy 
                            : offsetOffsets[_HexFieldLayout.y][even][i].zw;


                    int2 neighbor   = hexTileCoord + offset;
                    int valid       = neighbor.x > -1 && neighbor.x < _HexFieldSizeOrigin.x && neighbor.y > -1 && neighbor.y < _HexFieldSizeOrigin.y ? 1 : 0;
                    neighbors[i]    = int3(neighbor.x, neighbor.y, valid);
                }
            }
            
            fixed4 frag(v2f i) : SV_Target
            {                
                const float     kInvPI                  = 1.0 / 3.14159265359;
                const float     kInnerRadius            = 0.5; // https://en.wikipedia.org/wiki/Hexagon
                const float     kOuterRadius            = 0.866; 
                const float     kHexSegmentLength       = 1.0 / 6.0;
                const float2    hexVector               = float2(kInnerRadius, kOuterRadius);

                int2 hexTileCoord                       = int2(i.fieldUV.x, i.fieldUV.y);
                int  isSelected                         = hexTileCoord.x == _HexFieldCursor.x && hexTileCoord.y == _HexFieldCursor.y ? 1 : 0;

                int3        neighbors[6] =
                {
                    int3(0, 0, -1),
                    int3(0, 0, -1),
                    int3(0, 0, -1),
                    int3(0, 0, -1),
                    int3(0, 0, -1),
                    int3(0, 0, -1)
                };

                // return fixed4(i.tileUV.x,  i.tileUV.y,  0.0, 1.0);
                // return fixed4(i.fieldUV.x, i.fieldUV.y, 0.0, 1.0);

                // source: https://gist.github.com/paulhoux/560190d258b99bed03b55be5dfa41904

                float2  ncp                             = float2(i.tileUV.x * 2.0 - 1.0, i.tileUV.y * 2.0 - 1.0); // [-1;1]

                float2  p                               = abs(ncp);
                float   d                               = max(dot(p, hexVector), p.x);// hexgagon distance function
                float   w                               = _HighlightBorderBlur * fwidth(d);

                // return fixed4(1.0, 0.0, 0.0, step(d, kOuterRadius));

                float   cutout                          = kOuterRadius - _HighlightBorderSize;
                float   angle01                         = ((atan2(ncp.x, ncp.y) * kInvPI) + 1.0) * 0.5;

                // return fixed4(angle01, angle01, angle01, 1.0);

                float   segment                         = 0.0;

                float2  halfTexelSize                   = _MainTex_TexelSize * 0.5;
                float4  hexTileColor                    = tex2D(_MainTex, getHexTileUV(hexTileCoord) + float2(halfTexelSize.x * i.tileUV.x, halfTexelSize.y * i.tileUV.y));

                // return fixed4(hexTileColor.a ? hexTileColor.rgb : fixed3(1.0, 1.0, 1.0), isSelected ? _HexFieldCursorHighlightStrenth : 0.1);


                // segment                                 = step(kHexSegmentLength * 5, angle01) * step(angle01, kHexSegmentLength * (5 + 1));
                // return fixed4(angle01, angle01, angle01, segment);

                if(hexTileColor.a > 0)  
                {   
                    for(int segmentIndex                = 0; segmentIndex < 6; segmentIndex++)
                    {               
                        segment                         = step(kHexSegmentLength * segmentIndex, angle01) * step(angle01, kHexSegmentLength * (segmentIndex + 1));
                        if(segment)
                        {
                            getHexTileNeighborCoords(hexTileCoord, neighbors);

                            // valid neighbor
                            if(neighbors[segmentIndex].z)
                            {
                                float2 neighborUV       = getHexTileUV(neighbors[segmentIndex]);
                                float4 neighborColor    = tex2D(_MainTex, neighborUV + float2(halfTexelSize.x * i.tileUV.x, halfTexelSize.y * i.tileUV.y));

                                segment                 = length(hexTileColor.rgb - neighborColor.rgb) > _HexFieldBorderColorThreshold ? segment : 0.0;
                            }

                            break;
                        }
                    }   
                }

                float   hexagon                         = (smoothstep(kOuterRadius + w, kOuterRadius - w, d) - smoothstep(cutout + w, cutout - w, d)) * segment;

                return fixed4(hexTileColor.a ? hexTileColor.rgb : fixed3(1.0, 1.0, 1.0), max(isSelected ? _HexFieldCursorHighlightStrenth : 0.1, hexagon));
            }

            ENDCG
        }
    }
}
