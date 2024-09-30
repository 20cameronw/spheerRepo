using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetSuckedUp : MonoBehaviour
{
    [SerializeField] private float speed;

    private Transform target;

    private bool gettingSucked;

    [SerializeField] private ParticleSystem suckingEffect;

    private Transform startingPos;

    [SerializeField] private float bonus;

    [SerializeField] private int upgradeIndex;

    void Start()
    {
        startingPos = transform;
    }

    void Update()
    {
        if (target == null)
        {
            gettingSucked = false;
            return;
        }

        if (gettingSucked == true)
        {
            ParticleSystem currentEffect = Instantiate(suckingEffect, startingPos);
            currentEffect.transform.SetParent(target);
            currentEffect.transform.LookAt(target);
            Vector3 dir = target.position - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (dir.magnitude <= distanceThisFrame)
            {
                transform.SetParent(target);
                gettingSucked = false;
                Destroy(currentEffect);
                Player.Instance.removeUpgrade(bonus, upgradeIndex);
                Destroy(gameObject);
            }

            transform.Translate(dir.normalized * distanceThisFrame, Space.World);
        }
    }

    public void getSuckedUp(Transform sucker)
    {
        target = sucker;
        gettingSucked = true;
        //Debug.Log("set getting sucked to true");
    }

}
