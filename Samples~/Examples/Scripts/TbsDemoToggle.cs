using UnityEngine;

namespace TBSplineS.Examples
{
    public class TbsDemoToggle : MonoBehaviour
    {
        public GameObject Target;

        public void Toggle()
        {
            if (Target != null) Target.SetActive(!Target.activeSelf);
        }
    }
}
