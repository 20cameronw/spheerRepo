using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Transform target;

    public float speed = 50f;

    public GameObject fireHitParticleSystem;
    //public GameObject core;

    public float damage = 1;

    public void Seek (Transform _target)
    {
        target = _target;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        if (dir.magnitude <= distanceThisFrame)
        {
            HitTarget();
            return;
        }
        transform.Translate(dir.normalized * distanceThisFrame, Space.World);
    }

    void HitTarget()
    {
        //Debug.Log("Hit something");
        //Quaternion rotation = Quaternion.FromToRotation(Camera.main.transform.position, core.transform.position);
        GameObject fireHit = Instantiate(fireHitParticleSystem, transform.position, Quaternion.identity);
        fireHit.GetComponent<ParticleSystem>().Play();
        //target.GetComponent<Enemy>().takeHit(damage); FIX ME
        Destroy(this.gameObject);
        Destroy(fireHit, 0.5f);
    } 

}
