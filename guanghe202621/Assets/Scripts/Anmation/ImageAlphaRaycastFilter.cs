using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImageAlphaRaycastFilter : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float threshold = 0.15f;

    private void Awake()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = threshold;
    }
}
