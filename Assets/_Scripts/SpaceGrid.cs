using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceGrid
{
    private int gridSubdivisions;
    private int cellCount;
    private Vector2 cellSize;
    private Vector2 gridOrigin;
    private int[,] cellStars;
    private int[] cellStarCount;
    private int[] starCellHashes;

    public SpaceGrid(Vector2 universeSize, Vector2 gridOrigin, int gridSubdivisions, int starCount)
    {
        this.gridOrigin = gridOrigin;
        this.gridSubdivisions = gridSubdivisions;
        cellCount = gridSubdivisions * gridSubdivisions;
        cellSize = universeSize / gridSubdivisions;

        cellStars = new int[cellCount, starCount];
        cellStarCount = new int[cellCount];
        starCellHashes = new int[starCount];
    }

    public void AddStarToGrid(int starIndex)
    {
        Vector2 starLocation = Simulation.Instance.GetStarPosition(starIndex);
        Vector2 gridCoordinates = CalculateGridCoordinates(starLocation);
        int starCellHash = GetCellHash(gridCoordinates);
        starCellHashes[starIndex] = starCellHash;
        cellStars[starCellHash, cellStarCount[starCellHash]] = starIndex;
        cellStarCount[starCellHash] += 1;
    }

    public int?[] GetAdjacentCellHashesFromStar(int starIndex)
    {
        int starCellHash = starCellHashes[starIndex];
        return GetAdjacentCellHashes(starCellHash);
    }
    
    public int GetCellStarCount(int cellHash)
    {
        return cellStarCount[cellHash];
    }

    public int GetCellStar(int cellHash, int starIndex)
    {
        return cellStars[cellHash, starIndex];
    }

    private int GetCellHash(Vector2 gridCoordinates)
    {
        return GetCellHash((int)gridCoordinates.x, (int)gridCoordinates.y);
    }

    private int GetCellHash(int cellX, int cellY)
    {
        return cellX + cellY * gridSubdivisions;
    }

    private Vector2 CalculateGridCoordinates(Vector2 worldposition)
    {
        // Consider a new coordinate system, where the origin was moved
        Vector2 gridOriginPosition = worldposition - gridOrigin;

        int cellX = (int)(gridOriginPosition.x / cellSize.x);
        int cellY = (int)(gridOriginPosition.y / cellSize.y);

        // If given worldposition corresponds to an extreme value
        // then an incorrect value (gridSubdivisions) is returned
        cellX = Mathf.Clamp(cellX, 0, gridSubdivisions - 1);
        cellY = Mathf.Clamp(cellY, 0, gridSubdivisions - 1);

        return new Vector2(cellX, cellY);
    }

    private Vector2 GetCellGridCoordinates(int cellHash)
    {
        int cellY = Mathf.FloorToInt(cellHash / gridSubdivisions);
        int cellX = cellHash - cellY * gridSubdivisions;
        return new Vector2(cellX, cellY);
    }

    private int?[] GetAdjacentCellHashes(Vector2 gridCoordinates)
    {
        return GetAdjacentCellHashes(GetCellHash(gridCoordinates));
    }

    private int?[] GetAdjacentCellHashes(int cellHash)
    {
        const int MAX_NEIGHBORS = 9;
        // In a 2 dimensional grid, a cell can have up to 9 neighbors including itself
        // 0 1 2 
        // 3 4 5
        // 6 7 8
        int?[] neighbors = new int?[MAX_NEIGHBORS];
        Vector2 gridCoordinates = GetCellGridCoordinates(cellHash);
        int index = 0;

        // Shift through the 3 rows and columns
        for (int row = -1; row <= 1; row++)
        {

            for (int column = -1; column <= 1; column++)
            {
                Vector2 displacement = new Vector2(row, column);
                Vector2 neighborGridCoordinates = gridCoordinates + displacement;

                bool rowExists = neighborGridCoordinates.x >= 0 && neighborGridCoordinates.x < gridSubdivisions;
                bool columnExists = neighborGridCoordinates.y >= 0 && neighborGridCoordinates.y < gridSubdivisions;

                if (rowExists && columnExists)
                {
                    neighbors[index] = GetCellHash(neighborGridCoordinates);
                }

                index++;
            }
        }

        return neighbors;
    }
}
