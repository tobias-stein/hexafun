using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class HexagonFieldController
{
    public enum HexagonTileType 
    {
        None = 0,

        Start,
        End,

        Gras,
        Moud,
        Water,
        Wall,
    };

    public struct HexagonTile
    {
        public HexagonTileType      type;
        public Color                color;
        public float                cost;

        public bool                 isPath;
    }

    public class HexagonFieldState 
    {
        internal HexagonTile[]      tiles;

        public Vector2Int           hovered { get; internal set; } = Vector2Int.zero;

        public  Color[]             colors 
        { 
            get 
            { 
                return this.tiles.Select(tile => tile.isPath ? Color.yellow : tile.color).ToArray(); 
            }
        }

        public HexagonFieldState(Vector2Int size)
        {
            this.tiles = new HexagonTile[size.x * size.y];

            for(int y = 0; y < size.y; y++)
            {
                for(int x = 0; x < size.x; x++)
                {
                    this.tiles[y * size.x + x] = HexagonFieldController.Type2Tile[HexagonTileType.Gras];
                }
            }
        }
    }


    internal static readonly Dictionary<HexagonTileType, HexagonTile> Type2Tile = null;

    static HexagonFieldController() 
    {
        HexagonFieldController.Type2Tile = new Dictionary<HexagonTileType, HexagonTile>()
        {
            { HexagonTileType.Start,    new HexagonTile { type = HexagonTileType.Start, color = Color.blue,     cost = 0.0f } },
            { HexagonTileType.End,      new HexagonTile { type = HexagonTileType.End,   color = Color.magenta,  cost = 0.0f } },

            { HexagonTileType.Gras,     new HexagonTile { type = HexagonTileType.Gras,  color = Color.green,    cost = 1.0f } },
            { HexagonTileType.Moud,     new HexagonTile { type = HexagonTileType.Moud,  color = Color.red,      cost = 2.0f } },
            { HexagonTileType.Water,    new HexagonTile { type = HexagonTileType.Water, color = Color.cyan,     cost = 4.0f } },
            { HexagonTileType.Wall,     new HexagonTile { type = HexagonTileType.Gras,  color = Color.black,    cost = float.NaN } },
        };
    }

    private HexagonField        field;

    private HexagonFieldState   state;

    private readonly Plane      groundPlane = new Plane(Vector3.up, 0);

    private HexagonTile         paint       = HexagonFieldController.Type2Tile[HexagonTileType.Wall];

    private Vector2Int?         hoveredHex  = null;

    private bool                edit        = false;

    private Vector2Int?         start       = null;
    private Vector2Int?         end         = null;


    public HexagonFieldController(PlayerInput input, UIDocument ui, HexagonField field)
    {
        this.field = field;
        this.start = null;
        this.end   = null;
        this.state = new HexagonFieldState(field.size);
        
        #region UI/INPUT Boilerplate code

        input.actions["Look"].performed += ctx => 
        {
            var screenPos = ctx.ReadValue<Vector2>();

            float distance;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (groundPlane.Raycast(ray, out distance))
            {
                // update hex field curser position
                var worldPos = ray.GetPoint(distance);
                var hexCoord = this.field.hexagon.world2hex(worldPos) - this.field.origin;
                
                // make sure hex-coord is on field
                if(hexCoord.x >= 0 && hexCoord.x < this.field.size.x && hexCoord.y >= 0 && hexCoord.y < this.field.size.y)
                {
                    this.hoveredHex = this.state.hovered = hexCoord;
                    return;
                }

                this.hoveredHex = null;
            }
        };

        var uiEdit = ui.rootVisualElement.Q<Toggle>("edit");
        uiEdit.RegisterValueChangedCallback((value) => { this.toggleEdit(); });
        input.actions["ToggleEdit"].performed += ctx => { this.toggleEdit(); uiEdit.SetValueWithoutNotify(this.edit); };

        var uiCoord = ui.rootVisualElement.Q<RadioButtonGroup>("coordinate");
        uiCoord.RegisterValueChangedCallback((value) => this.toogleCoordinate());
        input.actions["ToggleCoordinate"].performed += ctx => { this.toogleCoordinate(); uiCoord.SetValueWithoutNotify((int)this.field.coordinateSystem); };

        var uiOrient = ui.rootVisualElement.Q<RadioButtonGroup>("orientation");
        uiOrient.RegisterValueChangedCallback((value) => this.toogleOrientation());
        input.actions["ToggleOrientation"].performed += ctx => { this.toogleOrientation(); uiOrient.SetValueWithoutNotify((int)this.field.orientation); };

        var uiTile = ui.rootVisualElement.Q<DropdownField>("tiletype");
        uiTile.RegisterValueChangedCallback((value) => 
        {
            object result;
            if(Enum.TryParse(typeof(HexagonTileType), value.newValue, true, out result))
            {
                this.paint = HexagonFieldController.Type2Tile[(HexagonTileType)result];
            }
        });

        var uiSize = ui.rootVisualElement.Q<SliderInt>("size");
        uiSize.RegisterValueChangedCallback((value) => 
        { 
            this.field.size = Vector2Int.one * value.newValue;
            this.field.origin = Vector2Int.one * (-value.newValue / 2);
            this.field.OnValidate();

            var tmp = new HexagonTile[this.field.size.x * this.field.size.y];

            for(int y = 0; y < this.field.size.y; y++)
            {
                for(int x = 0; x < this.field.size.x; x++)
                {
                    var id = y * this.field.size.x + x;
                    tmp[id] = HexagonFieldController.Type2Tile[HexagonTileType.Gras];
                }
            }
            this.state.tiles = tmp;
        });

        var uiScale = ui.rootVisualElement.Q<Slider>("scale");
        uiScale.RegisterValueChangedCallback((value) => 
        { 
            this.field.scale = value.newValue;
            this.field.OnValidate();
        });

        var uiZoom = ui.rootVisualElement.Q<Slider>("zoom");
        uiZoom.RegisterValueChangedCallback((value) => { Camera.main.transform.position = new Vector3(0.0f, value.newValue, 0.0f); });

        var uiPitch = ui.rootVisualElement.Q<Slider>("pitch");
        uiPitch.RegisterValueChangedCallback((value) => { Camera.main.transform.rotation = Quaternion.Euler(value.newValue, 0.0f, 0.0f); });

        input.actions["Zoom"].performed += ctx => 
        {
            var scrol = ctx.ReadValue<Vector2>();
            Camera.main.transform.position = new Vector3(0.0f, Mathf.Clamp(Camera.main.transform.position.y + scrol.y, 1.0f, 300.0f), 0.0f);
            uiZoom.SetValueWithoutNotify(Camera.main.transform.position.y);
        };


        #endregion
    }

    private void toggleEdit()
    {
        this.edit = !this.edit;
    }

    private void toogleCoordinate() 
    {
        this.field.coordinateSystem = this.field.coordinateSystem == Hexagon.CoordinateSystem.Offset 
            ? Hexagon.CoordinateSystem.Axial 
            : Hexagon.CoordinateSystem.Offset;

        this.field.OnValidate();
    }

    private void toogleOrientation() 
    {
        this.field.orientation = this.field.orientation == Hexagon.Orientation.PointyTop 
            ? Hexagon.Orientation.FlatTop 
            : Hexagon.Orientation.PointyTop;

        this.field.OnValidate();
    }

    public HexagonFieldState update(PlayerInput input)
    {
        // ignore, if pointer is over ui
        if(EventSystem.current.currentSelectedGameObject != null) { return this.state; }

        if(this.edit)
        { 
            if(input.actions["Select"].IsPressed())
            {
                if(this.hoveredHex != null) 
                {
                    var hexCoord = this.hoveredHex.Value;
                    this.state.tiles[hexCoord.y * this.field.size.x + hexCoord.x] = this.paint;

                    if(this.start != null && this.start.Value == hexCoord) { this.start = null; }
                    if(this.end   != null && this.end.Value   == hexCoord) { this.end   = null; }

                    this.astar();
                }
            }

            if(input.actions["Erase"].IsPressed())
            {
                if(this.hoveredHex != null) 
                {
                    var hexCoord = this.hoveredHex.Value;
                    this.state.tiles[ hexCoord.y * this.field.size.x + hexCoord.x] = HexagonFieldController.Type2Tile[HexagonTileType.Gras];

                    if(this.start != null && this.start.Value == hexCoord) { this.start = null; }
                    if(this.end   != null && this.end.Value   == hexCoord) { this.end   = null; }

                    this.astar();
                }
            }
        }
        else 
        {
            if(input.actions["Select"].IsPressed())
            {
                if(this.hoveredHex != null) 
                {
                    var hexCoord = this.hoveredHex.Value;

                    // clear old start location, if any
                    if(this.start != null)
                    {
                        this.state.tiles[this.start.Value.y * this.field.size.x + this.start.Value.x] = HexagonFieldController.Type2Tile[HexagonTileType.Gras];
                    }

                    this.start = hexCoord;
                    if(this.end != null && this.start.Value == this.end.Value) { this.end = null; }

                    this.state.tiles[hexCoord.y * this.field.size.x + hexCoord.x] = HexagonFieldController.Type2Tile[HexagonTileType.Start];

                    this.astar();
                }
            }

            if(input.actions["Erase"].IsPressed())
            {
                if(this.hoveredHex != null) 
                {
                    var hexCoord = this.hoveredHex.Value;

                    // clear old end location, if any
                    if(this.end != null)
                    {
                        this.state.tiles[this.end.Value.y * this.field.size.x + this.end.Value.x] = HexagonFieldController.Type2Tile[HexagonTileType.Gras];
                    }

                    this.end = hexCoord;
                    if(this.start != null && this.end.Value == this.start.Value) { this.start = null; }

                    this.state.tiles[hexCoord.y * this.field.size.x + hexCoord.x] = HexagonFieldController.Type2Tile[HexagonTileType.End];

                    this.astar();
                }
            }
        }

        return this.state;
    }

    /// <summary>
    /// source; https://www.redblobgames.com/pathfinding/a-star/introduction.html
    /// </summary>
    private void astar()
    {
        // clear old path
        for(int i = 0; i < this.state.tiles.Length; i++) { this.state.tiles[i].isPath = false; }

        // start and end tiles are required to be defined
        if(this.start == null || this.end == null) { return; } 

        // frontier = PriorityQueue()
        // frontier.put(start, 0)
        var frontier = new List<Tuple<Vector2Int, float>>() { new Tuple<Vector2Int, float>(this.start.Value, 0.0f) };

        // came_from = dict()
        var came_from = new Dictionary<Vector2Int, Vector2Int?>();
        // cost_so_far = dict()
        var cost_so_far = new Dictionary<Vector2Int, float>();
        // came_from[start] = None
        came_from.Add(this.start.Value, null);
        // cost_so_far[start] = 0
        cost_so_far.Add(this.start.Value, 0.0f);

        // while not frontier.empty():
        while(frontier.Count > 0)
        {
            // current = frontier.get()
            var current = frontier.First().Item1;
            frontier.RemoveAt(0);

            // if current == goal:
            //     break
            if(current == this.end.Value) { break; }
            
            // for next in graph.neighbors(current):
            var neighbors = this.field.hexagon
                .neighbors(current)
                // remove all neighbors out of bounds and tiles that have no NaN costs (e.g walls)
                .Where(neighbor => neighbor.x >= 0 && neighbor.y >= 0 && neighbor.x < this.field.size.x && neighbor.y < this.field.size.y && !float.IsNaN(this.state.tiles[neighbor.y * this.field.size.x + neighbor.x].cost));

            foreach(var next in neighbors)
            {
                //     new_cost = cost_so_far[current] + graph.cost(current, next)
                var new_cost = cost_so_far[current] + this.state.tiles[next.y * this.field.size.x + next.x].cost;
            
                //     if next not in cost_so_far or new_cost < cost_so_far[next]:
                if(!cost_so_far.ContainsKey(next) || new_cost < cost_so_far[next])
                {
                    //         cost_so_far[next] = new_cost
                    if(cost_so_far.ContainsKey(next)) { cost_so_far[next] = new_cost; } else { cost_so_far.Add(next, new_cost); }
                    
                    //         priority = new_cost + heuristic(goal, next)
                    float priority = new_cost + this.field.hexagon.distance(this.end.Value, next);

                    //         frontier.put(next, priority)
                    frontier.Add(new Tuple<Vector2Int, float>(next, priority));
                    frontier.Sort((a, b) => { return (int)(a.Item2 - b.Item2); });

                    //         came_from[next] = current
                    if(came_from.ContainsKey(next)) { came_from[next] = current; } else { came_from.Add(next, current); }
                }
            }

            bool valid = true;
            Vector2Int? from = this.end.Value;
            var path = new List<Vector2Int>();
            while(valid && from != null)
            {
                path.Add(from.Value);
                valid = came_from.TryGetValue(from.Value, out from);
            }

            // check if there is a path
            if(valid)
            {
                path.RemoveAt(0); // remove end
                path.Reverse();
                path.RemoveAt(0); // remove start
                
                // mark all tiles of this path
                foreach(var coord in path) { this.state.tiles[coord.y * this.field.size.x + coord.x].isPath = true; }
            }
        }
    }
}
