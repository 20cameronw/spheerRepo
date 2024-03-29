using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldSpawner : MonoBehaviour
{
    [HideInInspector]
    public GameObject CurrentWorldGO;

    [HideInInspector]
    public GameObject CurrentWorld;

    [Header("Setup Fields")]
    [SerializeField] List<GameObject> WorldsList;
    [SerializeField] private List<GameObject> structuresGOList;
    [SerializeField] private SphereCollider surface;

    [Space(10)]
    [Header("Orbit Settings")]
    [SerializeField] private SphereCollider orbitSC;
    [SerializeField] private float xOrbitSpeed;
    [SerializeField] private float yOrbitSpeed;
    [SerializeField] private float zOrbitSpeed;

    private GameObject orbitGO;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    private int clickQ;
    private bool cr_running;
    private void OnEnable() => EventManager.OnClicked += ExpandAndShrink;

    private void OnDisable() => EventManager.OnClicked -= ExpandAndShrink;

    private void Start()
    {
        spawnOrbit();
    }

    void spawnOrbit()
    {
        orbitGO = new GameObject("OrbitGO");
        orbitGO.transform.SetParent(transform);
        orbitGO.transform.position = transform.position;
        orbitGO.AddComponent<Rotate>();
        orbitGO.GetComponent<Rotate>().SetSpeeds(xOrbitSpeed, yOrbitSpeed, zOrbitSpeed);
    }

    public void SetCurrentWorld(int WorldIndex)
    {
        if (CurrentWorldGO == null)
        {
            CurrentWorldGO = WorldsList[WorldIndex];
            SpawnWorld();
        }
        else
        {
            DeleteCurrentWorld();
            CurrentWorldGO = WorldsList[WorldIndex];
            SpawnWorld();
        }
    }

    private void DeleteCurrentWorld()
    {
        Destroy(CurrentWorldGO);
    }

    public void SpawnWorld()
    {
        CurrentWorld = Instantiate(CurrentWorldGO, transform);
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

    public void spawnObject(int index, float passive)
    {
        if (TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].isInOrbit)
            spawnInOrbit(index, passive);
        else
            spawnOnSurface(index, passive);
    }

    public void spawnOnSurface(int index, float passive)
    {
        //Debug.Log("Spawning object");
        Vector3 spawnPosition = UnityEngine.Random.onUnitSphere * surface.radius + CurrentWorld.transform.position;
        Quaternion spawnRotation = Quaternion.identity;
        GameObject newObject = Instantiate(structuresGOList[index], spawnPosition, spawnRotation) as GameObject;
        newObject.transform.SetParent(CurrentWorld.transform);
        newObject.transform.LookAt(CurrentWorld.transform.position);
        newObject.transform.Rotate(-90, 0, 0);
        spawnedObjects.Add(newObject);
        newObject.gameObject.name = TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name;

        if (passive > 0)
        {
            AddsPassive passiveEarner = newObject.AddComponent<AddsPassive>();
            passiveEarner.setAmount(passive);
        }
        
    }

    public void LoadObjects(int count, int index)
    {
        StartCoroutine(delayLoadObjects(count, index));
    }

    private IEnumerator delayLoadObjects(int count, int index)
    {
        yield return new WaitForSeconds(.5f);
        SpawnManyObjects(count, index);
    }

    public void SpawnManyObjects(int count, int index)
    {
        for (int i = 0; i < count; i++)
        {
            spawnObject(index, TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].bonus);
        }
    }

    public void spawnInOrbit(int index, float passive)
    {
        Vector3 spawnPosition = UnityEngine.Random.onUnitSphere * orbitSC.radius + orbitGO.transform.position;
        Quaternion spawnRotation = Quaternion.identity;
        GameObject newObject = Instantiate(structuresGOList[index], spawnPosition, spawnRotation) as GameObject;
        newObject.transform.SetParent(orbitGO.transform);
        newObject.transform.LookAt(orbitGO.transform.position);
        newObject.transform.Rotate(-90, 0, 0);
        spawnedObjects.Add(newObject);
        newObject.gameObject.name = TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name;
        
        if (passive > 0)
        {
            AddsPassive passiveEarner = newObject.AddComponent<AddsPassive>();
            passiveEarner.setAmount(passive);
        }
    }

    public void removeObject(int index)
    {
        for (int i = 0; i < spawnedObjects.Count; i++) 
        {
            if (spawnedObjects[i].gameObject.name == TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name)
            {
                GameObject newObject;
                newObject = spawnedObjects[i];
                spawnedObjects.RemoveAt(i);
                Destroy(newObject);
            }
        }
    }
}

