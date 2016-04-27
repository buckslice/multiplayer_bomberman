using UnityEngine;
using System.Collections.Generic;

public class Level : MonoBehaviour {

    public const int width = 23;
    public const int height = 17;
    public const float SIZE = 2.0f; // game unit size of each tile

    //private int[,] tiles;
    private int[] tiles;

    public Texture2D[] textures;
    public Object bombPrefab;
    public Object explosionPrefab;
    public bool needToRebuild { private get; set; }

    public Dictionary<int, Bomb> bombs = new Dictionary<int, Bomb>();

    public Transform player;
    private Texture2D atlas;
    private Rect[] atlasRects;

    public const int GROUND = 0;
    public const int WALL = 1;
    public const int WALL_CRACKED = 2;
    public const int BOMB = 3;

    private Mesh mesh;
    private List<int> tris = new List<int>();
    private List<Vector3> verts = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();
    private int triNum = 0;


    // Use this for initialization
    void Awake() {
        atlas = new Texture2D(1024, 1024);
        atlasRects = atlas.PackTextures(textures, 2, 1024);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        GetComponent<MeshRenderer>().material.mainTexture = atlas;

        Camera.main.transform.position = new Vector3(width / 2.0f, 12.0f, -1.0f) * SIZE;
        Camera.main.transform.rotation = Quaternion.Euler(60.0f, 0.0f, 0.0f);
        tiles = new int[width * height];
        //GenerateLevel();

        //player = GameObject.Find("Player").transform;
    }

    void Start() {
        Pathfinder.instance.init(width, height);
    }

    // builds tile array
    public void GenerateLevel() {
        tiles = new int[width * height];


        // generate board
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1 || (x % 2 == 0 && y % 2 == 0)) { 
                    setTile(x, y, WALL);    // if at edge or random chance
                } else if (Random.value < .2f) {
                    setTile(x, y, WALL_CRACKED);    // random chance
                } else {
                    setTile(x, y, GROUND);
                }
            }
        }
        BuildMesh();
    }


    // builds mesh from tile data
    public void BuildMesh() {
        if (!mesh) {
            Destroy(mesh);
        }
        verts.Clear();
        tris.Clear();
        uvs.Clear();
        triNum = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int id = tiles[x + y * width];
                float h = getHeight(x, y) * SIZE;
                float xf = x * SIZE;
                float yf = y * SIZE;

                verts.Add(new Vector3(xf, h, yf));
                verts.Add(new Vector3(xf, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf));

                addUvsAndTris(id, x, y);

                // if height not equal zero check if neighbors are lower to add a wall down that side
                if (h > 0.0f) {
                    if (getHeight(x + 1, y) == 0) { // right neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));

                        addUvsAndTris(id, x + 1, y);
                    }

                    if (getHeight(x - 1, y) == 0) { // left neighbor
                        verts.Add(new Vector3(xf, 0, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf));
                        verts.Add(new Vector3(xf, 0, yf));

                        addUvsAndTris(id, x - 1, y);
                    }

                    if (getHeight(x, y + 1) == 0) { // top neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, 0, yf + SIZE));

                        addUvsAndTris(id, x, y + 1);
                    }

                    if (getHeight(x, y - 1) == 0) { // bottom neighbor
                        verts.Add(new Vector3(xf, 0, yf));
                        verts.Add(new Vector3(xf, h, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf));
                        verts.Add(new Vector3(xf + SIZE, 0, yf));

                        addUvsAndTris(id, x, y - 1);
                    }
                }
            }
        }

        // build mesh and collider
        mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void addUvsAndTris(int index, int x, int y) {
        if (index == BOMB) {
            index = GROUND;
        }
        if (index == GROUND && (x + y) % 2 == 0) {
            index = 3;  // hardcoded as the index of the ground_dark texture for now
            // should make a map or something so we could have random wall textures and stuff too
        }

        Rect r = atlasRects[index];

        uvs.Add(new Vector2(r.xMin, r.yMin));
        uvs.Add(new Vector2(r.xMin, r.yMax));
        uvs.Add(new Vector2(r.xMax, r.yMax));
        uvs.Add(new Vector2(r.xMax, r.yMin));

        tris.Add(triNum);
        tris.Add(triNum + 1);
        tris.Add(triNum + 2);
        tris.Add(triNum + 2);
        tris.Add(triNum + 3);
        tris.Add(triNum);

        triNum += 4;
    }

    // returns whether or not x,y is inside tile array
    private bool insideLevel(int x, int y) {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // if inside level and on a walkable tile
    public bool isWalkable(int x, int y) {
        return getTile(x,y) == GROUND;
    }

    private int getHeight(int x, int y) {
        switch (getTile(x,y)) {
            case WALL:
            case WALL_CRACKED:
                return 1;
            default:
                return 0;
        }
    }

    // safely check for tile id in array
    public int getTile(int x, int y) {
        if (!insideLevel(x, y)) {
            return -1;
        }
        return tiles[x + y * width];
    }

    // sets tile at x,y to id
    public void setTile(int x, int y, int id) {
        if (!insideLevel(x, y)) {
            return;
        }
        tiles[x + y * width] = id;
    }

    public void setTile(int i, int id)
    {
        tiles[i] = id;
    }

    // returns 1d tile position in array based on pos
    public int getTilePos(Vector3 pos) {
        return (int)(pos.z / SIZE) * width + (int)(pos.x / SIZE);
    }

    public Vector3 getRandomGroundPosition() {
        List<int> spots = new List<int>();
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (getTile(x, y) == GROUND) {
                    spots.Add(x + y * width);
                }
            }
        }
        int r = spots[Random.Range(0, spots.Count)];
        return new Vector3(r % width, 0.1f, r / width) * SIZE + Vector3.one * SIZE * 0.5f;
    }

    // with larger radius the random point will be more centered in the square
    public Vector3 getRandomPointInTile(int x, int y, float radius) {
        if (!insideLevel(x, y)) {
            return new Vector3(x * SIZE, 0f, y * SIZE);
        }
        float minx = x * SIZE + radius;
        float miny = y * SIZE + radius;
        float maxx = (x + 1) * SIZE - radius;
        float maxy = (y + 1) * SIZE - radius;
        return new Vector3(Random.Range(minx,maxx), 0f, Random.Range(miny,maxy));
    }

    // figure out which tile 'pos' is in
    // then place bomb prefab there
    public void placeBomb(Vector3 pos) {
        int x = (int)(pos.x / SIZE);
        int y = (int)(pos.z / SIZE);

        if (getTile(x, y) != GROUND) {   // if not on ground or outside of tile array then return
            return;
        }
        setTile(x, y, BOMB);

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, 0.0f, yf);

        GameObject go = (GameObject)Instantiate(bombPrefab, spawn, Quaternion.identity);
        go.name = "Bomb";
        Bomb b = go.GetComponent<Bomb>();
        b.init(x, y, this);
        bombs.Add(y * width + x, b);
    }

    public void spawnExplosion(int x, int y, int dx, int dy, int life) {
        int id = getTile(x, y);
        if (id == WALL) {    // this explosion hit a wall
            return;
        }
        setTile(x, y, GROUND);
        if (id == WALL_CRACKED) {
            needToRebuild = true;
            life = 0; // reduce life of explosion to zero so it wont spread anymore
        }
        if (id == BOMB) {    // this explosion hit a bomb so blow bomb up now
            bombs[y * width + x].explode();
            bombs.Remove(y * width + x);
            return;
        }

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, SIZE * 0.5f, yf);
        GameObject go = (GameObject)Instantiate(explosionPrefab, spawn, Quaternion.identity);
        go.name = "Explosion";
        go.GetComponent<Explosion>().start(x, y, dx, dy, life, this);
    }

    void LateUpdate() {
        if (needToRebuild) {
            BuildMesh();
            needToRebuild = false;
        }
    }

    public int[] getTiles()
    {
        return tiles;
    }

    public void setTiles(int[] t)
    {
        tiles = t;
    }

}
