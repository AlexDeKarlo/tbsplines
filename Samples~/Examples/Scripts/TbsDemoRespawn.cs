using UnityEngine;

namespace TBSplineS.Examples
{
    public class TbsDemoRespawn : MonoBehaviour
    {
        public float MinY = -3f;

        Vector3 _start;
        Rigidbody _body;

        void Start()
        {
            _start = transform.position;
            _body = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (transform.position.y >= MinY) return;
            transform.position = _start;
            if (_body == null) return;
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = Vector3.zero;
#else
            _body.velocity = Vector3.zero;
#endif
            _body.angularVelocity = Vector3.zero;
        }
    }
}
