using UnityEngine;

// Straßenschilder: an jeder Kreuzung ein Mast mit zwei grünen Schildern (eins pro Straße), auf beiden Seiten beschriftet.
// Der Mast steht an der Hausecke (0.8 m vor beiden Fassaden), also hinter jeder späteren Laufbahn auf dem Gehweg:
// So steht er nie zwischen Kamera und Spieler.
public static partial class CityBuilder
{
    private static readonly Color SignGreen = new Color(0.1f, 0.42f, 0.24f);

    private static void BuildStreetSigns()
    {
        Transform signs = Group("Street Signs");
        Material pole = Mat("Iron");
        Material plate = Mat("Street Sign", SignGreen, 0.4f);

        foreach (CityLayout.Street across in CityLayout.Streets)
        {
            if (!across.Vertical)
            {
                continue;
            }
            foreach (CityLayout.Street along in CityLayout.Streets)
            {
                // Kreuzen sich die beiden Straßen (inklusive T-Kreuzungen an den Enden)?
                if (along.Vertical || across.At < along.From - 0.1f || across.At > along.To + 0.1f ||
                    along.At < across.From - 0.1f || along.At > across.To + 0.1f)
                {
                    continue;
                }

                // Ecke zur Stadtmitte hin: am Ostring nach Westen, am Nordring nach Süden, sonst Nordost
                float sx = across.At > CityLayout.East ? -1f : 1f;
                float sz = along.At > CityLayout.North ? -1f : 1f;
                var corner = new Vector3(across.At + sx * FacadeDistance(across), Surface, along.At + sz * FacadeDistance(along));

                Transform sign = new GameObject($"Sign {along.Name} / {across.Name}").transform;
                sign.SetParent(signs, false);
                sign.position = corner;
                Cylinder("Pole", sign, new Vector3(0f, 1.8f, 0f), 0.07f, 3.6f, pole);
                // Schild der Ost-West-Straße zeigt entlang X, das der Nord-Süd-Straße entlang Z, jeweils zur Kreuzung hin
                Blade(sign, along.Name, new Vector3(-sx * 1.3f, 3.1f, 0f), 0f, plate);
                Blade(sign, across.Name, new Vector3(0f, 3.6f, -sz * 1.3f), 90f, plate);
            }
        }
    }

    // Abstand Straßenmitte → Fassade, minus 0.8 m (dort steht der Mast)
    private static float FacadeDistance(CityLayout.Street street)
    {
        float facade = street.Type == CityLayout.Side.Ring ? CityLayout.RingOffset + CityLayout.FacadeInset(street.Type) : CityLayout.FacadeInset(street.Type);
        return facade - 0.8f;
    }

    // Schild mit Text vorne und hinten. Ein TextMeshPro-Text ist von hinten gespiegelt, deshalb zwei Texte Rücken an Rücken
    private static void Blade(Transform sign, string name, Vector3 position, float yaw, Material plate)
    {
        Transform blade = PrimitiveKit.Pivot("Blade " + name, sign, position);
        blade.localRotation = Quaternion.Euler(0f, yaw, 0f);
        PrimitiveKit.Box("Plate", blade, Vector3.zero, new Vector3(2.6f, 0.5f, 0.05f), plate);
        PrimitiveKit.Text("Text Front", blade, new Vector3(0f, 0f, -0.035f), new Vector2(2.4f, 0.4f), name.ToUpperInvariant(), Color.white);
        var back = PrimitiveKit.Text("Text Back", blade, new Vector3(0f, 0f, 0.035f), new Vector2(2.4f, 0.4f), name.ToUpperInvariant(), Color.white);
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }
}
