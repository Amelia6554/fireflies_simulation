using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager instance;

        [Header("Simulation")] public float couplingStrenghth = 2.0f;
        public float visionRadius = 2.0f;
        
        //Spacial Grid
        private Dictionary<Vector3Int, List<Firefly>> grid = new Dictionary<Vector3Int, List<Firefly>>();
        public List<Firefly> allFireflies = new List<Firefly>();

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            float cellSize = Mathf.Max(0.1f, visionRadius);

            foreach (var list in grid.Values)
            {
                list.Clear();
            }

            foreach (var firefly in allFireflies)
            {
                Vector3Int cell = GetCellCord(firefly.transform.position, cellSize);
                if (!grid.ContainsKey(cell)) grid[cell] = new List<Firefly>();
                
                grid[cell].Add(firefly);
            }

            foreach (var firefly in allFireflies)
            {
                firefly.CalculateKuramoto(GetNeighbors(firefly.transform.position, cellSize));                
            }

            foreach (var firefly in allFireflies)
            {
                firefly.UpdatePhaseAndVisuals();
            }
        }
        
        private Vector3Int GetCellCord(Vector3 pos, float size)
        {
            return new Vector3Int(
                Mathf.FloorToInt(pos.x / size),
                Mathf.FloorToInt(pos.z / size),
                Mathf.FloorToInt(pos.y / size)
            );
        }
        
        private List<Firefly> GetNeighbors(Vector3 pos, float size)
        {
            List<Firefly> neighbors = new List<Firefly>();
            Vector3Int centerCell = GetCellCord(pos, size);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, z);
                        if (grid.TryGetValue(cell, out List<Firefly> cellFireflies))
                        {
                            neighbors.AddRange(cellFireflies);
                        }
                    }
                }
            }

            return neighbors;
        }
    }