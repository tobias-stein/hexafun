using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HexagonField : MonoBehaviour
{
    #region Field Properties

    public Vector2Int                           size                = Vector2Int.one * 5;
    public Vector2Int                           origin              = Vector2Int.zero;
    public float                                scale               = 1.0f;
    public Hexagon.Orientation        orientation               = Hexagon.Orientation.PointyTop;
    public Hexagon.CoordinateSystem   coordinateSystem          = Hexagon.CoordinateSystem.Offset;

    #endregion // Field Properties
    
    public PlayerInput                          input               = null;
    public UIDocument                           ui                  = null;

    public Hexagon                              hexagon             { get; private set; } = null;
    private MeshRenderer                        meshRenderer        = null;

    private HexagonFieldController              controller          = null;
    private Coroutine                           update              = null;



    private void Start() 
    {
        this.meshRenderer       = this.gameObject.GetComponent<MeshRenderer>();

        this.controller         = new HexagonFieldController(this.input, this.ui, this);

        this.update             = this.StartCoroutine(this.updateHexField());
    }

    private IEnumerator updateHexField() 
    {
        yield return new WaitForEndOfFrame();
        
        var hexFieldColorTex    = new Texture2D(this.size.x, this.size.y, DefaultFormat.HDR, 0)
        {
            filterMode          = FilterMode.Point
        };

        this.meshRenderer.sharedMaterial.mainTexture = hexFieldColorTex;

        while(true) 
        {
            yield return new WaitForSeconds(0.01f);
            
            var newState        = this.controller.update(this.input);

            this.meshRenderer.sharedMaterial.SetVector("_HexFieldCursor", new Vector4(newState.hovered.x, newState.hovered.y, 0.0f, 0.0f));
            
            if(this.size.x == hexFieldColorTex.width && this.size.y == hexFieldColorTex.height)
            {
                hexFieldColorTex.SetPixels(newState.colors);
                hexFieldColorTex.Apply();
            }
        }
    }

    private void OnDestroy() 
    {
        this.StopCoroutine(this.update);
        this.update = null;
        this.controller = null;
    }

    public void Reset()
    {
        this.OnValidate();
    }

    public void OnValidate()
    { 
        if(this.update != null) { this.StopCoroutine(this.update); }

        this.hexagon          = new Hexagon(this.orientation, this.coordinateSystem, this.origin, this.scale);

        this.size.x = Mathf.Min(this.size.x, 1024);
        this.size.y = Mathf.Min(this.size.y, 1024);

        this.rebuildHexagonMesh();      

        this.update             = UnityEngine.Application.isPlaying ? this.StartCoroutine(this.updateHexField()) : null;  
    }

    private void rebuildHexagonMesh() 
    {   
        var mesh                = new Mesh();
        mesh.indexFormat        = IndexFormat.UInt32;  

        var vertices            = new List<Vector3>();
        var triangles           = new List<int>();
        var tileUVs             = new List<Vector2>();
        var fieldUVs            = new List<Vector2>();
        var barycentric         = new List<Color>();

        var patter              = new int[]
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 6,
            0, 6, 1
        };

        for(int z = 0; z < this.size.y; z++)
        {
            for(int x = 0; x < this.size.x; x++)
            {
                var coord = new Vector2Int(x, z) + this.origin;                

                triangles.AddRange(patter.Select(x => x + vertices.Count));
                vertices.AddRange(this.hexagon.computeCorners(coord, 0.0f, true));
                tileUVs.AddRange(this.hexagon.unitCornerUvs);
                fieldUVs.AddRange(Enumerable.Repeat(new Vector2((float)x, (float)z), 7).ToArray()); // this seems to mitigate/eliminate precision problem, when uvs -> hex-cell overlap
                // fieldUVs.AddRange(Enumerable.Repeat(new Vector2((float)x / (float)this.size.x, (float)z / (float)this.size.y), 7).ToArray());

                barycentric.AddRange(new Color[]
                {
                    Color.green, // center

                    Color.blue, // top (12 O'clock)
                    Color.red,  // top-left (2 O'clock)
                    Color.blue, // bottom-left (4 O'clock)
                    Color.red,  // bottom (6 O'clock)
                    Color.blue, // bottom-right (8 O'clock)
                    Color.red,  // top-right (10 O'clock)
                });
            }
        }

        mesh.vertices           = vertices.ToArray();
        mesh.uv                 = tileUVs.ToArray();
        mesh.uv2                = fieldUVs.ToArray();
        mesh.triangles          = triangles.ToArray();
        mesh.colors             = barycentric.ToArray();
        
        var meshFilter          = this.gameObject.GetComponent<MeshFilter>();
        meshFilter.sharedMesh   = mesh;

        var renderer            = this.gameObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial.SetVector("_HexFieldLayout",        new Vector4((int)this.coordinateSystem, (int)this.orientation, 0.0f, 0.0f));
        renderer.sharedMaterial.SetVector("_HexFieldSizeOrigin",    new Vector4(this.size.x, this.size.y, this.origin.x, this.origin.y));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() 
    {
        var style = new GUIStyle();
        style.normal.textColor = Color.red;

        var svcFrustrum = GeometryUtility.CalculateFrustumPlanes(SceneView.lastActiveSceneView.camera);

        for(int z = 0; z < this.size.y; z++)
        {
            for(int x = 0; x < this.size.x; x++)
            {
                var coord = new Vector2Int(x, z);
                var world = this.hexagon.hex2world(coord + this.origin);

                if(GeometryUtility.TestPlanesAABB(svcFrustrum, new Bounds(world, Vector3.one)))
                {
                    UnityEditor.Handles.Label(world, $"{coord.x},{coord.y}", style);
                }
            }
        }
    }
#endif
}