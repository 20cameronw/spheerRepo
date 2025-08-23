using UnityEngine;
using TMPro;

public class SpaceJunk : MonoBehaviour
{
    public float speed = 30f;
    private Vector3 targetPos;

    public GameObject floatingTextPrefab;

    public void Initialize(Vector3 endPos)
    {
        targetPos = endPos;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.5f)
        {
            Destroy(gameObject);
        }
    }

    private void OnMouseDown()
    {
        // Get the amount collected
        float collectedAmount = Player.Instance.collectSpaceJunk();


        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null)
        {
            string message = "+";
            if (collectedAmount > 999999999)
            {
                message += collectedAmount.ToString("0.##E0");
            }
            else
            {
                message += Mathf.Round(collectedAmount).ToString("N0");
            }
            ui.CreateAnimatedText(message, Color.yellow);
        }

        Destroy(gameObject);
    }
}
