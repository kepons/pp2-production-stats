using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PP2ProductionStats.Components;

public class ClickableText : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Plugin.PerHour = !Plugin.PerHour;
    }
}