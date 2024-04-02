using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PP2ProductionStats.Helpers;

public static class UnityHelper
{
    public static GameObject GetGameObjectFromPaths(this IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var objectAtPath = GameObject.Find(path);

            if (objectAtPath != null)
            {
                return objectAtPath;
            }
        }

        return null;
    }

    public static TextMeshProUGUI GetOrCreateLabel(this GameObject parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject textObject;
        var layoutObject = parent.Children().Find(c => c.name == name);

        if (layoutObject == null)
        {
            layoutObject = new GameObject(name);
            layoutObject.transform.SetParent(parent.transform);
            layoutObject.layer = 5; // The UI layer

            var layoutComponent = layoutObject.AddComponent<VerticalLayoutGroup>();
            layoutComponent.childControlHeight = true;
            layoutComponent.childControlWidth = true;
            layoutComponent.childForceExpandHeight = false;
            layoutComponent.childForceExpandWidth = true;
            layoutComponent.childScaleHeight = true;
            layoutComponent.childScaleWidth = false;

            textObject = new GameObject("Text");
            textObject.transform.SetParent(layoutObject.transform);
            textObject.layer = 5;

            var label = textObject.AddComponent<TextMeshProUGUI>();
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
        else
        {
            textObject = layoutObject.Children().Find(c => c.name == "Text");
        }

        layoutObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        textObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        var layoutPosition = layoutObject.transform.localPosition;
        layoutObject.transform.localPosition = new Vector3(layoutPosition.x, layoutPosition.y, 0.0f);

        var textPosition = textObject.transform.localPosition;
        textObject.transform.localPosition = new Vector3(textPosition.x, textPosition.y, 0.0f);

        return textObject.GetComponent<TextMeshProUGUI>();
    }
}