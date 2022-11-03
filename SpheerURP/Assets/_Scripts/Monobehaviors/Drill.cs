using UnityEngine;


public class Drill : MonoBehaviour
{
    [SerializeField] private GameObject partToMove;

    void Start()
    {
        LeanTween.moveSplineLocal(partToMove, new Vector3[]{new Vector3(0f, 0f, 0f),
                                                            new Vector3(0f, -2f, 0f),
                                                            new Vector3(0f, -1f, 0f), 
                                                            new Vector3(0f, -2f, 0f),
                                                            new Vector3(0f, 0f, 0f)}, 3f)
                                                            .setEase(LeanTweenType.easeOutQuad)
                                                            .setLoopPingPong();
    }
}
