using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HexToRGB : MonoBehaviour
{
    public InputField hex;
    public InputField rgb;
    public Graphic graphic;

    public void ToRGB(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        var c32 = (Color32)color;
        rgb.text = $"{c32.r}, {c32.g}, {c32.b}, {c32.a}";
        graphic.color = c32;
    }
    public void ToHex(string rgb)
    {
        rgb = rgb.Replace(" ", "");

        var color = default(Color32);
        var colors = rgb.Split(',');
        var i = 0;
        foreach (var c in colors)
        {
            Debug.Log(c);
            if (byte.TryParse(c, out var v))
                color[i] = v;
            i++;
        }

        graphic.color = color;
        hex.text = "#" + ColorUtility.ToHtmlStringRGBA(color).ToLower();
    }
}
