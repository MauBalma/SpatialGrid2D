using System;
using UnityEngine;

public class GridEntity2D : MonoBehaviour
{
    public event Action<GridEntity2D> OnMove = delegate { };

    public float radius;
    public SpatialGrid2D grid;

    protected Vector3 _lastPosition;

    void Start()
    {
        grid.AddEntity(this);
        _lastPosition = transform.position;
    }

    void Update()
    {
        if (transform.position != _lastPosition)
        {
            _lastPosition = transform.position;
            OnMove(this);
        }
    }

}