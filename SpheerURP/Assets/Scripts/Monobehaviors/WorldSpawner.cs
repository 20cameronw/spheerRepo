using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldSpawner : MonoBehaviour
{
    public GameObject CurrentWorld;

    [SerializeField] List<GameObject> WorldsList;
    private void OnEnable() => EventManager.OnClicked += ExpandAndShrink;

    private void OnDisable() => EventManager.OnClicked -= ExpandAndShrink;

    public void SetCurrentWorld(int WorldIndex)
    {
        if (CurrentWorld == null)
        {
            CurrentWorld = WorldsList[WorldIndex];
            SpawnWorld();
        }
        else
        {
            DeleteCurrentWorld();
            CurrentWorld = WorldsList[WorldIndex];
            SpawnWorld();
        }
    }

    private void DeleteCurrentWorld()
    {
        Destroy(CurrentWorld);
    }

    public void SpawnWorld()
    {
        Instantiate(CurrentWorld, transform);
    }

    private void ExpandAndShrink()
    {
        StartCoroutine(ExpandWorld());
    }
    IEnumerator ExpandWorld()
    {
        transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

        yield return new WaitForSeconds(.02f);

        ResetScale();
    }

    private void ResetScale()
    {
        if (transform.localScale != new Vector3(1f, 1f, 1f))
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    public void spawnObject(GameObject objectToSpawn)
    {
        Debug.Log("Spawning object");
    }
}
