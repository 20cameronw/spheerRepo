using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Transform target;

    public float speed = 50f;

    public GameObject fireHitParticleSystem;
    //public GameObject core;

    public int damage = 1;

    public void Seek(Transform _target)
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
        float dmg = damage * Player.Instance.getTurretDamageMultiplier();
        GameObject fireHit = Instantiate(fireHitParticleSystem, transform.position, Quaternion.identity);
        fireHit.GetComponent<ParticleSystem>().Play();

        if (target != null)
        {
            EnemyHealth eh = target.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(dmg);
            }
            else
            {
                // Target is an enemy world building or the world itself during an attack.
                IAttackable attackable = target.GetComponentInParent<IAttackable>();
                attackable?.TakeDamage(dmg, AttackWeaponType.Turret);
            }
        }

        Destroy(this.gameObject);
        Destroy(fireHit, 0.5f);
    }

}
