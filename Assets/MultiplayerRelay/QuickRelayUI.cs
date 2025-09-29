using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickRelayUI : MonoBehaviour
{
    [SerializeField] Button hostBtn;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] Button joinBtn;
    [SerializeField] TMP_Text codeLabel;

    void Awake()
    {
        hostBtn.onClick.AddListener(async () =>
        {
            var code = await RelayTransportBootstrap.StartHostWithRelayAsync(10);
            if (codeLabel) codeLabel.text = "Código: " + code;
        });

        joinBtn.onClick.AddListener(async () =>
        {
            var code = codeInput.text.Trim();
            if (!string.IsNullOrEmpty(code))
                await RelayTransportBootstrap.StartClientWithRelayAsync(code);
        });
    }
}
