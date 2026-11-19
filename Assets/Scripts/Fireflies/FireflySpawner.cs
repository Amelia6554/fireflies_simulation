using UnityEngine;

public class FireflySpawner : MonoBehaviour
{
    [Header("Spawner Setup")]
    public GameObject fireflyPrefab;
    public Transform fireflyContainer;

    private void Start()
    {
        if (fireflyContainer == null)
        {
            fireflyContainer = transform;
        }

        if (SimulationManager.Instance != null)
        {
            SyncPopulation(SimulationManager.Instance.populationSize);
        }
    }

    public void SyncPopulation(int targetCount)
    {
        if (fireflyPrefab == null || SimulationManager.Instance == null) return;

        var list = SimulationManager.Instance.allFireflies;
        float radius = SimulationManager.Instance.boundsRadius;

        // Dospawnowanie brakuj¹cych
        while (list.Count < targetCount)
        {
            Vector3 randomPosition = transform.position + Random.insideUnitSphere * radius;
            GameObject newObj = Instantiate(fireflyPrefab, randomPosition, Quaternion.identity, fireflyContainer);
            newObj.name = $"Firefly_{list.Count}";

            if (newObj.TryGetComponent(out Firefly firefly))
            {
                list.Add(firefly);
            }
            else
            {
                Destroy(newObj);
                Debug.LogError("Prefab œwietlika nie ma komponentu Firefly!");
                break;
            }
        }

        // Usuniêcie nadmiarowych
        while (list.Count > targetCount)
        {
            int lastIndex = list.Count - 1;
            Firefly toRemove = list[lastIndex];
            list.RemoveAt(lastIndex);

            if (toRemove != null)
            {
                Destroy(toRemove.gameObject);
            }
        }
    }
}