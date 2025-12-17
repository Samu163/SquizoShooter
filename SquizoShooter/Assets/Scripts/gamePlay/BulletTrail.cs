using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class BulletTrail : MonoBehaviour
{
    private LineRenderer lineRenderer;
    public float fadeDuration = 0.1f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
    }

    public void SetPositions(Vector3 start, Vector3 end)
    {
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        Color startColor = lineRenderer.startColor;
        Color endColor = lineRenderer.endColor;
        startColor.a = 1f;
        endColor.a = 1f;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;

        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color startColor = lineRenderer.startColor;
        Color endColor = lineRenderer.endColor;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            lineRenderer.startColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
            lineRenderer.endColor = new Color(endColor.r, endColor.g, endColor.b, alpha);

            yield return null;
        }

        if (BulletTrailPool.Instance != null)
        {
            BulletTrailPool.Instance.ReturnToPool(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}