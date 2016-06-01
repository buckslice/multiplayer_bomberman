using UnityEngine;
using System.Collections.Generic;

public class Level : MonoBehaviour {

    public LevelData ld = null;

    private int powerUpPercent = 20;

    [SerializeField]
    private Texture2D[] textures;
    [SerializeField]
    private Object bombPrefab;
    [SerializeField]
    private Object explosionPrefab;
    [SerializeField]
    private Object fireUpPrefab;
    [SerializeField]
    private Object bombUpPrefab;

    public bool needToRebuild { private get; set; }

    private Dictionary<int, Bomb> bombs = new Dictionary<int, Bomb>();

    private Texture2D atlas;
    private Rect[] atlasRects;

    private Mesh mesh;
    private List<int> tris = new List<int>();
    private List<Vector3> verts = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();
    private int triNum = 0;
    private float rebuildTimer = 0.0f;

    public void Awake() {

        atlas = new Texture2D(1024, 1024);
        atlasRects = atlas.PackTextures(textures, 2, 1024);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        GetComponent<MeshRenderer>().material.mainTexture = atlas;

        Camera.main.transform.position = new Vector3(LevelData.width / 2.0f, 17.0f, -1.0f) * LevelData.SIZE;
        Camera.main.transform.rotation = Quaternion.Euler(60.0f, 0.0f, 0.0f);

    }

    // builds mesh from tile data
    public void buildMesh() {
        if (!mesh) {
            Destroy(mesh);
        }
        if (ld == null) {
            return;
        }
        verts.Clear();
        tris.Clear();
        uvs.Clear();
        triNum = 0;

        int[] tiles = ld.getTiles();
        float SIZE = LevelData.SIZE;
        for (int y = 0; y < LevelData.height; y++) {
            for (int x = 0; x < LevelData.width; x++) {
                int id = tiles[x + y * LevelData.width];
                float h = ld.getHeight(x, y) * SIZE;
                float xf = x * SIZE;
                float yf = y * SIZE;

                verts.Add(new Vector3(xf, h, yf));
                verts.Add(new Vector3(xf, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf));

                addUvsAndTris(id, x, y);

                // if height not equal zero check if neighbors are lower to add a wall down that side
                if (h > 0.0f) {
                    if (ld.getHeight(x + 1, y) == 0) { // right neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));

                        addUvsAndTris(id, x + 1, y);
                    }

                    if (ld.getHeight(x - 1, y) == 0) { // left neighbor
                        verts.Add(new Vector3(xf, 0, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf));
                        verts.Add(new Vector3(xf, 0, yf));

                        addUvsAndTris(id, x - 1, y);
                    }

                    if (ld.getHeight(x, y + 1) == 0) { // top neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, 0, yf + SIZE));

                        addUvsAndTris(id, x, y + 1);
                    }

                    if (ld.getHeight(x, y - 1) == 0) { // bottom neighbor
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
        if (index == LevelData.BOMB) {
            index = LevelData.GROUND;
        }
        if (index == LevelData.POWERUP) {
            index = LevelData.GROUND;
        }
        if (index == LevelData.GROUND && (x + y) % 2 == 0) {
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

    // figure out which tile 'pos' is in
    // then place bomb prefab there
    public void placeBomb(Vector3 pos, bool thisPlayers, int bombRange) {
        float SIZE = LevelData.SIZE;
        int x = (int)(pos.x / SIZE);
        int y = (int)(pos.z / SIZE);

        if (ld.getTile(x, y) != LevelData.GROUND) {   // if not on ground or outside of tile array then return
            return;
        }
        ld.setTile(x, y, LevelData.BOMB);

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, 0.0f, yf);

        GameObject go = (GameObject)Instantiate(bombPrefab, spawn, Quaternion.identity);
        go.name = "Bomb";
        go.tag = thisPlayers ? "PlayerBomb" : "Bomb";
        Bomb b = go.GetComponent<Bomb>();
        b.init(x, y, this, bombRange);
        bombs.Add(y * LevelData.width + x, b);
    }

    public void placePowerUp(Vector3 pos, int type) {
        float SIZE = LevelData.SIZE;
        int x = (int)(pos.x / SIZE);
        int y = (int)(pos.z / SIZE);

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, 1f, yf);

        ld.setTile(x, y, LevelData.POWERUP);
        if (type == 1) {
            GameObject go = (GameObject)Instantiate(fireUpPrefab, spawn, Quaternion.identity);
            go.name = "FireUp";
            PowerUp p = go.GetComponent<PowerUp>();
            p.init(x, y, this, type);
        } else {
            GameObject go = (GameObject)Instantiate(bombUpPrefab, spawn, Quaternion.identity);
            go.name = "FireUp";
            PowerUp p = go.GetComponent<PowerUp>();
            p.init(x, y, this, type);
        }
    }

    public void spawnExplosion(int x, int y, int dx, int dy, int life) {
        int id = ld.getTile(x, y);
        if (id == LevelData.WALL) {    // this explosion hit a wall
            return;
        }
        ld.setTile(x, y, LevelData.GROUND);
        if (id == LevelData.WALL_CRACKED) {
            needToRebuild = true;
            if (Random.value < powerUpPercent / 100f) // handling for powerup spawning
            {
                float xf2 = x * LevelData.SIZE + LevelData.SIZE * 0.5f;
                float yf2 = y * LevelData.SIZE + LevelData.SIZE * 0.5f;
                if (Random.value < 0.5f) {
                    placePowerUp(new Vector3(xf2, LevelData.SIZE * 0.5f, yf2), 1);
                } else {
                    placePowerUp(new Vector3(xf2, LevelData.SIZE * 0.5f, yf2), 2);
                }
            }

            life = 0; // reduce life of explosion to zero so it wont spread anymore
        }
        if (id == LevelData.BOMB) {    // this explosion hit a bomb so blow bomb up now
            bombs[y * LevelData.width + x].explode();
            bombs.Remove(y * LevelData.width + x);
            return;
        }

        float xf = x * LevelData.SIZE + LevelData.SIZE * 0.5f;
        float yf = y * LevelData.SIZE + LevelData.SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, LevelData.SIZE * 0.5f, yf);
        GameObject go = (GameObject)Instantiate(explosionPrefab, spawn, Quaternion.identity);
        go.name = "Explosion";
        go.GetComponent<Explosion>().start(x, y, dx, dy, life, this);
    }

    void LateUpdate() {
        rebuildTimer -= Time.deltaTime;
        if (needToRebuild || rebuildTimer < 0.0f) {
            buildMesh();
            rebuildTimer = 1.0f;
            needToRebuild = false;
        }
    }

}
