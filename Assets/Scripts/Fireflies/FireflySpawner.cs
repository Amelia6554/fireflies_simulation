using UnityEngine;
using System.Collections.Generic;

public class FireflySpawner : MonoBehaviour
    {
        [Header("Population Parameters")]
        public GameObject fireflyPrefab;

        public int populationSize = 100;
        public float spawnRadius = 10f;

        void Start()
        {
            SpawnFireflies();
        }

        void SpawnFireflies()
        {
            for (int i = 0; i < populationSize; i++)
            {
                Vector3 randomPosition = transform.position + Random.insideUnitSphere * spawnRadius;
                
                GameObject newFirefly = Instantiate(fireflyPrefab, randomPosition, Quaternion.identity);
                newFirefly.name = "Firefly_" + i;

                SimulationManager.instance.allFireflies.Add(newFirefly.GetComponent<Firefly>());
            }
        }
    }