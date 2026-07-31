using UnityEngine;

public class TbsDemoOrbit : MonoBehaviour
{
    public Vector3 Center;
    public float Radius = 10f;
    public float DegreesPerSecond = 40f;
    public float Height = 2f;

    float _angle;

    void Update()
    {
        _angle += DegreesPerSecond * Time.deltaTime * Mathf.Deg2Rad;
        transform.position = Center + new Vector3(Mathf.Cos(_angle) * Radius, Height, Mathf.Sin(_angle) * Radius);
    }
}
