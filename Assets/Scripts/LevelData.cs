using UnityEngine;
using System.Collections.Generic;

public class LevelData {
    public Vector3 levelOrigin;

    public const int width = 23;
    public const int height = 17;
    public const float SIZE = 2.0f; // game unit size of each tile

    public const int GROUND = 0;
    public const int WALL = 1;
    public const int WALL_CRACKED = 2;
    public const int BOMB = 3;
    public const int POWERUP = 4;

    private int[] tiles;

    public LevelData() {
        tiles = new int[width * height];
    }

    // builds tile array
    public void generateLevel() {
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
        return new Vector3(Random.Range(minx, maxx), 0f, Random.Range(miny, maxy));
    }

    // returns whether or not x,y is inside tile array
    private bool insideLevel(int x, int y) {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // if inside level and on a walkable tile
    public bool isWalkable(int x, int y) {
        return getTile(x, y) == GROUND;
    }

    public int getHeight(int x, int y) {
        switch (getTile(x, y)) {
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

    public void setTile(int i, int id) {
        tiles[i] = id;
    }

    // returns 1d tile position in array based on pos
    public int getTilePos(Vector3 pos) {
        return (int)(pos.z / SIZE) * width + (int)(pos.x / SIZE);
    }

    public int[] getTiles() {
        return tiles;
    }
}
