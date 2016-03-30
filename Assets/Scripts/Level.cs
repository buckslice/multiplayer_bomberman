using UnityEngine;
using System.Collections.Generic;

public class Level : MonoBehaviour {

    const int width = 23;
    const int height = 17;
    const float SIZE = 2.0f;

    int[,] tiles;

    public Texture2D[] textures;
    public Object bombPrefab;

    Texture2D atlas;
    Rect[] atlasRects;

    const int GROUND = 0;
    const int WALL = 1;
    const int WALL_CRACKED = 2;
    const int BOMB = 3;

    Mesh mesh;

    List<int> tris = new List<int>();
    List<Vector3> verts = new List<Vector3>();
    List<Vector2> uvs = new List<Vector2>();
    int triNum = 0;


    // Use this for initialization
    void Start() {
        atlas = new Texture2D(1024, 1024);
        atlasRects = atlas.PackTextures(textures, 2, 1024);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        GetComponent<MeshRenderer>().material.mainTexture = atlas;

        Camera.main.transform.position = new Vector3(width / 2.0f, 12.0f, -1.0f) * SIZE;
        Camera.main.transform.rotation = Quaternion.Euler(60.0f, 0.0f, 0.0f);

        GenerateLevel();
    }

    // builds tile array
    public void GenerateLevel() {
        tiles = new int[width, height];

        // generate board
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1 || (x % 2 == 0 && y % 2 == 0)) {
                    tiles[x, y] = WALL;     // if at edge or random chance
                } else if (Random.value < .1f) {
                    tiles[x, y] = WALL_CRACKED;   // random chance
                } else {
                    tiles[x, y] = GROUND;
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
                int id = tiles[x, y];
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
        if(index == BOMB) {
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

    private int getHeight(int x, int y) {
        if (x < 0 || x >= tiles.GetLength(0) || y < 0 || y >= tiles.GetLength(1)) {
            return 0;
        }
        switch (tiles[x, y]) {
            case WALL:
            case WALL_CRACKED:
                return 1;
            default:
                return 0;
        }
    }

    // safely check for tile id in array
    private int getTile(int x, int y) {
        if (x < 0 || x >= tiles.GetLength(0) || y < 0 || y >= tiles.GetLength(1)) {
            return -1;
        }
        return tiles[x, y];
    }

    // figure out which tile 'pos' is in
    // then place bomb prefab there
    public void placeBomb(Vector3 pos) {
        int x = (int)(pos.x / SIZE);
        int z = (int)(pos.z / SIZE);

        if (getTile(x, z) != GROUND) {   // if not on ground or outside of tile array then return
            return;
        }

        tiles[x, z] = BOMB;

        float xf = x * SIZE + SIZE * 0.5f;
        float zf = z * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, 0.0f, zf);

        GameObject go = (GameObject)Instantiate(bombPrefab, spawn, Quaternion.identity);
        go.GetComponent<Bomb>().init(x, z, this);
        go.name = "Bomb";
    }

    // explodes bomb from outward from tile at x,y
    public void explodeBomb(int x, int y) {
        tiles[x, y] = GROUND;   // set tile back to ground

        int dist = 2;   // distance the bomb explosion travels
        bool rebuild = false;
        for (int dir = 0; dir < 4; dir++) {
            for (int i = 0; i <= dist; i++) {
                int cx, cy;
                switch (dir) {
                    case 0: cx = x + i; cy = y; break;
                    case 1: cx = x - i; cy = y; break;
                    case 2: cx = x; cy = y + i; break;
                    case 3: cx = x; cy = y - i; break;
                    default: cx = cy = -1; break;
                }
                int id = getTile(cx, cy);
                if (id == WALL) {
                    break;  // stop this direction if it hits wall
                } else if (id == WALL_CRACKED) {
                    tiles[cx, cy] = GROUND;
                    rebuild = true;
                }
            }
        }
        if (rebuild) {
            BuildMesh();
        }
    }

}
