using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldSpawner : MonoBehaviour
{
    public GameObject CurrentWorld;

    [SerializeField] List<GameObject> WorldsList;

    private int clickQ;
    private bool cr_running;
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
        clickQ++;
        if (!cr_running)
            StartCoroutine(ExpandWorld());
    }
    IEnumerator ExpandWorld()
    {
        cr_running = true;
        while (clickQ > 0)
        {
            transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

            yield return new WaitForSeconds(.05f);

            ResetScale();
        }
        cr_running = false;
    }

    private void ResetScale()
    {
        transform.localScale = new Vector3(1f, 1f, 1f);
        clickQ--;
    }

    public void spawnObject(GameObject objectToSpawn)
    {
        Debug.Log("Spawning object");
    }
}
