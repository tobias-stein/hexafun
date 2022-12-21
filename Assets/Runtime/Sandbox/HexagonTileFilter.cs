using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Light-weight representation of a signle hexagon tile.
/// </summary>
public class HexagonTileFilter 
{
    public enum Orientation { PointyTop, FlatTop };
    
    public enum CoordinateSystem { Offset, Axial }

    public readonly Orientation         orientation         = Orientation.FlatTop;
    public readonly CoordinateSystem    coordinateSystem    = CoordinateSystem.Offset;
    private Vector2Int                  origin              = Vector2Int.zero;

    private readonly Vector3[]          unitCornerOffset; // exclude center
    public readonly Vector2[]           unitCornerUvs; // include center UV
    
    #region Construct new hexagon tile

    // TODO: consider the origin to determine even/odd spacing
    public HexagonTileFilter(Orientation orientation, CoordinateSystem coordinateSystem, Vector2Int origin, float scale = 1.0f)
    {
        this.orientation        = orientation;
        this.coordinateSystem   = coordinateSystem;
        this.origin             = origin;
        this.D                  = scale;

        Vector3 firstUnitCorner = this.orientation == Orientation.PointyTop 
            // put first unit corner on the top
            ? new Vector3(0.0f, 0.0f, this.R) 
            // put first unit corner on the right (yields a flat-top)
            : new Vector3(this.R, 0.0f, 0.0f);

        // pre-calculate all unit corners in object-space
        this.unitCornerOffset   = new Vector3[6]
        {
            firstUnitCorner,
            Quaternion.AngleAxis( 60.0f, Vector3.up) * firstUnitCorner,
            Quaternion.AngleAxis(120.0f, Vector3.up) * firstUnitCorner,
            Quaternion.AngleAxis(180.0f, Vector3.up) * firstUnitCorner,
            Quaternion.AngleAxis(240.0f, Vector3.up) * firstUnitCorner,
            Quaternion.AngleAxis(300.0f, Vector3.up) * firstUnitCorner
        };

        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        uvs.AddRange(this.unitCornerOffset.Select(corner => 
        { 
            // -1;+1
            corner = this.orientation == Orientation.FlatTop 
                ? Quaternion.AngleAxis( 90.0f, Vector3.up) * corner 
                : Quaternion.AngleAxis(180.0f, Vector3.up) * corner;
                
            //  0;+1
            return new Vector2(((corner.x / this.R) + 1.0f) * 0.5f, ((corner.z / this.R) + 1.0f) * 0.5f);
        }));

        this.unitCornerUvs      = uvs.ToArray();
    }

    #endregion

    #region Parameters [https://en.wikipedia.org/wiki/Hexagon]

    /// <summary>
    /// The maximal diameter (which corresponds to the long diagonal of the hexagon), D, 
    /// is twice the maximal radius or circumradius, R, which equals the side length, t. 
    /// The minimal diameter or the diameter of the inscribed circle (separation of parallel sides, 
    /// flat-to-flat distance, short diagonal or height when resting on a flat base), d, is twice the minimal radius or inradius, r.
    /// </summary>
    public readonly float D = 1.0f;

    public float R { get { return this.D * 0.5f; } }

    public float t { get { return this.R; } }

    public float d { get { return Mathf.Sqrt(3.0f) * this.R; } }

    public float r { get { return this.d * 0.5f; } }

    #endregion

    private Vector2Int axial2offset(Vector2Int coord)
    {
        return new Vector2Int(
            this.orientation == Orientation.PointyTop 
                ? coord.x + ((coord.y + (coord.y % 2 == 0 ? 1 : 0)) / 2) - (coord.y < 0 ? 1 : 0)
                : coord.x,
            this.orientation == Orientation.PointyTop 
                ? coord.y
                : coord.y + ((coord.x + (coord.x % 2 == 0 ? 1 : 0)) / 2) - (coord.x < 0 ? 1 : 0)
        ); 
    }

    private Vector2Int offset2axial(Vector2Int coord)
    {
        return new Vector2Int(
            this.orientation == Orientation.PointyTop 
                ? coord.x - ((coord.y + (coord.y % 2 == 0 ? 1 : 0)) / 2) - (coord.y < 0 ? 1 : 0)
                : coord.x,
            this.orientation == Orientation.PointyTop 
                ? coord.y
                : coord.y - ((coord.x + (coord.x % 2 == 0 ? 1 : 0)) / 2) - (coord.x < 0 ? 1 : 0)
        ); 
    }

    public int distance(Vector2Int a, Vector2Int b)
    {
        if(this.coordinateSystem == CoordinateSystem.Offset)
        {
            a = this.offset2axial(a); 
            b = this.offset2axial(b); 
        }

        // axial coord dinstace
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y) + Mathf.Abs(a.y - b.y)) / 2;
    }

    public Vector2Int world2hex(Vector3 point)
    {
        Vector2Int coord = Vector2Int.zero;

        // source: https://www.redblobgames.com/grids/hexagons/#pixel-to-hex
        // Mathf.Sqrt(3.0f) / 3.0f == 0.57735026919
        const float sqrt3Div3 = 0.57735026919f;

        var q = this.orientation == Orientation.PointyTop 
            ? (sqrt3Div3  * point.x -  0.333333f * point.z) / this.R
            : ( 0.666666f * point.x                       ) / this.R;
        var r = this.orientation == Orientation.PointyTop 
            ? (                        0.666666f * point.z) / this.R
            : (-0.333333f * point.x +  sqrt3Div3 * point.z) / this.R;

        // source: https://observablehq.com/@jrus/hexround
        int xGrid = Mathf.RoundToInt(q);
        int yGrid = Mathf.RoundToInt(r);
        float xRem = q - xGrid;
        float yRem = r - yGrid;

        float xRemSqr = xRem * xRem;
        float yRemSqr = yRem * yRem;

        int dx = Mathf.RoundToInt(xRem + 0.5f * yRem) * (xRemSqr >= yRemSqr ? 1 : 0);
        int dy = Mathf.RoundToInt(yRem + 0.5f * xRem) * (xRemSqr <  yRemSqr ? 1 : 0);

        coord.x = xGrid + dx;
        coord.y = yGrid + dy;
        
        if(this.coordinateSystem == CoordinateSystem.Offset)
        {
            coord = this.axial2offset(coord); 
        }

        return coord;
    }

    public Vector3 hex2world(Vector2Int coordinate)
    {
        // see here for reference: https://www.redblobgames.com/grids/hexagons/#basics
        Vector3 center = Vector3.zero;

        bool evenCol = coordinate.x % 2 == 0;
        bool evenRow = coordinate.y % 2 == 0;

        // note: order of operations for offet computation matters!
        switch(this.orientation)
        {
            case Orientation.PointyTop:
            {
                center.z = coordinate.y * this.R * 1.5f;

                switch(this.coordinateSystem)
                {
                    case CoordinateSystem.Offset:
                    {
                        center.x = (coordinate.x * this.d) + (!evenRow ? this.r : 0.0f);
                        break;
                    }

                    case CoordinateSystem.Axial:
                    {
                        // q = coordinate.x
                        // r = coordinate.y
                        center.x = (coordinate.x * this.d) + (coordinate.y * this.r);
                        break;
                    }
                }
                
                break;
            }
            
            case Orientation.FlatTop: 
            {
                center.x = coordinate.x * this.R * 1.5f;
                switch(this.coordinateSystem)
                {
                    case CoordinateSystem.Offset:
                    {
                        center.z = (coordinate.y * this.d) + (!evenCol ? this.r : 0.0f);
                        break;
                    }

                    case CoordinateSystem.Axial:
                    {
                        // q = coordinate.x
                        // r = coordinate.y
                        center.z = (coordinate.y * this.d) + (coordinate.x * this.r);
                        break;
                    }
                }
                break;
            }
        }

        return center;
    }

    public Vector3[] computeCorners(Vector2Int coordinate, float height = 0.0f, bool includeCenter = false) 
    {
        // see here for reference: https://www.redblobgames.com/grids/hexagons/#basics
        Vector3 center = this.hex2world(coordinate);
        center.y = height;

        /*  Pointy-Top     Flat-Top
              0              4___5   
            5/ \1           3/   \0
            |   |            \___/
            4\ /2            2   1
              3
        */
        var corners = new Vector3[6] 
        {
            this.unitCornerOffset[0] + center,
            this.unitCornerOffset[1] + center,
            this.unitCornerOffset[2] + center,
            this.unitCornerOffset[3] + center,
            this.unitCornerOffset[4] + center,
            this.unitCornerOffset[5] + center
        };

        return includeCenter ? corners.Prepend(center).ToArray() : corners;
    } 



    #region Lookup Tables

    private static readonly Vector2Int[][] axialOffsets = new Vector2Int[2][]
    {
        // PT-A 
        new Vector2Int[6]
        {
            new Vector2Int( 0,  1),
            new Vector2Int( 1,  0),
            new Vector2Int( 1, -1),
            new Vector2Int( 0, -1),
            new Vector2Int(-1,  0),
            new Vector2Int(-1,  1)
        },
        // FT-A  
        new Vector2Int[6]
        {
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1,  0),
            new Vector2Int(-1,  1),
            new Vector2Int(0,  1),
            new Vector2Int(1,  0)
        }
    };

    private static readonly Vector2Int[][][][] offsetOffsets = new Vector2Int[2][][][] 
    {
        // PT-O
        new Vector2Int[2][][] 
        {
            // even-row
            new Vector2Int[2][]
            {
                new Vector2Int[6]
                {
                    new Vector2Int( 1,  1),
                    new Vector2Int( 1,  0),
                    new Vector2Int( 1, -1),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int( 0,  1)
                },

                new Vector2Int[6]
                {
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  0),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int(-1,  1)
                }
            },

            // odd-row
            new Vector2Int[2][]
            {
                new Vector2Int[6]
                {
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  0),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int(-1,  1)
                },

                new Vector2Int[6]
                {
                    new Vector2Int( 1,  1),
                    new Vector2Int( 1,  0),
                    new Vector2Int( 1, -1),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int( 0,  1)
                }
            }
        },

        // FT-O
        new Vector2Int[2][][] 
        {
            // even-col
            new Vector2Int[2][]
            {
                new Vector2Int[6]
                {
                    new Vector2Int( 1,  0),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int(-1,  1),
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  1)
                },

                new Vector2Int[6]
                {
                    new Vector2Int( 1, -1),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  0)
                }
            },
            // odd-col
            new Vector2Int[2][]
            {
                new Vector2Int[6]
                {
                    new Vector2Int( 1, -1),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  0)
                },
                new Vector2Int[6]
                {
                    new Vector2Int( 1,  0),
                    new Vector2Int( 0, -1),
                    new Vector2Int(-1,  0),
                    new Vector2Int(-1,  1),
                    new Vector2Int( 0,  1),
                    new Vector2Int( 1,  1)
                },  
            }
        }
    };

    #endregion

    /// <summary>
    /// Copy paste from shader code
    /// </summary>
    /// <param name="coordinate"></param>
    /// <returns></returns>
    public Vector2Int[] neighbors(Vector2Int coordinate)
    {
        var neighbors = new List<Vector2Int>();
        
        int even      = (this.orientation == Orientation.FlatTop ? this.origin.x : this.origin.y) & 1;
        int parity    = (this.orientation == Orientation.FlatTop ? coordinate.x : coordinate.y) & 1;

        for(int i = 0; i < 6; i++)
        {
            Vector2Int offset = this.coordinateSystem == CoordinateSystem.Axial 
                ? axialOffsets[(int)this.orientation][i] 
                : offsetOffsets[(int)this.orientation][even][1 - parity][i];

            neighbors.Add(coordinate + offset);
        }

        return neighbors.ToArray();

    }
}