using TBSplineS;
using UnityEngine;

public class TbsDemoJunctionSwitcher : MonoBehaviour
{
    TbsSplineFollower _follower;
    int _homeSplineId;

    void OnEnable()
    {
        _follower = GetComponent<TbsSplineFollower>();
        if (_follower == null) return;
        _homeSplineId = _follower.SplineId;
        _follower.JunctionReached += OnJunction;
        _follower.ReachedEnd += OnEnd;
    }

    void OnDisable()
    {
        if (_follower == null) return;
        _follower.JunctionReached -= OnJunction;
        _follower.ReachedEnd -= OnEnd;
    }

    static void OnJunction(TbsSplineFollower follower, TbsJunction junction, TbsKnotRef crossed)
    {
        foreach (TbsKnotRef member in junction.Members)
        {
            if (member.Equals(crossed)) continue;
            follower.SwitchToBranch(member);
            break;
        }
    }

    void OnEnd(TbsSplineFollower follower)
    {
        follower.SplineId = _homeSplineId;
        follower.Distance = 0f;
        follower.Play();
    }
}
