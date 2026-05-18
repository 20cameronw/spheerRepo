using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetSuckedUp : MonoBehaviour
{
    private float speed = 10;

    private Transform target;

    public bool gettingSucked;

    [SerializeField] private int upgradeIndex;


    void Update()
    {
        if (target == null)
        {
            gettingSucked = false;
            return;
        }
        if (gettingSucked == true)
        {
            Vector3 dir = target.position - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (dir.magnitude - 5 <= distanceThisFrame)
            {
                gettingSucked = false;
                Player.Instance.removeUpgrade(upgradeIndex);
                Destroy(gameObject);
            }

            transform.Translate(dir.normalized * distanceThisFrame, Space.World);
        }
    }
    public void getSuckedUp(Transform sucker)
    {
        // Free the slot before reparenting — transform.position is still in world space here.
        WorldSpawner ws = FindObjectOfType<WorldSpawner>();
        if (ws != null)
            ws.FreeSlotAtPosition(transform.position);

        target = sucker;
        gettingSucked = true;
        transform.SetParent(sucker);
        Debug.Log("Object is being sucked up: " + gameObject.name);
    }

}
