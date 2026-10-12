using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialTaskUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI taskText;
    [SerializeField] private GameObject checkmarkIcon;
    [SerializeField] private Color completedColor = Color.gray;

    private void Awake()
    {
        // Asegurarse de que la tilde esté oculta al instanciar
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
    }

    public void SetupTask(string text)
    {
        taskText.text = text;
    }

    public void MarkAsCompleted()
    {
        if (checkmarkIcon != null) checkmarkIcon.SetActive(true);
        taskText.color = completedColor; //oscurecer el texto
        taskText.fontStyle = FontStyles.Strikethrough; //tachar el texto
    }
}


