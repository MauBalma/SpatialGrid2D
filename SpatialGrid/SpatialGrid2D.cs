using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ISpatialQuery2D
{
    Vector2 AABBFrom { get; }
    Vector2 AABBTo { get; }
    Func<Vector2, float, bool> Filter { get; }
}

public class SpatialGrid2D
{
    static readonly public Vector2Int Outside = new Vector2Int(-1, -1);

    public Vector2 Position { get; protected set; }

    public float x { get { return Position.x; } }
    public float y { get { return Position.y; } }

    public Vector2 Origin { get { return Position; } }
    public Vector2 End { get { return Origin + new Vector2(CellCant.x * CellDimensions.x, CellCant.y * CellDimensions.y); } }

    public Vector2 Size { get; protected set; }
    public Vector2Int CellCant { get; protected set; }

    public Vector2 CellDimensions { get { return new Vector2(Size.x / CellCant.x, Size.y / CellCant.y); } }

    private Dictionary<GridEntity2D, Vector2Int> lastPositions;
    private HashSet<GridEntity2D>[,] buckets;

    public SpatialGrid2D(Vector2 position, Vector2 size, Vector2Int cellCant)
    {
        Position = position;
        Size = size;
        CellCant = cellCant;

        lastPositions = new Dictionary<GridEntity2D, Vector2Int>();
        buckets = new HashSet<GridEntity2D>[CellCant.x, CellCant.y];

        for (int i = 0; i < CellCant.x; i++)
            for (int j = 0; j < CellCant.y; j++)
                buckets[i, j] = new HashSet<GridEntity2D>();

    }

    public void AddEntity(GridEntity2D entity)
    {
        entity.OnMove += UpdateEntity;
        UpdateEntity(entity);
    }

    protected bool InsideGrid(Vector2Int position)
    {
        return
            0 <= position.x && position.x < CellCant.x &&
            0 <= position.y && position.y < CellCant.y;
    }

    protected Vector2Int PositionInGrid(Vector3 position)
    {
        return Vector2Int.FloorToInt(new Vector2(
            (position.x - x) / CellDimensions.x,
            (position.y - y) / CellDimensions.y
        ));
    }

    public void RemoveEntity(GridEntity2D entity)
    {
        var prevCell = lastPositions.ContainsKey(entity) ? lastPositions[entity] : Outside;
        if (InsideGrid(prevCell))
        {
            buckets[prevCell.x, prevCell.y].Remove(entity);
        }
        entity.OnMove -= UpdateEntity;
    }

    public void UpdateEntity(GridEntity2D entity)
    {
        var prevCell = lastPositions.ContainsKey(entity) ? lastPositions[entity] : Outside;
        var curCell = PositionInGrid(entity.transform.position);

        if (prevCell.Equals(curCell)) return;

        //Entity was previously inside the grid and it will move from there
        if (InsideGrid(prevCell))
        {
            buckets[prevCell.x, prevCell.y].Remove(entity);
        }

        //Entity is now inside the grid, and just moved from prev cell, add it to the new cell
        if (InsideGrid(curCell))
        {
            buckets[curCell.x, curCell.y].Add(entity);
            lastPositions[entity] = curCell;
        }
        else
        {
            lastPositions.Remove(entity);
        }
    }

    public IEnumerable<GridEntity2D> Query(ISpatialQuery2D query)
    {
        return Query(query.AABBFrom, query.AABBTo, query.Filter);
    }

    public IEnumerable<GridEntity2D> Query(Vector2 aabbFrom, Vector2 aabbTo, Func<Vector2, float, bool> filter)
    {
        var from = new Vector2(Mathf.Min(aabbFrom.x, aabbTo.x), Mathf.Min(aabbFrom.y, aabbTo.y));
        var to = new Vector2(Mathf.Max(aabbFrom.x, aabbTo.x), Mathf.Max(aabbFrom.y, aabbTo.y));

        var fromCoord = PositionInGrid(from);
        var toCoord = PositionInGrid(to);

        //¡Ojo que clampea a 0,0 el Outside! TODO: Checkear cuando descartar el query si estan del mismo lado
        fromCoord.Clamp(Vector2Int.zero, CellCant);
        toCoord.Clamp(Vector2Int.zero, CellCant);

        if (!InsideGrid(fromCoord) && !InsideGrid(toCoord))
            return new GridEntity2D[0];

        var xrows = EnumerableUtils.Generate(fromCoord.x, x => x + 1)
            .TakeWhile(x => x < CellCant.x && x <= toCoord.x);

        var yrows = EnumerableUtils.Generate(fromCoord.y, y => y + 1)
            .TakeWhile(y => y < CellCant.y && y <= toCoord.y);

        var cells = xrows.SelectMany(
            xs => yrows.Select(ys => new Vector2Int(xs, ys))
        );

        return cells
            .SelectMany(cell => buckets[cell.x, cell.y])
            .Where(entity =>
                from.x <= entity.transform.position.x + entity.radius && entity.transform.position.x - entity.radius <= to.x &&
                from.y <= entity.transform.position.y + entity.radius && entity.transform.position.y - entity.radius <= to.y
            ).Where(entity => filter(entity.transform.position, entity.radius));
    }

}