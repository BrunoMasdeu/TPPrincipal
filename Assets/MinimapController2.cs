
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapController2 : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform playersContainer;

    [Header("Limites del escenario")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minZ = -50f;
    [SerializeField] private float maxZ = 50f;

    [Header("Configuracion")]
    [SerializeField] private float dotSize = 8f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertZ = false;

    [Header("Colores")]
    [SerializeField] private Color playerColor = Color.white;
    [SerializeField] private Color teammateColor = Color.green;

    private Image playerDot;
    private Sprite dotSprite;

    private readonly Dictionary<Transform, Image> teammateDots = new();

    private void Awake()
    {
        Texture2D texture = Texture2D.whiteTexture;

        dotSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        if (playersContainer != null)
        {
            if (player != null)
            {
                playerDot = CreateDot("PlayerDot", playerColor);
            }
        }
    }

    private Image CreateDot(string dotName, Color color)
    {
        GameObject dotObject = new GameObject(
            dotName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        dotObject.transform.SetParent(playersContainer, false);

        Image dot = dotObject.GetComponent<Image>();
        dot.sprite = dotSprite;
        dot.color = color;
        dot.raycastTarget = false;

        RectTransform rect = dot.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(dotSize, dotSize);

        return dot;
    }

    public void SetTeammates(List<Transform> teammates)
    {
        foreach (Image dot in teammateDots.Values)
        {
            if (dot != null)
            {
                Destroy(dot.gameObject);
            }
        }

        teammateDots.Clear();

        if (teammates == null || playersContainer == null)
        {
            return;
        }

        foreach (Transform teammate in teammates)
        {
            if (teammate == null || teammate == player)
            {
                continue;
            }

            Image dot = CreateDot("TeammateDot", teammateColor);
            teammateDots.Add(teammate, dot);
        }
    }

    private void LateUpdate()
    {
        if (playersContainer == null)
        {
            return;
        }

        if (player != null && playerDot != null)
        {
            UpdateDot(playerDot, player.position);
        }

        foreach (KeyValuePair<Transform, Image> entry in teammateDots)
        {
            if (entry.Key != null && entry.Value != null)
            {
                UpdateDot(entry.Value, entry.Key.position);
            }
        }
    }

    private void UpdateDot(Image dot, Vector3 worldPosition)
    {
        if (maxX <= minX || maxZ <= minZ)
        {
            return;
        }

        float normalizedX = Mathf.InverseLerp(
            minX, maxX, worldPosition.x
        );

        float normalizedZ = Mathf.InverseLerp(
            minZ, maxZ, worldPosition.z
        );

        if (invertX)
        {
            normalizedX = 1f - normalizedX;
        }

        if (invertZ)
        {
            normalizedZ = 1f - normalizedZ;
        }

        float width = playersContainer.rect.width;
        float height = playersContainer.rect.height;

        Vector2 position = new Vector2(
            (normalizedX - 0.5f) * width,
            (normalizedZ - 0.5f) * height
        );

        dot.rectTransform.anchoredPosition = position;
    }

    private void OnDestroy()
    {
        if (dotSprite != null)
        {
            Destroy(dotSprite);
        }
    }
}
