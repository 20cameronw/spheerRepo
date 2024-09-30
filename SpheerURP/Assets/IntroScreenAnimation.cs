using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IntroScreenAnimation : MonoBehaviour
{
    [SerializeField] private Transform endPoint;

    [SerializeField] private GameObject image;

    [SerializeField] private float secondsToFly;


    void Awake()
    {
        LeanTween.move(image, endPoint, secondsToFly).setEase(LeanTweenType.easeInQuad);
    }
}
