using UnityEngine;
using TMPro;
using System.Collections;

public class RoundCountdownUI : MonoBehaviour
{
    public TextMeshProUGUI countdownText;

    public void StartCountdown(int seconds, System.Action onFinish = null)
    {
        gameObject.SetActive(true);
        StartCoroutine(DoCountdown(seconds, onFinish));
    }

    IEnumerator DoCountdown(int seconds, System.Action onFinish)
    {
        int t = seconds;
        while (t > 0)
        {
            countdownText.text = t.ToString();
            yield return new WaitForSeconds(1f);
            t--;
        }

        countdownText.text = "GO!";
        yield return new WaitForSeconds(1f);

        gameObject.SetActive(false);
        onFinish?.Invoke();
    }
}