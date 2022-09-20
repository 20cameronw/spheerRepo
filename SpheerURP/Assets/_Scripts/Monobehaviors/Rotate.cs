using UnityEngine;

public class Rotate : MonoBehaviour
{
    [SerializeField] private float x;
    [SerializeField] private float y;
    [SerializeField] private float z;

    void Update()
    {
        transform.Rotate(x * Time.deltaTime, y * Time.deltaTime, z * Time.deltaTime);
    }

    public void SetSpeeds(float _x, float _y, float _z)
    {
        x = _x;
        y = _y;
        z = _z;
    }
}
